using BulkyBook.Models.ViewModels;
using BulkyBook.Utility;
using BulkyBookWeb.DataAccess.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BulkyBookWeb.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = SD.RoleAdmin + "," + SD.RoleEmployee)]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _db;
        // Orders that count as revenue (paid): Approved, Processing, Shipped.
        private static readonly string[] Paid = { SD.StatusApproved, SD.StatusInProcess, SD.StatusShipped };

        public DashboardController(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index()
        {
            var orders = _db.OrderHeaders.AsNoTracking();
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            async Task<double> Revenue(DateTime from, DateTime to) =>
                await orders.Where(o => Paid.Contains(o.OrderStatus!) && o.OrderDate >= from && o.OrderDate < to).SumAsync(o => o.OrderTotal);
            async Task<int> Count(DateTime from, DateTime to) =>
                await orders.CountAsync(o => o.OrderDate >= from && o.OrderDate < to);

            var vm = new DaschBoardVM
            {
                TotalOrders = await orders.CountAsync(),
                TotalProducts = await _db.Products.CountAsync(),
                TotalUsers = await _db.Users.CountAsync(),
                TotalRevenue = await orders.Where(o => Paid.Contains(o.OrderStatus!)).SumAsync(o => o.OrderTotal),

                TodayRevenue = await Revenue(today, tomorrow),
                YesterdayRevenue = await Revenue(today.AddDays(-1), today),
                Last7Revenue = await Revenue(today.AddDays(-6), tomorrow),
                Prev7Revenue = await Revenue(today.AddDays(-13), today.AddDays(-6)),
                OrdersToday = await Count(today, tomorrow),
                OrdersYesterday = await Count(today.AddDays(-1), today),
                AwaitingPack = await orders.CountAsync(o => o.OrderStatus == SD.StatusApproved),
                PendingPayment = await orders.CountAsync(o => o.OrderStatus == SD.StatusPending)
            };

            vm.PackingQueue = await orders
                .Where(o => o.OrderStatus == SD.StatusApproved)
                .OrderBy(o => o.OrderDate).Take(5)
                .Select(o => new DashOrderVM { Id = o.Id, Name = o.Name, Total = o.OrderTotal, Status = o.OrderStatus!, Date = o.OrderDate })
                .ToListAsync();

            vm.RecentOrders = await orders
                .OrderByDescending(o => o.OrderDate).Take(6)
                .Select(o => new DashOrderVM { Id = o.Id, Name = o.Name, Total = o.OrderTotal, Status = o.OrderStatus ?? "Unknown", Date = o.OrderDate })
                .ToListAsync();

            var top = await _db.OrderDetails
                .Where(d => Paid.Contains(d.OrderHeader.OrderStatus!))
                .GroupBy(d => d.ProductId)
                .Select(g => new { ProductId = g.Key, Units = g.Sum(x => x.Count), Revenue = g.Sum(x => x.Count * x.Price) })
                .OrderByDescending(x => x.Units).Take(5)
                .ToListAsync();
            var ids = top.Select(t => t.ProductId).ToList();
            var products = await _db.Products.AsNoTracking().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
            vm.TopProducts = top.Where(t => products.ContainsKey(t.ProductId)).Select(t => new DashTopProductVM
            {
                Title = products[t.ProductId].Title,
                Author = products[t.ProductId].Author,
                Units = t.Units,
                Revenue = t.Revenue
            }).ToList();

            ViewData["Greeting"] = true;
            return View(vm);
        }

        /// <summary>Revenue series for the "Sales studio" chart: current period vs the period before it.</summary>
        public async Task<IActionResult> GetSalesSeries(string range = "30d")
        {
            var monthly = range is "6m" or "12m";
            var n = range switch { "7d" => 7, "6m" => 6, "12m" => 12, _ => 30 };

            var today = DateTime.Today;
            DateTime firstCurrent, firstPrev;
            if (monthly)
            {
                var thisMonth = new DateTime(today.Year, today.Month, 1);
                firstCurrent = thisMonth.AddMonths(-(n - 1));
                firstPrev = firstCurrent.AddMonths(-n);
            }
            else
            {
                firstCurrent = today.AddDays(-(n - 1));
                firstPrev = firstCurrent.AddDays(-n);
            }

            var rows = await _db.OrderHeaders.AsNoTracking()
                .Where(o => Paid.Contains(o.OrderStatus!) && o.OrderDate >= firstPrev)
                .Select(o => new { o.OrderDate, o.OrderTotal })
                .ToListAsync();

            var labels = new List<string>();
            var current = new List<double>();
            var previous = new List<double>();
            for (var i = 0; i < n; i++)
            {
                if (monthly)
                {
                    var m = firstCurrent.AddMonths(i);
                    var pm = m.AddMonths(-n);
                    labels.Add(m.ToString("MMM yy"));
                    current.Add(rows.Where(r => r.OrderDate.Year == m.Year && r.OrderDate.Month == m.Month).Sum(r => r.OrderTotal));
                    previous.Add(rows.Where(r => r.OrderDate.Year == pm.Year && r.OrderDate.Month == pm.Month).Sum(r => r.OrderTotal));
                }
                else
                {
                    var d = firstCurrent.AddDays(i);
                    var pd = d.AddDays(-n);
                    labels.Add(d.ToString("MMM d"));
                    current.Add(rows.Where(r => r.OrderDate.Date == d).Sum(r => r.OrderTotal));
                    previous.Add(rows.Where(r => r.OrderDate.Date == pd).Sum(r => r.OrderTotal));
                }
            }

            var total = current.Sum();
            var prevTotal = previous.Sum();
            return Json(new { labels, current, previous, total, previousTotal = prevTotal, deltaPct = DaschBoardVM.Pct(total, prevTotal) });
        }

        public async Task<IActionResult> GetChartData()
        {
            var orders = await _db.OrderHeaders.AsNoTracking().ToListAsync();
            var products = await _db.Products.AsNoTracking().ToListAsync();
            var categories = await _db.Categories.AsNoTracking().ToListAsync();

            var statusBreakdown = orders
                .GroupBy(o => o.OrderStatus ?? "Unknown")
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToList();

            var productsPerCategory = categories.Select(c => new
            {
                Category = c.Name,
                Count = products.Count(p => p.CategoryId == c.Id)
            }).OrderByDescending(x => x.Count).ToList();

            return Json(new { statusBreakdown, productsPerCategory });
        }
    }
}
