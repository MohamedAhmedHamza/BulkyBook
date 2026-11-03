using BulkyBook.Business.Services;
using BulkyBook.Business.Services.IServices;
using BulkyBook.Models;
using BulkyBook.Models.ViewModels;
using BulkyBook.Utility;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Security.Claims;

namespace BulkyBookWeb.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = SD.RoleAdmin + "," + SD.RoleEmployee)]
    public class UserController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IApplicationUserService _userService;

        public UserController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            SignInManager<ApplicationUser> signInManager,
            IApplicationUserService userService)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _signInManager = signInManager;
            _userService = userService;
        }

        // =========================
        // Users
        // =========================

        public async Task<IActionResult> Index()
        {
            return View();
        }


        // =========================
        // Lock
        // =========================

        /*[HttpPost]
        public async Task<IActionResult> Lock(string id)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                TempData["error"] = "User not found!";
                return RedirectToAction("Index");
            }

            await _userManager.SetLockoutEndDateAsync(
                user,
                DateTimeOffset.UtcNow.AddYears(100)
            );

            TempData["success"] = "User locked successfully.";

            return RedirectToAction("Index");
        }


        // =========================
        // Unlock
        // =========================

        [HttpPost]
        public async Task<IActionResult> Unlock(string id)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                TempData["error"] = "User not found!";
                return RedirectToAction("Index");
            }

            await _userManager.SetLockoutEndDateAsync(
                user,
                null
            );

            TempData["success"] = "User unlocked successfully.";

            return RedirectToAction("Index");
        }
*/
            
        // =========================
        // Role Page
        // =========================

        [HttpGet]
        public async Task<IActionResult> Role(string id)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                return Json(new { success = false, message = "User not found!" });
            }
            RoleManegmentVM RoleVM = new ()
            {
                User = user,
                RoleList = _roleManager.Roles.Select(role => new SelectListItem
                {
                    Text = role.Name,
                    Value = role.Name
                })
            };
            RoleVM.User.Role = (await _userManager.GetRolesAsync(user)).FirstOrDefault();
            return View(RoleVM);
        }


        // =========================
        // Update Role
        // =========================

        [HttpPost]
        public async Task<IActionResult> Role(RoleManegmentVM roleManegmentVM)
        {
            var user = await _userManager.FindByIdAsync(roleManegmentVM.User.Id);

            if (user == null)
            {
                return NotFound();  
            }

            var currentRoles = await _userManager.GetRolesAsync(user);

            foreach (var role in currentRoles)
            {
                await _userManager.RemoveFromRoleAsync(user, role);
            }

            await _userManager.AddToRoleAsync(user, roleManegmentVM.User.Role);

            TempData["success"] = "User role updated successfully.";

            return RedirectToAction("Index");
        }


        // =========================
        // Reset Password Page
        // =========================

        [HttpGet]
        public async Task<IActionResult> ResetPassword(string? id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            var model = new ResetPasswordVM
            {
                UserId = user.Id,
                UserName = user.Name,
                UserEmail = user.Email
            };

            return View(model);
        }


        // =========================
        // Reset Password
        // =========================

        [HttpPost]
        public async Task<IActionResult> ResetPassword(ResetPasswordVM resetPasswordVM)
        {
            if (!ModelState.IsValid)
            {
                return View(resetPasswordVM);
            }

            var user = await _userManager.FindByIdAsync(resetPasswordVM.UserId);

            if (user == null)
            {
                TempData["error"] = "User not found!";
                return RedirectToAction("Index");
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);

            var result = await _userManager.ResetPasswordAsync(
                user,
                token,
                resetPasswordVM.NewPassword);

            if (result.Succeeded)
            {
                TempData["success"] = "Password reset successfully.";
                return RedirectToAction("Index");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            resetPasswordVM.UserEmail = user.Email;
            resetPasswordVM.UserName = user.Name;

            return View(resetPasswordVM);
        }


        private async Task LoadUsers(string? selectedUserId)
        {
            var users = await _userService.GetAllUserAsync();

            ViewBag.Users = users
                .Select(user => new SelectListItem
                {
                    Text = $"{user.Name} - {user.Email}",
                    Value = user.Id,
                    Selected = user.Id == selectedUserId
                })
                .ToList();
        }

        #region API CALLS
        [AllowAnonymous]
        public async Task<IActionResult> Getall()
        {
            var users = await _userService.GetAllUserAsync();
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                user.Role = roles.FirstOrDefault() ?? "No Role";
            }
            return Json(new { data = users });
        }
        [HttpPost] 
        public async Task<IActionResult> LockUnlock([FromBody] string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return Json(new { success = false, message = "User not found!" });
            }

            var isLocked = await _userManager.IsLockedOutAsync(user);
            if (isLocked)
            {
                await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow);
                return Json(new { success = true, message = "User Locked successfully" });

            }
            else
            {
                await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddMinutes(10));
                return Json(new { success = true, message = "User UnLocked successfully" });

            }

        }
        #endregion
    }
}