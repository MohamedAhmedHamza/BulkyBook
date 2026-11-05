
using BulkyBook.Business.Services.IServices;
using BulkyBook.Models;
using BulkyBook.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BulkyBookWeb.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize]
    public class OrderController : Controller
    {
        private readonly IOrderService _orderService;

        [BindProperty]
        public OrderHeader OrderHeader { get; set; } = new();

        public OrderController(IOrderService orderService)
        {
            _orderService = orderService;
        }

        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> Details(int orderId)
        {
            OrderHeader = await _orderService.GetOrderByIdAsync(
                orderId,
                includDetail: true,
                includeUser: true
            );

            if (OrderHeader == null)
            {
                return NotFound();
            }

            var isStaff =
                User.IsInRole(SD.RoleAdmin) ||
                User.IsInRole(SD.RoleEmployee);

            var currentUserId = User.FindFirstValue(
                ClaimTypes.NameIdentifier
            );

            // Customers can only view their own orders.
            if (!isStaff && OrderHeader.ApplicationUserId != currentUserId)
            {
                return RedirectToAction(
                    "AccessDenied",
                    "Account",
                    new { area = "Identity" }
                );
            }

            return View(OrderHeader);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = SD.RoleAdmin + "," + SD.RoleEmployee)]
        public async Task<IActionResult> UpdateOrderDetails(OrderHeader orderHeader)
        {
            var orderHeaderFromDb =
                await _orderService.GetOrderByIdAsync(orderHeader.Id);

            if (orderHeaderFromDb == null)
            {
                TempData["error"] = "Order not found!";
                return RedirectToAction(nameof(Index));
            }

            // Update only the allowed customer and shipping information.
            orderHeaderFromDb.Name = orderHeader.Name;
            orderHeaderFromDb.PhoneNumber = orderHeader.PhoneNumber;
            orderHeaderFromDb.StreetAddress = orderHeader.StreetAddress;
            orderHeaderFromDb.City = orderHeader.City;
            orderHeaderFromDb.State = orderHeader.State;
            orderHeaderFromDb.PostalCode = orderHeader.PostalCode;

            if (!string.IsNullOrEmpty(orderHeader.Carrier))
            {
                orderHeaderFromDb.Carrier = orderHeader.Carrier;
            }

            if (!string.IsNullOrEmpty(orderHeader.TrackingNumber))
            {
                orderHeaderFromDb.TrackingNumber = orderHeader.TrackingNumber;
            }

            await _orderService.UpdateOrderAsync(orderHeaderFromDb);

            TempData["success"] = "Order details updated successfully!";

            return RedirectToAction(
                nameof(Details),
                new { orderId = orderHeaderFromDb.Id }
            );
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = SD.RoleAdmin + "," + SD.RoleEmployee)]
        public async Task<IActionResult> UpdateOrderStatus(string status)
        {
            if (OrderHeader == null || OrderHeader.Id <= 0)
            {
                TempData["error"] = "Invalid order information!";
                return RedirectToAction(nameof(Index));
            }

            var orderHeader =
                await _orderService.GetOrderByIdAsync(OrderHeader.Id);

            if (orderHeader == null)
            {
                TempData["error"] = "Order not found!";
                return RedirectToAction(nameof(Index));
            }

            string successMessage;

            switch (status)
            {
                case SD.StatusInProcess:

                    await _orderService.UpdateOrderStatusAsync(
                        orderHeader.Id,
                        status
                    );

                    successMessage = "Order processing started successfully.";
                    break;

                case SD.StatusShipped:

                    if (string.IsNullOrWhiteSpace(OrderHeader.Carrier) ||
                        string.IsNullOrWhiteSpace(OrderHeader.TrackingNumber))
                    {
                        TempData["error"] =
                            "Carrier and Tracking Number must be provided before shipping the order.";

                        return RedirectToAction(
                            nameof(Details),
                            new { orderId = orderHeader.Id }
                        );
                    }

                    await _orderService.UpdateOrderStatusAsync(
                        orderHeader.Id,
                        SD.StatusShipped,
                        OrderHeader.Carrier,
                        OrderHeader.TrackingNumber
                    );

                    successMessage = "Order shipped successfully.";
                    break;

                case SD.StatusCancelled:
                case SD.StatusRefunded:

                    try
                    {
                        bool refundIssued =
                            await _orderService.CancelOrderWithRefundAsync(
                                orderHeader.Id
                            );

                        if (refundIssued)
                        {
                            successMessage =
                                "Order cancelled and refund request submitted successfully.";
                        }
                        else
                        {
                            successMessage =
                                "Order cancelled successfully. No payment was processed.";
                        }
                    }
                    catch (InvalidOperationException ex)
                    {
                        // The service may wrap Stripe errors in this exception.
                        TempData["error"] = ex.Message;

                        return RedirectToAction(
                            nameof(Details),
                            new { orderId = orderHeader.Id }
                        );
                    }
                    catch (Stripe.StripeException)
                    {
                        // Do not claim the order was cancelled when the refund fails.
                        TempData["error"] =
                            "The refund could not be processed. The order status was not changed. Please check Stripe Dashboard.";

                        return RedirectToAction(
                            nameof(Details),
                            new { orderId = orderHeader.Id }
                        );
                    }

                    break;

                default:

                    TempData["error"] = "Invalid status provided.";

                    return RedirectToAction(
                        nameof(Details),
                        new { orderId = orderHeader.Id }
                    );
            }

            TempData["success"] = successMessage;

            return RedirectToAction(
                nameof(Details),
                new { orderId = orderHeader.Id }
            );
        }

        #region API CALLS

        [HttpGet]
        public async Task<IActionResult> Getall(string status)
        {
            string? userId = null;

            var isStaff =
                User.IsInRole(SD.RoleAdmin) ||
                User.IsInRole(SD.RoleEmployee);

            if (!isStaff)
            {
                userId = User.FindFirstValue(
                    ClaimTypes.NameIdentifier
                );

                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }
            }

            var orders = await _orderService.GetAllOrderAsync(
                userId,
                status
            );

            return Json(new { data = orders });
        }

        #endregion
    }
}
