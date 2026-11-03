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
        Task<bool> VerifyStripePaymentAsync(OrderHeader orderHeader);

    }
}
