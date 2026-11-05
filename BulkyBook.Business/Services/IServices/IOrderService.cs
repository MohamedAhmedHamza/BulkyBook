using BulkyBook.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace BulkyBook.Business.Services.IServices
{
    public interface IOrderService
    {
        Task<OrderHeader> CreateOrderAsync(OrderHeader orderHeader /*, IEnumerable<ShoppingCart> shoppingCartItems*/);
        Task<OrderHeader?> GetOrderByIdAsync(int Id,bool includeUser = false , bool includDetail = false);
        Task<IEnumerable<OrderHeader>> GetAllOrderAsync(string? userId = null,string? status = null, bool includeUser = false, bool includDetail = false);
        Task UpdateOrderAsync(OrderHeader orderHeader);
        Task UpdateOrderStatusAsync(int id , string orderStatus , string? carrier = null ,string? trackingNumber = null);
        Task UpdateStripePaymentAsync(int orderId, string sessionId , string paymentIntentId);

        Task<bool> CancelOrderWithRefundAsync(int orderID);
        Task<string> CreateStripeCheckoutSessionAsync(OrderHeader orderHeader,IEnumerable<ShoppingCart> cartItems, string domain);
        Task<PaymentVerification> VerifyStripePaymentAsync(OrderHeader orderHeader);
        /// <summary>Atomically moves Pending -> Approved. True only for the call that made the transition (idempotent).</summary>
        Task<bool> MarkAsPaidAsync(int orderId, string? sessionId, string? paymentIntentId);
        /// <summary>Atomically moves Pending -> Cancelled (abandoned / expired checkout).</summary>
        Task<bool> CancelPendingOrderAsync(int orderId);

    }

    /// <param name="IsPaid">Stripe reports the checkout session as paid.</param>
    /// <param name="NewlyApproved">This call is the one that approved the order (send e-mail / clear cart once).</param>
    public record PaymentVerification(bool IsPaid, bool NewlyApproved);
}
