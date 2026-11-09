using BulkyBook.Business.Services.IServices;
using BulkyBook.Models;
using BulkyBook.Models.ViewModels;
using BulkyBook.Utility;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using BulkyBookWeb.DataAccess.Data;
using BulkyBookWeb.Security;
using System.Net;

namespace BulkyBookWeb.Areas.Identity.Controllers
{
    [Area("Identity")]
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IShoppingCartService _shoppingCartService;
        private readonly IEmailService _emailService;
        private readonly PasswordResetOtpManager _otp;
        public AccountController(UserManager<ApplicationUser> userManager , SignInManager<ApplicationUser> signInManager, RoleManager<IdentityRole> roleManager, IShoppingCartService shoppingCartService, IEmailService emailService, ApplicationDbContext db, IConfiguration config)
        {
            _emailService = emailService;
            _otp = new PasswordResetOtpManager(db, config);
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
                        , loginVM.RememberMe, lockoutOnFailure: true);
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
                if (result.IsLockedOut)
                {
                    ModelState.AddModelError(string.Empty, "Too many failed attempts. Please try again in a few minutes.");
                    return View(loginVM);
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
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            HttpContext.Session.SetInt32(SD.SessionCart, 0);
            return RedirectToAction("Index", "Home", new { area = "Customer" });
        }

        // ---------------------------------------------------------------- forgot / reset password
        public IActionResult ForgotPassword() => View(new ForgotPasswordVM());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordVM model)
        {
            if (!ModelState.IsValid) return View(model);

            await SendOtpAsync(model.Email);
            // Same answer whether or not the e-mail exists (no account enumeration).
            return RedirectToAction(nameof(VerifyOtp), new { email = model.Email });
        }

        /// <summary>Issues a NEW code (the previous one stops working) and e-mails it.</summary>
        private async Task SendOtpAsync(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null) return;

            var code = await _otp.IssueAsync(user);
            if (code == null) return;   // hourly limit reached: silently ignore

            var minutes = (int)PasswordResetOtpManager.Lifetime.TotalMinutes;
            var body =
                "<div style=\"font-family:Segoe UI,Arial,sans-serif;max-width:480px;margin:auto;padding:28px;border-radius:18px;background:#0f1428;color:#e9ecfa\">" +
                $"<h2 style=\"margin:0 0 6px\">Reset your password</h2>" +
                $"<p style=\"color:#98a2c3\">Hello {WebUtility.HtmlEncode(user.Name)}, use this code to reset your BulkyBook password:</p>" +
                $"<div style=\"font-size:36px;letter-spacing:10px;font-weight:800;text-align:center;padding:18px;margin:18px 0;border-radius:14px;background:#1a2142;color:#8fb4ff\">{code}</div>" +
                $"<p style=\"color:#98a2c3;font-size:14px\">It expires in {minutes} minutes and works once. If you ask for another code, this one stops working. " +
                "If you did not request it, you can ignore this e-mail.</p></div>";
            await _emailService.SendEmailAsync(user.Email!, "Your BulkyBook verification code", body);
        }

        public IActionResult VerifyOtp(string? email)
        {
            if (string.IsNullOrWhiteSpace(email)) return RedirectToAction(nameof(ForgotPassword));
            return View(new VerifyOtpVM { Email = email });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyOtp(VerifyOtpVM model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            var result = user == null ? OtpResult.Invalid : await _otp.VerifyAsync(user, model.Code);

            if (result == OtpResult.Ok && user != null)
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                return RedirectToAction(nameof(ResetPassword), new { email = user.Email, token });
            }

            ModelState.Clear();
            ModelState.AddModelError(string.Empty, result switch
            {
                OtpResult.Expired => "This code has expired. Request a new one.",
                OtpResult.TooManyAttempts => "Too many wrong attempts. Request a new code.",
                _ => "That code is not correct or is no longer valid."
            });
            model.Code = string.Empty;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendOtp(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return RedirectToAction(nameof(ForgotPassword));
            await SendOtpAsync(email);
            TempData["success"] = "A new code was sent. Older codes no longer work.";
            return RedirectToAction(nameof(VerifyOtp), new { email });
        }

        public IActionResult ForgotPasswordConfirmation() => View();

        public IActionResult ResetPassword(string? email, string? token)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
            {
                return RedirectToAction(nameof(ForgotPassword));
            }
            return View(new ResetPasswordTokenVM { Email = email, Token = token });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordTokenVM model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                return RedirectToAction(nameof(ResetPasswordConfirmation));
            }
            var result = await _userManager.ResetPasswordAsync(user, model.Token, model.Password);
            if (result.Succeeded)
            {
                await _otp.InvalidateAsync(user);
                return RedirectToAction(nameof(ResetPasswordConfirmation));
            }
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        public IActionResult ResetPasswordConfirmation() => View();
    }
}
