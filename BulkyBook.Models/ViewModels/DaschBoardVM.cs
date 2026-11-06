using System;
using System.Collections.Generic;

namespace BulkyBook.Models.ViewModels
{
    public class DaschBoardVM
    {
        // --- original totals (kept) ---
        public int TotalUsers { get; set; }
        public int TotalOrders { get; set; }
        public int TotalProducts { get; set; }
        public double TotalRevenue { get; set; }

        // --- KPI strip ---
        public double TodayRevenue { get; set; }
        public double YesterdayRevenue { get; set; }
        public double Last7Revenue { get; set; }
        public double Prev7Revenue { get; set; }
        public int OrdersToday { get; set; }
        public int OrdersYesterday { get; set; }
        public int AwaitingPack { get; set; }      // paid (Approved) and waiting to be packed
        public int PendingPayment { get; set; }

        // --- panels ---
        public List<DashOrderVM> PackingQueue { get; set; } = new();
        public List<DashOrderVM> RecentOrders { get; set; } = new();
        public List<DashTopProductVM> TopProducts { get; set; } = new();

        public static double? Pct(double current, double previous)
        {
            if (previous <= 0) return current > 0 ? 100 : (double?)null;
            return Math.Round((current - previous) / previous * 100, 1);
        }
        public double? RevenueDelta => Pct(Last7Revenue, Prev7Revenue);
        public double? TodayDelta => Pct(TodayRevenue, YesterdayRevenue);
        public double? OrdersDelta => Pct(OrdersToday, OrdersYesterday);
    }

    public class DashOrderVM
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public double Total { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime Date { get; set; }
    }

    public class DashTopProductVM
    {
        public string Title { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public int Units { get; set; }
        public double Revenue { get; set; }
    }
}
