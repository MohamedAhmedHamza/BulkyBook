using BulkyBook.Business.Services.IServices;
using BulkyBook.Models;
using BulkyBook.Models.ViewModels;
using BulkyBook.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BulkyBookWeb.Areas.Customer.Controllers
{
    [Area("Customer")]
    public class HomeController : Controller
    {
        private readonly IProductService _productService;
        private readonly IShoppingCartService _shoppingCartService;
        private readonly ICategoryService _categoryService;
        public HomeController(IProductService productService, IShoppingCartService shoppingCartService, ICategoryService categoryService)
        {
            _productService = productService;
            _shoppingCartService = shoppingCartService;
            _categoryService = categoryService;
        }

        public async Task<IActionResult> Index(string? search, int? categoryId, string? sort, int page = 1)
        {
            const int pageSize = 12;
            sort = string.IsNullOrWhiteSpace(sort) ? "newest" : sort;

            var (items, total) = await _productService.SearchAsync(search, categoryId, sort, page, pageSize);
            var categories = (await _categoryService.GetAllCategoryAsync()).OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name).ToList();

            var vm = new ProductListVM
            {
                Products = items,
                Categories = categories,
                Search = search,
                CategoryId = categoryId,
                Sort = sort,
                Page = Math.Max(1, page),
                PageSize = pageSize,
                TotalCount = total,
                // unfiltered total for the hero stats
                TotalBooks = (search == null && categoryId == null) ? total : (await _productService.SearchAsync(null, null, null, 1, 1)).TotalCount
            };
            return View(vm);
        }
        public async Task<IActionResult> Details(int productId)
        {
            var product = await _productService.GetProductByIdAsync(productId, includeCategory: true);
            if (product == null)
            {
                return NotFound();
            }
            ShoppingCart cart = new()
            {
                ProductId = product.Id,
                Count = 1,
                Product = product
            };

            return View(cart);
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Details(ShoppingCart shoppingCart)
        {
            var calimIdentity = (ClaimsIdentity)User.Identity;
            var userId = calimIdentity?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }
            var product = await _productService.GetProductByIdAsync(shoppingCart.ProductId);
            if (product == null)
            {
                return NotFound();
            }
            shoppingCart.Id = 0;
            shoppingCart.Count = Math.Clamp(shoppingCart.Count, 1, 1000);
            shoppingCart.Product = null!;
            shoppingCart.ApplicationUserId = userId;
            await _shoppingCartService.AddToCartAsync(shoppingCart);

            var Count = await _shoppingCartService.GetCartCountAsync(userId);
            HttpContext.Session.SetInt32(SD.SessionCart, Count);
            TempData["success"] = "Added to your cart";
            /*return RedirectToAction(shoppingCart.ProductId == 0 ? "Index" : "Details", new { productId = shoppingCart.ProductId });*/
            return RedirectToAction("Index");
        }
      

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error(int? code = null)
        {
            var status = code is >= 400 and < 600 ? code.Value : 500;
            Response.StatusCode = status;
            ViewBag.StatusCode = status;
            ViewBag.RequestId = HttpContext.TraceIdentifier;
            return View();
        }
    }
}
