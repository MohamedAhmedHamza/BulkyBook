using BulkyBook.Business.Services.IServices;
using BulkyBook.Models;
using BulkyBook.Models.ViewModels;
using BulkyBook.Utility;
using BulkyBookWeb.DataAccess.Data;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;
using Stripe.Climate;
using System;
using System.Collections.Generic;
using System.Text;

namespace BulkyBook.Business.Services
{
    public class OrderService : IOrderService
    {
        public readonly ApplicationDbContext _db;

        public OrderService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<bool> CancelOrderWithRefundAsync(int orderId)
        {
            var order = await _db.OrderHeaders.FindAsync(orderId);

            if (order == null)
            {
                throw new KeyNotFoundException($"Order {orderId} not found");
            }
            if(order.OrderStatus == SD.StatusShipped)
            {
                throw new InvalidOperationException("Can't cancel orders that have already been shipped ");
            }
            if(order.OrderStatus == SD.StatusCancelled || order.OrderStatus == SD.StatusRefunded)
            {
                throw new InvalidOperationException("Order has already been cancelled");    
            }
            bool refundIssued = false;
            if(!string.IsNullOrEmpty(order.PaymentIntentId) && (order.OrderStatus == SD.StatusApproved || order.OrderStatus == SD.StatusInProcess))
            {
                try
                {
                    var options = new RefundCreateOptions {PaymentIntent = order.PaymentIntentId, Reason = RefundReasons.RequestedByCustomer};
                    var service = new RefundService();
                    Refund refund = service.Create(options);
                    if (refund.Status == "succeeded" || refund.Status == "pending")
                    {
                        refundIssued = true;
                        order.OrderStatus = SD.StatusRefunded;
                    }
                }
                catch(Exception ex)
                {
                    order.OrderStatus = SD.StatusCancelled;
                    await _db.SaveChangesAsync();
                    throw new InvalidOperationException($"Stripe refund faild:{ex.Message}.Order has been cancelled ");
                }
            }
            else
            {
                order.OrderStatus = SD.StatusCancelled;
            }

            await _db.SaveChangesAsync();
            return refundIssued;
        }

        public async Task<OrderHeader> CreateOrderAsync(OrderHeader orderHeader)
        {
            _db.OrderHeaders.Add(orderHeader);
            await _db.SaveChangesAsync();
            return orderHeader;
        }

        public async Task<string> CreateStripeCheckoutSessionAsync(OrderHeader orderHeader, IEnumerable<ShoppingCart> cartItems, string domain)
        {
            var option = new SessionCreateOptions
            {
                SuccessUrl = domain + $"/Customer/Cart/OrderConfirmation?id={orderHeader.Id}",
                CancelUrl = domain + $"/Customer/Cart/Index",
                LineItems = new List<SessionLineItemOptions>(),
                Mode = "payment",
                Metadata = new Dictionary<string, string>
                    {
                        { "orderId", orderHeader.Id.ToString() }
                    }
            };

            foreach (var item in cartItems)
            {
                var sessionLineItem = new SessionLineItemOptions()
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        UnitAmount = (long)(item.Product.Price * 100), // Convert to cents
                        Currency = "usd",
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = item.Product.Title,
                        },
                    },
                    Quantity = item.Count,
                };
                option.LineItems.Add(sessionLineItem);
            }
            var service = new SessionService();
            Session session = service.Create(option);
            await UpdateStripePaymentAsync(orderHeader.Id, session.Id, session.PaymentIntentId);
            return session.Url;
        }

        public async Task<IEnumerable<OrderHeader>> GetAllOrderAsync(string? userId = null, string? status = null, bool includeUser = false, bool includDetail = false)
        {
            var query = _db.OrderHeaders.AsQueryable();
            if (includeUser)
            {
                query = query.Include(o => o.applicationUser);
            }
            if (includDetail)
            {
                query = query.Include(o => o.OrderDetails).ThenInclude(od => od.Product);
            }
            if(!string.IsNullOrEmpty(userId))
            {
                query = query.Where(o => o.ApplicationUserId == userId);
            }
            if (!string.IsNullOrEmpty(status) && status.ToLower() != "all")
            {
                if(status.ToLower() == "cancelled")
                {
                    query = query.Where(u => u.OrderStatus == SD.StatusCancelled || u.OrderStatus == SD.StatusRefunded);
                }
                else
                {
                    query = query.Where(o => o.OrderStatus.ToLower() == status.ToLower());
                }
            }
            return await query.ToListAsync();
        }

        public async Task<OrderHeader?> GetOrderByIdAsync(int Id, bool includeUser = false, bool includDetail = false)
        {
            var query = _db.OrderHeaders.AsQueryable();
            if (includeUser)
            {
                query = query.Include(o => o.applicationUser);
            }
            if (includDetail)
            {
                query = query.Include(o => o.OrderDetails).ThenInclude(od => od.Product);
            }
            
            return await query.FirstOrDefaultAsync(u => u.Id == Id);
        }
        public async Task UpdateOrderAsync(OrderHeader orderHeader)
        {
            _db.OrderHeaders.Update(orderHeader);
            await _db.SaveChangesAsync();
        }

        public async Task UpdateOrderStatusAsync(int id, string orderStatus, string? carrier = null, string? trackingNumber = null)
        {
            var order = await _db.OrderHeaders.FindAsync(id);
            if (order == null)
            {
                throw new ArgumentException("Order not found");
            }
            order.OrderStatus = orderStatus;
            if(orderStatus == order.OrderStatus)
            {
                order.ShippingDate = DateTime.Now; 
                if (!string.IsNullOrEmpty(carrier))
                {
                    order.Carrier = carrier;
                }
                if (!string.IsNullOrEmpty(trackingNumber))
                {
                    order.TrackingNumber = trackingNumber;
                }
            }
            await _db.SaveChangesAsync();
        }

        public async Task UpdateStripePaymentAsync(int orderId, string sessionId, string paymentIntentId)
        {
            var order = await _db.OrderHeaders.FindAsync(orderId);
            if (order == null)
            {
                throw new ArgumentException($"Order {orderId} not found");
            }
            if (!string.IsNullOrEmpty(sessionId))
            {
                order.SessionId = sessionId;
            }
            if (!string.IsNullOrEmpty(paymentIntentId))
            {
                order.PaymentIntentId = paymentIntentId;
            }
            await _db.SaveChangesAsync();
        }

        public async Task<bool> VerifyStripePaymentAsync(OrderHeader orderHeader)
        {
            var service = new SessionService();
            Session session = service.Get(orderHeader.SessionId);
            if (session.PaymentStatus.ToLower() == "paid")
            {
                await UpdateStripePaymentAsync(orderHeader.Id, session.Id, session.PaymentIntentId);
                await UpdateOrderStatusAsync(orderHeader.Id, SD.StatusApproved);
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
