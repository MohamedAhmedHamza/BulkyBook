using BulkyBook.Business.Services.IServices;
using BulkyBook.Models;
using BulkyBook.Models.ViewModels;
using BulkyBook.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using System.Security.Claims;

namespace BulkyBookWeb.Areas.Customer.Controllers
{
    [Area("Customer")]
    [Authorize]
    public class CartController : Controller
    {
        private const int MaxQty = 1000;

        private readonly IOrderService _orderService;
        private readonly IShoppingCartService _shoppingCartService;
        private readonly IApplicationUserService _applicationUserService;
        private readonly IEmailService _emailService;
        private readonly ILogger<CartController> _logger;

        public CartController(IOrderService orderService, IShoppingCartService shoppingCartService,
            IApplicationUserService applicationUserService, IEmailService emailService, ILogger<CartController> logger)
        {
            _orderService = orderService;
            _shoppingCartService = shoppingCartService;
            _applicationUserService = applicationUserService;
            _emailService = emailService;
            _logger = logger;
        }

        private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

        // ---------------------------------------------------------------- cart page
        public async Task<IActionResult> Index()
        {
            var userId = CurrentUserId;
            if (string.IsNullOrEmpty(userId)) return Challenge();
            var user = await _applicationUserService.GetUserByIdAsync(userId);
            if (user == null) return Challenge();

            return View(await BuildCartVmAsync(userId, user, posted: null));
        }

        private async Task<ShoppingCartVM> BuildCartVmAsync(string userId, ApplicationUser user, OrderHeader? posted)
        {
            var items = (await _shoppingCartService.GetUserCartItemsAsync(userId)).ToList();
            var header = new OrderHeader
            {
                ApplicationUserId = userId,
                applicationUser = user,
                Name = posted?.Name ?? user.Name,
                PhoneNumber = posted?.PhoneNumber ?? user.PhoneNumber ?? string.Empty,
                StreetAddress = posted?.StreetAddress ?? user.StreetAddress ?? string.Empty,
                City = posted?.City ?? user.City ?? string.Empty,
                State = posted?.State ?? user.State ?? string.Empty,
                PostalCode = posted?.PostalCode ?? user.PostalCode ?? string.Empty,
                OrderTotal = items.Sum(c => c.Price * c.Count)   // tiered price
            };
            return new ShoppingCartVM { ShoppingCartList = items, OrderHeader = header };
        }

        // ---------------------------------------------------------------- checkout
        [HttpPost]
        [ActionName("Index")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> IndexPOST(ShoppingCartVM shoppingCartVM)
        {
            var userId = CurrentUserId;
            if (string.IsNullOrEmpty(userId)) return Challenge();
            var user = await _applicationUserService.GetUserByIdAsync(userId);
            if (user == null) return Challenge();

            var cartItems = (await _shoppingCartService.GetUserCartItemsAsync(userId)).ToList();
            if (cartItems.Count == 0)
            {
                TempData["error"] = "Your cart is empty.";
                return RedirectToAction(nameof(Index));
            }

            var posted = shoppingCartVM?.OrderHeader ?? new OrderHeader();
            var invalid = false;
            void Require(string? value, string field, string label)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    invalid = true;
                    ModelState.AddModelError("OrderHeader." + field, label + " is required.");
                }
            }
            Require(posted.Name, nameof(OrderHeader.Name), "Name");
            Require(posted.PhoneNumber, nameof(OrderHeader.PhoneNumber), "Phone");
            Require(posted.StreetAddress, nameof(OrderHeader.StreetAddress), "Street address");
            Require(posted.City, nameof(OrderHeader.City), "City");
            Require(posted.State, nameof(OrderHeader.State), "State");
            Require(posted.PostalCode, nameof(OrderHeader.PostalCode), "Postal code");
            if (invalid)
            {
                return View(await BuildCartVmAsync(userId, user, posted));
            }

