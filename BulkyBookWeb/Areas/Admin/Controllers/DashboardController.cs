using BulkyBook.Models;
using BulkyBook.Models.ViewModels;
    using BulkyBook.Utility;
    using BulkyBookWeb.DataAccess.Data;
using Mailjet.Client.Resources.SMS;
using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using System.Reflection.Emit;

    namespace BulkyBookWeb.Areas.Admin.Controllers
    {
        [Area("Admin")]
        public class DashboardController : Controller
        {
            private readonly ApplicationDbContext _db;
            public DashboardController(ApplicationDbContext db)
            {
                _db = db;
            }
            public async Task<IActionResult> Index()
            {
                var orders = await _db.OrderHeaders.ToListAsync();
                var ProductCount = await _db.Products.CountAsync();
                var usersCount = await _db.Users.CountAsync();

                DaschBoardVM daschBoardVM = new DaschBoardVM()
                {
                    TotalOrders = orders.Count(),
                    TotalProducts = ProductCount,
                    TotalUsers = usersCount,
                    TotalRevenue = orders.Where(o => o.OrderStatus == SD.StatusApproved || o.OrderStatus == SD.StatusShipped).Sum(o => o.OrderTotal)
                };

                return View(daschBoardVM);
            }

            public async Task<IActionResult> GetChartData()
            {
                var orders = await _db.OrderHeaders.ToListAsync();
                var ProductCount = await _db.Products.Include(c => c.Category).ToListAsync();
                var categories = await _db.Categories.ToListAsync();

                var now = DateTime.Now;
                var sixMonthsAgo = now.AddMonths(-5);
                var monthlyRevenue  = Enumerable.Range(0,6).Select (i =>
                {
                    var month = sixMonthsAgo.AddMonths(i);
                    var revenue = orders.Where(o => o.OrderDate.Year == month.Year && o.OrderDate.Month == month.Month
                    && (o.OrderStatus == SD.StatusApproved || o.OrderStatus == SD.StatusShipped)).Sum(o => o.OrderTotal);

                    return new { Label = month.ToString("MMM yyy"), Revenue = revenue };   
                }).ToList();


                var monthlyOrders = Enumerable.Range(0, 6).Select(i =>
                {
                    var month = sixMonthsAgo.AddMonths(i);
                    var count = orders.Count(o => o.OrderDate.Year == month.Year && o.OrderDate.Month == month.Month);

                    return new { Label = month.ToString("MMM yyy"), Count = count};
                }).ToList();

                var statusBreakdown = orders
                 .GroupBy(o => o.OrderStatus ?? "UnKnown")
                 .Select(g => new { Status = g.Key, Count = g.Count() })
                 .ToList(); 
                var productsPerCategory = categories.Select(c => new
                {
                    Category = c.Name,
                    Count = ProductCount.Count(p => p.CategoryId == c.Id)
                }).ToList();
            

            return Json(new
                {
                    monthlyRevenue,
                    statusBreakdown,
                    monthlyOrders,
                    productsPerCategory
                });
            }
        }
    }
