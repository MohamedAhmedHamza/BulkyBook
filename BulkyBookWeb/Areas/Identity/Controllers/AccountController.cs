using BulkyBook.Business.Services.IServices;
using BulkyBook.Models;
using BulkyBook.Models.ViewModels;
using BulkyBook.Utility;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace BulkyBookWeb.Areas.Identity.Controllers
{
    [Area("Identity")]
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IShoppingCartService _shoppingCartService;
        public AccountController(UserManager<ApplicationUser> userManager , SignInManager<ApplicationUser> signInManager, RoleManager<IdentityRole> roleManager, IShoppingCartService shoppingCartService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _shoppingCartService = shoppingCartService;
        }

        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginVM());
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginVM loginVM, string? returnUrl = null)
        {
            if (ModelState.IsValid)
            {

                var result = await _signInManager.PasswordSignInAsync(loginVM.Email , loginVM.Password 
                        , loginVM.RememberMe, lockoutOnFailure: false);
                if (result.Succeeded)
                {

                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                        return Redirect(returnUrl);
                    }
                    var user = await _userManager.FindByEmailAsync(loginVM.Email);

                    if (user != null &&
                        (await _userManager.IsInRoleAsync(user, SD.RoleAdmin) ||
                         await _userManager.IsInRoleAsync(user, SD.RoleEmployee)))
                    {
                        return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
                    }
                    return RedirectToAction("Index", "Home", new { area = "Customer" });    
                }
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");       
            }
            return View(loginVM);
        }
        public IActionResult Register(string? returnUrl = null)
        {
            var model = new RegiesterVM();
            if (User.IsInRole(SD.RoleAdmin))
            {


                model.RoleList =
                    [
                        new SelectListItem { Text =SD.RoleCustomer, Value = SD.RoleCustomer },
                    new SelectListItem { Text = SD.RoleAdmin, Value = SD.RoleAdmin },
                    new SelectListItem { Text = SD.RoleEmployee, Value = SD.RoleEmployee }
                    ];
            }
            else
            {
                model.RoleList = new List<SelectListItem>();
            }
            ViewData["ReturnUrl"] = returnUrl;
            return View(model);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegiesterVM regiesterVM, string? returnUrl = null)
        {
           
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser
                {
                    UserName = regiesterVM.Email,
                    Email = regiesterVM.Email,
                    Name = regiesterVM.Name,
                    StreetAddress = regiesterVM.StreetAddress,
                    City = regiesterVM.City,
                    State = regiesterVM.State,
                    PostalCode = regiesterVM.PostalCode,
                    PhoneNumber = regiesterVM.PhoneNumber
                };
                    var result = await _userManager.CreateAsync(user, regiesterVM.Password);
                    if (result.Succeeded)
                    {
                        if(User.IsInRole(SD.RoleAdmin) && !string.IsNullOrEmpty(regiesterVM.Role))
                        {
                            await _userManager.AddToRoleAsync(user, regiesterVM.Role);
                            TempData["success"] = $"User '{user.UserName}' create successffully with role '{regiesterVM.Role}'.";
                            return RedirectToAction("Index","User", new {area = "Admin"});
                        }
                        else
                        {
                            await _userManager.AddToRoleAsync(user, SD.RoleCustomer);
                            await _signInManager.SignInAsync(user, isPersistent: false);
                            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                            {
                                return Redirect(returnUrl);
                            }
                            return RedirectToAction("Index", "Home", new { area = "Customer" });
                        }
                       
                    }
                        foreach (var error in result.Errors)
                        {
                            ModelState.AddModelError(string.Empty, error.Description);
                        }


            }
            return View(regiesterVM);

        }
        public IActionResult AccessDenied()
        {
            return View();
        }
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            HttpContext.Session.SetInt32(SD.SessionCart, 0);
            return RedirectToAction("Index", "Home", new { area = "Customer" });
        }
    }
}
