using BulkyBook.Business.Services.IServices;
using BulkyBook.Models;
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
        public HomeController(IProductService productService,IShoppingCartService shoppingCartService)
        {
            _productService = productService;
            _shoppingCartService = shoppingCartService;
        }

        public async Task<IActionResult> Index()
        {
            var products = await _productService.GetAllProductAsync(includeCategory: true);
            return View(products);
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
        public async Task<IActionResult> Details(ShoppingCart shoppingCart)
        {
            var calimIdentity = (ClaimsIdentity)User.Identity;
            var userId = calimIdentity?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }
            shoppingCart.ApplicationUserId = userId;
            await _shoppingCartService.AddToCartAsync(shoppingCart);

            var Count = await _shoppingCartService.GetCartCountAsync(userId);
            HttpContext.Session.SetInt32(SD.SessionCart, Count);
            TempData["success"] = "Item added to cart successfully";    
            /*return RedirectToAction(shoppingCart.ProductId == 0 ? "Index" : "Details", new { productId = shoppingCart.ProductId });*/
            return RedirectToAction("Index");
        }
      
    }
}
