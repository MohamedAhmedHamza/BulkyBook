# BulkyBook – Aurora redesign (v3)
Copy these files over the project (same paths). Nothing here touches your own edits
(OrderController, OrderService, Product/Upsert.cshtml, Program.cs, csproj, appsettings*).
NOT compiled (no .NET SDK in my environment) - send me any build error.

## New files
- wwwroot/css/aurora.css   design system (dark/light glass, aurora glow, motion, bootstrap + DataTables + SweetAlert + toastr skin)
- wwwroot/css/admin.css    admin shell, KPI strip, panels, orders board
- wwwroot/js/aurora.js     theme toggle, collapsible sidebar, reveal-on-scroll, count-up, spotlight
- wwwroot/js/orders-board.js   kanban board (reads /admin/order/Getall?status=all)

## Replaced files
- Views/Shared/_AdminLayout.cshtml, _AdminSidebarNav.cshtml   (new glass sidebar, role-aware, "to pack" badge, search, Ctrl+K, collapse to icon rail)
- Views/Shared/_Layout.cshtml, _LoginPartial.cshtml          (floating glass navbar, shared theme toggle)
- wwwroot/css/theme.css  (store components re-skinned on Aurora tokens)
- wwwroot/css/site.css   (ONLY change: old "Dark Theme Overrides" block removed - it fought the new theme)
- Areas/Admin/Views/Dashboard/Index.cshtml + wwwroot/js/dashboard.js + DashboardController.cs + BulkyBook.Models/ViewModels/DaschBoardVM.cs
- Areas/Admin/Views/Order/Index.cshtml + wwwroot/js/orders.js     (Board / Table switch, status pills)
- Areas/Customer/Views/Home/_Hero.cshtml
- Areas/Customer/Controllers/CartController.cs  (one line: OrderDate = DateTime.Now, same clock as the rest of your data)

## Behaviour notes
- Revenue now counts Approved + Processing + Shipped (before: Approved + Shipped only).
- Orders board is read-only (cards open the order page). Status changes still go through your OrderController.
- Users link in the sidebar is shown to Admin only.
- All other admin pages (Products, Categories, Users, Order details, Upsert) are restyled by CSS only - markup untouched.
