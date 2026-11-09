using BulkyBook.Business.Services.IServices;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using Stripe.Checkout;

namespace BulkyBookWeb.Controllers
{
    /// <summary>
    /// Stripe -> server notifications. Configure in the Stripe dashboard:
    ///   endpoint  : https://YOUR-DOMAIN/stripe/webhook
    ///   events    : payment_intent.succeeded (on-site payment), checkout.session.completed, checkout.session.expired
    ///   secret    : put the signing secret in configuration key "Stripe:WebhookSecret".
    /// </summary>
    [ApiController]
    [Route("stripe/webhook")]
    public class StripeWebhookController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly IOrderService _orderService;
        private readonly IShoppingCartService _cartService;
        private readonly IApplicationUserService _userService;
        private readonly IEmailService _emailService;
        private readonly ILogger<StripeWebhookController> _logger;

        public StripeWebhookController(IConfiguration config, IOrderService orderService, IShoppingCartService cartService,
            IApplicationUserService userService, IEmailService emailService, ILogger<StripeWebhookController> logger)
        {
            _config = config;
            _orderService = orderService;
            _cartService = cartService;
            _userService = userService;
            _emailService = emailService;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> Handle()
        {
            var secret = _config["Stripe:WebhookSecret"];
            if (string.IsNullOrWhiteSpace(secret))
            {
                _logger.LogError("Stripe:WebhookSecret is not configured.");
                return StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            var json = await new StreamReader(Request.Body).ReadToEndAsync();
            Event stripeEvent;
            try
            {
                stripeEvent = EventUtility.ConstructEvent(json, Request.Headers["Stripe-Signature"], secret, throwOnApiVersionMismatch: false);
            }
            catch (StripeException ex)
            {
                _logger.LogWarning(ex, "Invalid Stripe webhook signature.");
                return BadRequest();
            }

            if (stripeEvent.Type == "payment_intent.succeeded" && stripeEvent.Data.Object is PaymentIntent paymentIntent
                && paymentIntent.Metadata != null
                && paymentIntent.Metadata.TryGetValue("orderId", out var piOrderText)
                && int.TryParse(piOrderText, out var piOrderId))
            {
                if (await _orderService.MarkAsPaidAsync(piOrderId, null, paymentIntent.Id))
                {
                    await AfterPaymentAsync(piOrderId);
                }
                return Ok();
            }

            if (stripeEvent.Data.Object is Session session
                && session.Metadata != null
                && session.Metadata.TryGetValue("orderId", out var orderIdText)
                && int.TryParse(orderIdText, out var orderId))
            {
                if (stripeEvent.Type == "checkout.session.completed" &&
                    string.Equals(session.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase))
                {
                    if (await _orderService.MarkAsPaidAsync(orderId, session.Id, session.PaymentIntentId))
                    {
                        await AfterPaymentAsync(orderId);
                    }
                }
                else if (stripeEvent.Type == "checkout.session.expired")
                {
                    await _orderService.CancelPendingOrderAsync(orderId);
                }
            }
            return Ok();
        }

        private async Task AfterPaymentAsync(int orderId)
        {
            try
            {
                var order = await _orderService.GetOrderByIdAsync(orderId, includeUser: true);
                if (order == null) return;
                await _cartService.ClearAsCartAsync(order.ApplicationUserId);
                var email = order.applicationUser?.Email;
                if (!string.IsNullOrEmpty(email))
                {
                    await _emailService.SendOrderConfirmationEmailAsync(email, order.Id, (decimal)order.OrderTotal);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Post-payment steps failed for order {OrderId}", orderId);
            }
        }
    }
}
