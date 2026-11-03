using BulkyBook.Business.Services;
using BulkyBook.Business.Services.IServices;
using BulkyBook.Models;
using BulkyBook.Models.ViewModels;
using BulkyBook.Utility;
using BulkyBookWeb.DataAccess.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BulkyBookWeb.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = SD.RoleAdmin)]
    public class ProductController : Controller
    {
        private readonly IProductService _ProductService;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ICategoryService _categoryService;
        public ProductController(IProductService ProductService, ICategoryService categoryService, IWebHostEnvironment webHostEnvironment)
        {
            _ProductService = ProductService;
            _categoryService = categoryService;
            _webHostEnvironment = webHostEnvironment;
        }
        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
            var product = await _ProductService.GetAllProductAsync();
            return View(product);
        }
      
        public async Task<IActionResult> Upsert(int? id)
        {
          var categories = await _categoryService.GetAllCategoryAsync();

            ProductVM productVM = new()
            {
                CategoryList = categories.Select(c => new SelectListItem
                {
                    Text = c.Name,
                    Value = c.Id.ToString()
                }),
                Product = new Product()
            };
            if (id == null || id == 0)
            {
                //create
                return View(productVM);
            }
            else
            {
                //update
                productVM.Product = await _ProductService.GetProductByIdAsync(id.Value);
                return View(productVM);
            }
        }
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("Upsert")]
        public async Task<IActionResult> UpsertPost(ProductVM productVM, IFormFile? file)
        {
            if (ModelState.IsValid)
            {
                string wwwRootPath = _webHostEnvironment.WebRootPath;
                if (file != null)
                {
                    string fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                    string productPath = Path.Combine("images", "products");
                    string finalPath = Path.Combine(wwwRootPath, productPath);

                    if (!Directory.Exists(finalPath))
                         Directory.CreateDirectory(finalPath);

                    //save the new image
                    using(var fileStream = new FileStream(Path.Combine(finalPath, fileName), FileMode.Create))
                    {
                        await file.CopyToAsync(fileStream);
                    }
                    productVM.Product.ImageUrl = Path.Combine(productPath, fileName).Replace("\\", "/");

                }
                if (productVM.Product.Id == null || productVM.Product.Id == 0)
                {
                    //create
                    await _ProductService.CreateProductAsync(productVM.Product);
                }
                else
                {
                    //update
                    await _ProductService.UpdateProductAsync(productVM.Product);
                }



                TempData["success"] = "Product created successfully!";
                return RedirectToAction("Index");
            }
            else {
                    var categories = await _categoryService.GetAllCategoryAsync();

                    productVM = new ProductVM()
                    {
                        CategoryList = categories.Select(c => new SelectListItem
                        {
                            Text = c.Name,
                            Value = c.Id.ToString()
                        }),
                    };
                    return View(productVM);
            }
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
        [ActionName("Delete")]
        public  async Task<IActionResult> Delete(int? id)
        {
            if (id == null || id == 0)
            {
                return Json(new { success = false, message = "Invalid ID" });
            }

            var productToBeDeleted = await _ProductService.GetProductByIdAsync(id.Value);
            if (productToBeDeleted == null)
            {
                return Json(new { success = false, message = "Error while deleting" });
            }

            //delete product image if that exists
            if (!string.IsNullOrEmpty(productToBeDeleted.ImageUrl))
            {
                var imagePath = Path.Combine(_webHostEnvironment.WebRootPath, productToBeDeleted.ImageUrl.TrimStart('\\','/'));
                if (System.IO.File.Exists(imagePath))
                {
                    System.IO.File.Exists(imagePath); 
                }
            }

            await _ProductService.DeleteProductAsync(id.Value);
            return Json(new { success = true, message = "Delete Successful" });

        }

        #region API CALLS
        [AllowAnonymous]
        public async Task<IActionResult> Getall()
        {
            var product = await _ProductService.GetAllProductAsync(true);
            return Json(new { data = product });
        }
        #endregion

    }
}
