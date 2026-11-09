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
    /// <summary>
    /// On-site payment (Stripe Payment Element). The page is styled by us (Aurora);
    /// card data never touches our server (Stripe.js iframe).
    /// Flow:  Cart  ->  Checkout/Pay  ->  Stripe confirms  ->  Checkout/Complete  ->  Cart/OrderConfirmation
    /// </summary>
    [Area("Customer")]
    [Authorize]
    public class CheckoutController : Controller
    {
        private readonly IOrderService _orderService;
        private readonly IShoppingCartService _cartService;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _config;
        private readonly ILogger<CheckoutController> _logger;

        public CheckoutController(IOrderService orderService, IShoppingCartService cartService, IEmailService emailService,
            IConfiguration config, ILogger<CheckoutController> logger)
        {
            _orderService = orderService;
            _cartService = cartService;
            _emailService = emailService;
            _config = config;
            _logger = logger;
        }

        private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

        private static bool IsPaidStatus(string? s) =>
            s == SD.StatusApproved || s == SD.StatusInProcess || s == SD.StatusShipped;

        // ------------------------------------------------------------------ payment page
        public async Task<IActionResult> Pay(int orderId)
        {
            var userId = CurrentUserId;
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var order = await _orderService.GetOrderByIdAsync(orderId, includeUser: true, includDetail: true);
            if (order == null) return NotFound();
            if (order.ApplicationUserId != userId)
            {
                return RedirectToAction("AccessDenied", "Account", new { area = "Identity" });
            }
            if (IsPaidStatus(order.OrderStatus))
            {
                return RedirectToAction("OrderConfirmation", "Cart", new { area = "Customer", id = order.Id });
            }
            if (order.OrderStatus != SD.StatusPending)
            {
                TempData["error"] = "This order can no longer be paid.";
                return RedirectToAction("Index", "Cart", new { area = "Customer" });
            }

            var publishableKey = _config["Stripe:PublishableKey"];
            if (string.IsNullOrWhiteSpace(publishableKey))
            {
                _logger.LogError("Stripe:PublishableKey is not configured.");
                TempData["error"] = "Payments are not configured yet. Please contact support.";
                return RedirectToAction("Index", "Cart", new { area = "Customer" });
            }

            try
            {
                var intent = await GetOrCreateIntentAsync(order);
                if (intent.Status == "succeeded")
                {
                    return RedirectToAction(nameof(Complete), new { orderId = order.Id });
                }

                return View(new CheckoutVM
                {
                    Order = order,
                    ClientSecret = intent.ClientSecret,
                    PublishableKey = publishableKey,
                    CustomerEmail = order.applicationUser?.Email ?? string.Empty,
                    ReturnUrl = Url.Action(nameof(Complete), "Checkout", new { area = "Customer", orderId = order.Id }, Request.Scheme)!
                });
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Could not prepare payment for order {OrderId}", order.Id);
                TempData["error"] = "We couldn't start the payment. Your cart is safe - please try again.";
                return RedirectToAction("Index", "Cart", new { area = "Customer" });
            }
        }

        private async Task<PaymentIntent> GetOrCreateIntentAsync(OrderHeader order)
        {
            var service = new PaymentIntentService();
            var amount = (long)Math.Round(order.OrderTotal * 100);

            if (!string.IsNullOrEmpty(order.PaymentIntentId))
            {
                var existing = await service.GetAsync(order.PaymentIntentId);
                if (existing.Status is "requires_payment_method" or "requires_confirmation" or "requires_action")
                {
                    if (existing.Amount != amount)
                    {
                        existing = await service.UpdateAsync(existing.Id, new PaymentIntentUpdateOptions { Amount = amount });
                    }
                    return existing;
                }
                if (existing.Status == "succeeded") return existing;
            }

            var options = new PaymentIntentCreateOptions
            {
                Amount = amount,
                Currency = "usd",
                AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions { Enabled = true },
                Description = $"BulkyBook order #{order.Id}",
                ReceiptEmail = order.applicationUser?.Email,
                Metadata = new Dictionary<string, string>
                {
                    ["orderId"] = order.Id.ToString(),
                    ["userId"] = order.ApplicationUserId
                }
            };
            var intent = await service.CreateAsync(options, new RequestOptions { IdempotencyKey = $"bulkybook-order-{order.Id}-pi" });
            await _orderService.UpdateStripePaymentAsync(order.Id, string.Empty, intent.Id);
            return intent;
        }

        // ------------------------------------------------------------------ return from Stripe
        public async Task<IActionResult> Complete(int orderId)
        {
            var userId = CurrentUserId;
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var order = await _orderService.GetOrderByIdAsync(orderId, includeUser: true);
            if (order == null) return NotFound();
            if (order.ApplicationUserId != userId)
            {
                return RedirectToAction("AccessDenied", "Account", new { area = "Identity" });
            }
            if (string.IsNullOrEmpty(order.PaymentIntentId))
            {
                return RedirectToAction(nameof(Pay), new { orderId });
            }

            try
            {
                // Never trust the query string: ask Stripe about the order's own PaymentIntent.
                var intent = await new PaymentIntentService().GetAsync(order.PaymentIntentId);

                if (intent.Status == "succeeded")
                {
                    if (await _orderService.MarkAsPaidAsync(order.Id, null, intent.Id))
                    {
                        await AfterPaymentAsync(order, userId);
                    }
                    return RedirectToAction("OrderConfirmation", "Cart", new { area = "Customer", id = order.Id });
                }
                if (intent.Status == "processing")
                {
                    // e.g. bank debits: the webhook will finish the job.
                    return RedirectToAction("OrderConfirmation", "Cart", new { area = "Customer", id = order.Id });
                }
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Could not verify payment for order {OrderId}", orderId);
            }

            TempData["error"] = "The payment was not completed. You can try again.";
            return RedirectToAction(nameof(Pay), new { orderId });
        }

        private async Task AfterPaymentAsync(OrderHeader order, string userId)
        {
            await _cartService.ClearAsCartAsync(userId);
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
    }
}
