    using BulkyBook.Business.Services;
    using BulkyBook.Business.Services.IServices;
    using BulkyBook.Models;
    using BulkyBook.Models.ViewModels;
    using BulkyBook.Utility;
    using BulkyBookWeb.DataAccess.Data;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Rendering;
    using Stripe;
    using System.Security.Claims;

    namespace BulkyBookWeb.Areas.Admin.Controllers
    {
        [Area("Admin")]
        [Authorize(Roles = SD.RoleAdmin)]
        public class OrderController : Controller
        {
            private readonly IOrderService _orderService;


            [BindProperty]
            public OrderHeader OrderHeader { get; set; }
            public OrderController(IOrderService orderService)
            {
                   _orderService = orderService;
            }
            [AllowAnonymous]
            public async Task<IActionResult> Index()
            {
                var orders = await _orderService.GetAllOrderAsync();
                return View(orders);
            }
            [AllowAnonymous]
            public async Task<IActionResult> Details(int orderId )
            {
                OrderHeader = await _orderService.GetOrderByIdAsync(orderId, includDetail : true, includeUser: true);
                return View(OrderHeader);
            }
            [HttpPost]
            [Authorize(Roles = SD.RoleAdmin+ "," + SD.RoleEmployee)]

        [HttpPost]
        [Authorize(Roles = SD.RoleAdmin + "," + SD.RoleEmployee)]
        public async Task<IActionResult> UpdateOrderDetails(OrderHeader orderHeader)
        {
            var orderHeaderFromDb = await _orderService.GetOrderByIdAsync(orderHeader.Id);

            if (orderHeaderFromDb == null)
            {
                TempData["error"] = "Order not found!";
                return RedirectToAction(nameof(Index));
            }

            orderHeaderFromDb.Name = orderHeader.Name;
            orderHeaderFromDb.PhoneNumber = orderHeader.PhoneNumber;
            orderHeaderFromDb.StreetAddress = orderHeader.StreetAddress;
            orderHeaderFromDb.City = orderHeader.City;
            orderHeaderFromDb.State = orderHeader.State;
            orderHeaderFromDb.PostalCode = orderHeader.PostalCode;

            if (!(string.IsNullOrEmpty(orderHeader.Carrier)
                && orderHeaderFromDb.OrderStatus == SD.StatusShipped))
            {
                orderHeaderFromDb.Carrier = orderHeader.Carrier;
            }

            if (!(string.IsNullOrEmpty(orderHeader.TrackingNumber)
                && orderHeaderFromDb.OrderStatus == SD.StatusShipped))
            {
                orderHeaderFromDb.TrackingNumber = orderHeader.TrackingNumber;
            }

            await _orderService.UpdateOrderAsync(orderHeaderFromDb);

            TempData["success"] = "Order details updated successfully!";

            return RedirectToAction(nameof(Details),
                new { orderId = orderHeaderFromDb.Id });
        }

        [HttpPost]
            [Authorize(Roles = SD.RoleAdmin + "," + SD.RoleEmployee)]
            public async Task<IActionResult> UpdateOrderStatus(string status)
            {
                var orderHeader = await _orderService.GetOrderByIdAsync(OrderHeader.Id);
                if (orderHeader == null)
                {
                    TempData["error"] = "Order not found!";
                    return RedirectToAction("Index");
                }
                string successMessage;
                switch (status)
                {
                    case SD.StatusInProcess:
                        await _orderService.UpdateOrderStatusAsync(orderHeader.Id, status);
                        successMessage = "Order processing started successfully.";
                        break;
                    case SD.StatusShipped:
                        if (string.IsNullOrEmpty(OrderHeader.Carrier) || string.IsNullOrEmpty(OrderHeader.TrackingNumber))
                        {
                            TempData["error"] = "Carrier and Tracking Number must be provided before shipping the order.";
                            return RedirectToAction("Details", "Order", new { orderId = orderHeader.Id });
                        }
                        await _orderService.UpdateOrderStatusAsync(orderHeader.Id,SD.StatusShipped,OrderHeader.Carrier ,OrderHeader.TrackingNumber);
                        successMessage = "Order shipped successfully.";
                        break;
                    case SD.StatusCancelled:
                    case SD.StatusRefunded:
                        try
                        {
                            bool refundIssued = await _orderService.CancelOrderWithRefundAsync(OrderHeader.Id);
                            if (refundIssued)
                            {
                                successMessage = "Order cancelled and refund issued successfully. Funds will be returns to customer within 5-10 business";
                            }
                            else
                            {
                                successMessage = "Order cancelled successfully.(No paymeny was processed)";
                            }
                        }
                        catch (InvalidOperationException ex)
                        {
                            TempData["error"] = ex.Message;
                            return RedirectToAction(nameof(Details),new {order = orderHeader.Id});
                        }
                        catch(StripeException ex)
                        {
                            TempData["error"] = $"Order cancelled but refund faild:{ex.Message}.Please refund manually in Stripe Dashboard";
                            return RedirectToAction(nameof(Details), new { order = orderHeader.Id });

                        }
                        break;
                    default:
                        TempData["error"] = "Invalid status provided.";
                        return RedirectToAction("Details", "Order", new { orderId = orderHeader.Id });
                }
                TempData["success"] = successMessage;
                return RedirectToAction( "Details", "Order", new { orderId = orderHeader.Id }
    );
            }
            /*public async Task<IActionResult> Update(int? id)
            {
                if (id == null || id == 0)
                {
                    return NotFound();
                }
                var Product =  await _ProductService.GetProductByIdAsync(id.Value);
                if (Product == null)
                {
                    return NotFound();
                }
                return View(Product);
            }
            [HttpPost]
            [ValidateAntiForgeryToken]
            [ActionName("Update")]
            public async Task<IActionResult> UpdatePost(Product Product)
            {
            
                if (ModelState.IsValid)
                {
                    await _ProductService.UpdateProductAsync(Product);
                    TempData["success"] = "Product updated successfully!";
                    return RedirectToAction("Index");
                }
                return View(Product);
            }
            public async Task<IActionResult> Delete (int? id)
            {
                if (id == null || id == 0)
                {
                    return NotFound();
                }
                var Product = await _ProductService.GetProductByIdAsync(id.Value);
                if (Product == null)
                {
                    return NotFound();
                }
                return View(Product);
            }*/


            #region API CALLS
            [AllowAnonymous]
            public async Task<IActionResult> Getall(string status)
            {
                string? userId = null;

                if(!User.IsInRole(SD.RoleAdmin) && !User.IsInRole(SD.RoleEmployee))
                {
                    var calimIdentity = (ClaimsIdentity)User.Identity;
                    userId = calimIdentity?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    if(string.IsNullOrEmpty(userId))
                    {
                        return Unauthorized();
                    }
                }
                var orders= await _orderService.GetAllOrderAsync(userId,status);
                return Json(new { data = orders });
            }
            #endregion

        }
    }
