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

            if (order.OrderStatus == SD.StatusShipped)
            {
                throw new InvalidOperationException("Can't cancel orders that have already been shipped.");
            }

            if (order.OrderStatus == SD.StatusCancelled ||
                order.OrderStatus == SD.StatusRefunded)
            {
                throw new InvalidOperationException("Order has already been cancelled or refunded.");
            }

            bool refundIssued = false;

            // Paid orders must be refunded before changing their status.
            if (order.OrderStatus == SD.StatusApproved ||
                order.OrderStatus == SD.StatusInProcess)
            {
                if (string.IsNullOrEmpty(order.PaymentIntentId))
                {
                    throw new InvalidOperationException(
                        "Cannot refund this order because its PaymentIntentId is missing.");
                }

                try
                {
                    var options = new RefundCreateOptions
                    {
                        PaymentIntent = order.PaymentIntentId,
                        Reason = RefundReasons.RequestedByCustomer
                    };

                    var service = new RefundService();
                    Refund refund = service.Create(options);

                    if (refund.Status == "succeeded" || refund.Status == "pending")
                    {
                        refundIssued = true;
                        order.OrderStatus = SD.StatusRefunded;
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            $"Stripe refund was not successful. Refund status: {refund.Status}");
                    }
                }
                catch (Exception ex)
                {
                    // Keep the original order status if the refund fails.
                    throw new InvalidOperationException(
                        $"Stripe refund failed. The order status was not changed. Details: {ex.Message}", ex);
                }
            }
            else
            {
                // Unpaid orders can be cancelled without a refund.
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
                        UnitAmount = (long)Math.Round(item.Price * 100), // tiered price (1-50 / 51-100 / 100+), in cents
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
            if (orderStatus == SD.StatusShipped)
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

        public async Task<PaymentVerification> VerifyStripePaymentAsync(OrderHeader orderHeader)
        {
            // Always get the latest order state from the database.
            var currentOrder = await _db.OrderHeaders
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == orderHeader.Id);

            if (currentOrder == null || string.IsNullOrEmpty(currentOrder.SessionId))
            {
                return new PaymentVerification(false, false);
            }

            // Verify the payment directly with Stripe.
            var service = new SessionService();
            Session session = service.Get(currentOrder.SessionId);

            if (!string.Equals(session.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase))
            {
                return new PaymentVerification(false, false);
            }

            // A cancelled or refunded order must not be displayed as successfully paid.
            if (currentOrder.OrderStatus == SD.StatusCancelled ||
                currentOrder.OrderStatus == SD.StatusRefunded)
            {
                return new PaymentVerification(false, false);
            }

            // Approve the order only if it is still pending.
            if (currentOrder.OrderStatus == SD.StatusPending)
            {
                var newlyApproved = await MarkAsPaidAsync(
                    currentOrder.Id,
                    session.Id,
                    session.PaymentIntentId);

                if (newlyApproved)
                {
                    return new PaymentVerification(true, true);
                }

                // Another request may have updated the order at the same time.
                currentOrder = await _db.OrderHeaders
                    .AsNoTracking()
                    .FirstOrDefaultAsync(o => o.Id == orderHeader.Id);

                if (currentOrder == null)
                {
                    return new PaymentVerification(false, false);
                }
            }

            // Paid orders remain successful without changing their status again.
            bool isOrderPaid =
                currentOrder.OrderStatus == SD.StatusApproved ||
                currentOrder.OrderStatus == SD.StatusInProcess ||
                currentOrder.OrderStatus == SD.StatusShipped;

            return new PaymentVerification(isOrderPaid, false);
        }

        public async Task<bool> MarkAsPaidAsync(int orderId, string? sessionId, string? paymentIntentId)
        {
            var affected = await _db.OrderHeaders
                .Where(o => o.Id == orderId && o.OrderStatus == SD.StatusPending)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(o => o.OrderStatus, SD.StatusApproved)
                    .SetProperty(o => o.SessionId, o => sessionId ?? o.SessionId)
                    .SetProperty(o => o.PaymentIntentId, o => paymentIntentId ?? o.PaymentIntentId));
            return affected > 0;
        }

        public async Task<bool> CancelPendingOrderAsync(int orderId)
        {
            var affected = await _db.OrderHeaders
                .Where(o => o.Id == orderId && o.OrderStatus == SD.StatusPending)
                .ExecuteUpdateAsync(set => set.SetProperty(o => o.OrderStatus, SD.StatusCancelled));
            return affected > 0;
        }
    }
}
