using BulkyBook.Business.Services.IServices;
using BulkyBook.Models;
using BulkyBook.Models.ViewModels;
using BulkyBook.Utility;
using Mailjet.Client.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using Stripe.Checkout;
using System.Security.Claims;

namespace BulkyBookWeb.Areas.Customer.Controllers
{
    [Area("Customer")]
    public class CartController : Controller
    {
        private readonly IOrderService _orderService;
        private readonly IShoppingCartService _shoppingCartService;
        private readonly IApplicationUserService _applicationUserService;
        private readonly IEmailService _emailService;   
        public CartController(IOrderService orderService ,IShoppingCartService shoppingCartService,IApplicationUserService applicationUserService,IEmailService emailService)
        {
            _orderService = orderService;
            _shoppingCartService = shoppingCartService;
            _applicationUserService = applicationUserService;
            _emailService = emailService;
        }

        public async Task<IActionResult> Index()
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var userId = claimsIdentity?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var user = await _applicationUserService.GetUserByIdAsync(userId);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }
            var cartItems = await _shoppingCartService.GetUserCartItemsAsync(userId);
            ShoppingCartVM shoppingCartVM = new ShoppingCartVM
            {
                ShoppingCartList = cartItems,
                OrderHeader = new OrderHeader()
            };
            shoppingCartVM.OrderHeader.applicationUser = user;
            shoppingCartVM.OrderHeader.ApplicationUserId = userId;
            shoppingCartVM.OrderHeader.Name = user.Name;
            shoppingCartVM.OrderHeader.PhoneNumber = user.PhoneNumber;
            shoppingCartVM.OrderHeader.StreetAddress = user.StreetAddress;
            shoppingCartVM.OrderHeader.City = user.City;
            shoppingCartVM.OrderHeader.State = user.State;
            shoppingCartVM.OrderHeader.PostalCode = user.PostalCode;

            foreach (var cart in shoppingCartVM.ShoppingCartList)
            {
                shoppingCartVM.OrderHeader.OrderTotal += (cart.Product.Price * cart.Count);
            }

            return View(shoppingCartVM);
        }
        [HttpPost]
        [ActionName("Index")]
        public async Task<IActionResult> IndexPOST(ShoppingCartVM shoppingCartVM)
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var userId = claimsIdentity?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var user = await _applicationUserService.GetUserByIdAsync(userId);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }
            var cartItems = await _shoppingCartService.GetUserCartItemsAsync(userId);
            shoppingCartVM.ShoppingCartList = cartItems;
            shoppingCartVM.OrderHeader.OrderDate = DateTime.UtcNow;
            shoppingCartVM.OrderHeader.ApplicationUserId = userId;
            

            foreach (var cart in shoppingCartVM.ShoppingCartList)
            {
                shoppingCartVM.OrderHeader.OrderTotal += (cart.Product.Price * cart.Count);
            }
            shoppingCartVM.OrderHeader.OrderStatus = SD.StatusPending;
            shoppingCartVM.OrderHeader.OrderDetails = shoppingCartVM.ShoppingCartList.Select(cart => new OrderDetails
            {
                ProductId = cart.ProductId,
                Count = cart.Count,
                Price = cart.Product.Price
            }).ToList();
            await _orderService.CreateOrderAsync(shoppingCartVM.OrderHeader);

            try
            {
                var domain = Request.Scheme + "://" + Request.Host.Value;

                var sessionUrl = await _orderService.CreateStripeCheckoutSessionAsync(shoppingCartVM.OrderHeader, shoppingCartVM.ShoppingCartList, domain);
                await _shoppingCartService.ClearAsCartAsync(userId);
                HttpContext.Session.SetInt32(SD.SessionCart, 0);
                Response.Headers.Append("Location", sessionUrl);
                return new StatusCodeResult(303); // Redirect to Stripe Checkout
            }
            catch (StripeException ex)
            {
                TempData["Error"] = "Payment processing failed. Please try again.";
                return RedirectToAction(nameof(Index));
            }

        }
        public async Task<IActionResult> OrderConfirmation(int id)
        {
            var orderHeader = await _orderService.GetOrderByIdAsync(id, includeUser: true, includDetail: true);

            if (orderHeader == null)
            {
                return NotFound();
            }
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var userId = claimsIdentity?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var user = await _applicationUserService.GetUserByIdAsync(userId);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }
            if (orderHeader.ApplicationUserId != userId)
            {
                return RedirectToAction("AccessDenied", "Account", new { area = "Identity" });
            }
            try
            {
                var result = await _orderService.VerifyStripePaymentAsync(orderHeader);
                if (result)
                {
                    TempData["success"] = "Payment complete successflly. Your Order has been confirmed";
                }
                else
                {
                    TempData["error"] = "Unable to verify payment status. Please contact support with your order number";
                }

            }
            catch (StripeException ex)
            {
                TempData["Error"] = "Payment confirmation failed. Please contact support.";
            }
                await _emailService.SendOrderConfirmationEmailAsync(
               user.Email,
               orderHeader.Id,
               (decimal)orderHeader.OrderTotal);

            return View(id);
        }
        public async Task<IActionResult> Plus(int cartid)
          {
            var cart = await _shoppingCartService.GetCartByIdAsync(cartid);
            if (cart != null)
            {
                if (cart.Count == 1000)
                {
                    // do nothing or show a message that the maximum limit has been reached
                }
                else
                {
                    cart.Count++;
                    await _shoppingCartService.UpdateCartAsync(cart);
                    await UpdateCartSessionAsync();

                }

            }
                return RedirectToAction(nameof(Index));
          }
        public async Task<IActionResult> Minus(int cartid)
        {
            var cart = await _shoppingCartService.GetCartByIdAsync(cartid);
            if (cart != null)
            {
                cart.Count--;
                await _shoppingCartService.UpdateCartAsync(cart);
                await UpdateCartSessionAsync();
            }
            return RedirectToAction(nameof(Index));
        }
        public async Task<IActionResult> Remove(int cartid)
        {
            var cart = await _shoppingCartService.GetCartByIdAsync(cartid);
            if (cart != null)
            {
                cart.Count = 0 ;
                await _shoppingCartService.UpdateCartAsync(cart);
                await UpdateCartSessionAsync();
            }
            return RedirectToAction(nameof(Index));
        }
        public async Task<IActionResult> updateCart(int cartId, int count)
        {
            var cart = await _shoppingCartService.GetCartByIdAsync(cartId);
            if (cart == null)
            {
                return NotFound();
            }
            if(count <= 1)
            {
                cart.Count = 0;
            }
            else
            {
                if(count >= 1000)
                    cart.Count = 1000;
                else
                    cart.Count = count;
            }   
                await _shoppingCartService.UpdateCartAsync(cart);
            await UpdateCartSessionAsync();
            return Ok(new {success =true});
        }  
        private async Task UpdateCartSessionAsync()
        {
            var calimIdentity = (ClaimsIdentity)User.Identity;
            var userId = calimIdentity?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (! string.IsNullOrEmpty(userId))
            {
                var Count = await _shoppingCartService.GetCartCountAsync(userId);
                HttpContext.Session.SetInt32(SD.SessionCart, Count);
            }

            
        }
    }
}