            // Build the order from trusted data only (never bind status/total/tracking from the form).
            var header = new OrderHeader
            {
                ApplicationUserId = userId,
                Name = posted.Name.Trim(),
                PhoneNumber = posted.PhoneNumber.Trim(),
                StreetAddress = posted.StreetAddress.Trim(),
                City = posted.City.Trim(),
                State = posted.State.Trim(),
                PostalCode = posted.PostalCode.Trim(),
                OrderDate = DateTime.Now,
                OrderStatus = SD.StatusPending,
                OrderTotal = cartItems.Sum(c => c.Price * c.Count),
                OrderDetails = cartItems.Select(c => new OrderDetails
                {
                    ProductId = c.ProductId,
                    Count = c.Count,
                    Price = c.Price
                }).ToList()
            };
            var created = await _orderService.CreateOrderAsync(header);

            try
            {
                var domain = Request.Scheme + "://" + Request.Host.Value;
                var sessionUrl = await _orderService.CreateStripeCheckoutSessionAsync(created, cartItems, domain);
                // The cart is cleared only AFTER payment is confirmed (see OrderConfirmation / webhook).
                Response.Headers.Append("Location", sessionUrl);
                return new StatusCodeResult(303);
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Stripe checkout failed for order {OrderId}", created.Id);
                await _orderService.CancelPendingOrderAsync(created.Id);
                TempData["error"] = "Payment processing failed. Your cart is still saved - please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        public async Task<IActionResult> OrderConfirmation(int id)
        {
            var userId = CurrentUserId;
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var order = await _orderService.GetOrderByIdAsync(id, includeUser: true);
            if (order == null) return NotFound();
            if (order.ApplicationUserId != userId)
            {
                return RedirectToAction("AccessDenied", "Account", new { area = "Identity" });
            }

            var paid = false;
            try
            {
                var result = await _orderService.VerifyStripePaymentAsync(order);
                paid = result.IsPaid;
                if (result.NewlyApproved)
                {
                    await AfterPaymentAsync(order, userId);
                }
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Could not verify payment for order {OrderId}", id);
            }

            ViewBag.Paid = paid;
            return View(id);
        }

        private async Task AfterPaymentAsync(OrderHeader order, string userId)
        {
            await _shoppingCartService.ClearAsCartAsync(userId);
            HttpContext.Session.SetInt32(SD.SessionCart, 0);
            try
            {
                var email = order.applicationUser?.Email;
                if (!string.IsNullOrEmpty(email))
                {
                    await _emailService.SendOrderConfirmationEmailAsync(email, order.Id, (decimal)order.OrderTotal);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Confirmation e-mail failed for order {OrderId}", order.Id);
            }
        }

        // ---------------------------------------------------------------- cart item actions (POST + antiforgery + ownership)
        [HttpPost, ValidateAntiForgeryToken]
        public Task<IActionResult> Plus(int cartid) => ChangeAsync(cartid, +1);

        [HttpPost, ValidateAntiForgeryToken]
        public Task<IActionResult> Minus(int cartid) => ChangeAsync(cartid, -1);

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove(int cartid)
        {
            var cart = await _shoppingCartService.GetCartByIdAsync(cartid);
            if (cart != null && cart.ApplicationUserId == CurrentUserId)
            {
                cart.Count = 0;
                await _shoppingCartService.UpdateCartAsync(cart);
                await UpdateCartSessionAsync();
                TempData["success"] = "Item removed from your cart.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateCart(int cartId, int count)
        {
            var cart = await _shoppingCartService.GetCartByIdAsync(cartId);
            if (cart == null || cart.ApplicationUserId != CurrentUserId)
            {
                return NotFound();
            }
            cart.Count = Math.Min(count, MaxQty);   // < 1 removes the line (handled by the service)
            await _shoppingCartService.UpdateCartAsync(cart);
            await UpdateCartSessionAsync();
            return Ok(new { success = true });
        }

        private async Task<IActionResult> ChangeAsync(int cartId, int delta)
        {
            var cart = await _shoppingCartService.GetCartByIdAsync(cartId);
            if (cart != null && cart.ApplicationUserId == CurrentUserId)
            {
                cart.Count = Math.Clamp(cart.Count + delta, 0, MaxQty);
                await _shoppingCartService.UpdateCartAsync(cart);
                await UpdateCartSessionAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private async Task UpdateCartSessionAsync()
        {
            var userId = CurrentUserId;
            if (!string.IsNullOrEmpty(userId))
            {
                var count = await _shoppingCartService.GetCartCountAsync(userId);
                HttpContext.Session.SetInt32(SD.SessionCart, count);
            }
        }
    }
}
