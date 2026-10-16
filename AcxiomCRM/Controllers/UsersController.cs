using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using AcxiomCRM.Models;
using AcxiomCRM.Models.Enums;
using AcxiomCRM.Services.Interfaces;
using AcxiomCRM.ViewModels;

namespace AcxiomCRM.Controllers
{
    [Authorize(Roles = RoleConstants.Admin)]
    public class UsersController : Controller
    {
        private readonly IUserService _userService;
        private readonly UserManager<ApplicationUser> _userManager;

        public UsersController(IUserService userService, UserManager<ApplicationUser> userManager)
        {
            _userService = userService;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? searchTerm, string? roleFilter, string? statusFilter)
        {
            var model = await _userService.GetUsersAsync(searchTerm, roleFilter, statusFilter);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var model = new UserEditViewModel();
            var dummy = await _userService.GetUserForEditAsync("dummy");
            // Populate defaults
            model.AvailableRoles = RoleConstants.AllRoles
                .Select(r => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = r, Text = r, Selected = r == RoleConstants.SalesExecutive })
                .ToList();

            var users = _userManager.Users.OrderBy(u => u.FullName).ToList();
            model.AvailableManagers = users
                .Select(u => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = u.Id, Text = $"{u.FullName} ({u.Email})" })
                .ToList();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UserEditViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Password))
                ModelState.AddModelError("Password", "Password is required for a new user.");

            if (!ModelState.IsValid)
            {
                var edit = await _userService.GetUserForEditAsync(model.Id ?? "");
                model.AvailableRoles = edit?.AvailableRoles ?? new();
                model.AvailableManagers = edit?.AvailableManagers ?? new();
                return View(model);
            }

            var adminUser = await _userManager.GetUserAsync(User);
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

            var (success, message, _) = await _userService.CreateUserAsync(model, adminUser?.Id, ip);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, message);
                var edit = await _userService.GetUserForEditAsync(model.Id ?? "");
                model.AvailableRoles = edit?.AvailableRoles ?? new();
                model.AvailableManagers = edit?.AvailableManagers ?? new();
                return View(model);
            }

            TempData["SuccessMessage"] = $"User '{model.Email}' created with role '{model.Role}'.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            var model = await _userService.GetUserForEditAsync(id);
            if (model == null) return NotFound();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, UserEditViewModel model)
        {
            if (id != model.Id) return BadRequest();

            // Password is optional on edit
            ModelState.Remove("Password");
            ModelState.Remove("ConfirmPassword");

            if (!ModelState.IsValid)
            {
                var edit = await _userService.GetUserForEditAsync(id);
                model.AvailableRoles = edit?.AvailableRoles ?? new();
                model.AvailableManagers = edit?.AvailableManagers ?? new();
                return View(model);
            }

            var adminUser = await _userManager.GetUserAsync(User);
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

            var (success, message) = await _userService.UpdateUserAsync(model, adminUser?.Id, ip);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, message);
                var edit = await _userService.GetUserForEditAsync(id);
                model.AvailableRoles = edit?.AvailableRoles ?? new();
                model.AvailableManagers = edit?.AvailableManagers ?? new();
                return View(model);
            }

            TempData["SuccessMessage"] = $"User '{model.Email}' updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(string id)
        {
            var adminUser = await _userManager.GetUserAsync(User);
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

            var (success, message) = await _userService.ToggleUserStatusAsync(id, adminUser?.Id, ip);
            if (!success)
                TempData["ErrorMessage"] = message;
            else
                TempData["SuccessMessage"] = message;

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unlock(string id)
        {
            var adminUser = await _userManager.GetUserAsync(User);
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

            var (success, message) = await _userService.UnlockUserAsync(id, adminUser?.Id, ip);
            if (!success)
                TempData["ErrorMessage"] = message;
            else
                TempData["SuccessMessage"] = message;

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> ResetPassword(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            return View(new AdminResetPasswordViewModel
            {
                UserId = user.Id,
                UserEmail = user.Email ?? string.Empty,
                FullName = user.FullName
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(AdminResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var adminUser = await _userManager.GetUserAsync(User);
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

            var (success, message) = await _userService.AdminResetPasswordAsync(model, adminUser?.Id, ip);
            if (!success)
            {
                ModelState.AddModelError(string.Empty, message);
                return View(model);
            }

            TempData["SuccessMessage"] = $"Password for '{model.UserEmail}' reset successfully.";
            return RedirectToAction(nameof(Index));
        }
    }
}
