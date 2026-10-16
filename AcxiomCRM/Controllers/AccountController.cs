using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using AcxiomCRM.Models;
using AcxiomCRM.Models.Enums;
using AcxiomCRM.Services.Interfaces;
using AcxiomCRM.ViewModels;

namespace AcxiomCRM.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuditLogService _auditLogService;

        public AccountController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IAuditLogService auditLogService)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _auditLogService = auditLogService;
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Dashboard");

            ViewData["ReturnUrl"] = returnUrl;
            return View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var user = await _userManager.FindByEmailAsync(model.Email.Trim());

            if (user == null)
            {
                await _auditLogService.LogAsync(null, model.Email, AuditActions.FailedLogin, "Authentication", null, null, null, "Failed", "Non-existent user attempted login.", ip);
                ModelState.AddModelError(string.Empty, "Invalid login credentials.");
                return View(model);
            }

            if (!user.IsActive)
            {
                await _auditLogService.LogAsync(user.Id, user.Email, AuditActions.FailedLogin, "Authentication", user.Id, null, null, "Failed", "Deactivated user attempted login.", ip);
                ModelState.AddModelError(string.Empty, "Your account is deactivated. Please contact your system administrator.");
                return View(model);
            }

            var result = await _signInManager.PasswordSignInAsync(
                user.UserName!,
                model.Password,
                model.RememberMe,
                lockoutOnFailure: true);

            if (result.Succeeded)
            {
                user.LastLoginDate = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);

                await _auditLogService.LogAsync(user.Id, user.Email, AuditActions.Login, "Authentication", user.Id, null, null, "Success", "User logged in via web portal.", ip);

                if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                    return Redirect(model.ReturnUrl);

                return RedirectToAction("Index", "Dashboard");
            }

            if (result.IsLockedOut)
            {
                await _auditLogService.LogAsync(user.Id, user.Email, AuditActions.AccountLockout, "Authentication", user.Id, null, null, "Locked", "Account locked out after repeated failed logins.", ip);
                return RedirectToAction(nameof(Lockout));
            }

            await _auditLogService.LogAsync(user.Id, user.Email, AuditActions.FailedLogin, "Authentication", user.Id, null, null, "Failed", "Password check failed.", ip);
            ModelState.AddModelError(string.Empty, "Invalid login credentials.");
            return View(model);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Dashboard");

            return View(new RegisterViewModel());
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

            var existing = await _userManager.FindByEmailAsync(model.Email.Trim());
            if (existing != null)
            {
                ModelState.AddModelError("Email", "An account with this email address already exists.");
                return View(model);
            }

            var user = new ApplicationUser
            {
                UserName = model.Email.Trim(),
                Email = model.Email.Trim(),
                FullName = model.FullName.Trim(),
                PhoneNumber = model.PhoneNumber.Trim(),
                Department = model.Department?.Trim() ?? "Sales",
                EmailConfirmed = true,
                IsActive = true,
                CreatedDate = DateTime.UtcNow
            };

            var createResult = await _userManager.CreateAsync(user, model.Password);
            if (!createResult.Succeeded)
            {
                foreach (var err in createResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, err.Description);
                }
                return View(model);
            }

            // MANDATORY SECURITY RULE: Public registration ALWAYS assigns SalesExecutive on the server side
            await _userManager.AddToRoleAsync(user, RoleConstants.SalesExecutive);

            await _auditLogService.LogAsync(user.Id, user.Email, AuditActions.Create, "UserRegistration", user.Id, null, new { user.Email, user.FullName, Role = RoleConstants.SalesExecutive }, "Success", "New user self-registered as SalesExecutive.", ip);

            // Sign in the newly registered user
            await _signInManager.SignInAsync(user, isPersistent: false);
            return RedirectToAction("Index", "Dashboard");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            var user = await _userManager.GetUserAsync(User);
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

            await _signInManager.SignOutAsync();

            if (user != null)
            {
                await _auditLogService.LogAsync(user.Id, user.Email, AuditActions.Logout, "Authentication", user.Id, null, null, "Success", "User logged out.", ip);
            }

            return RedirectToAction("Login", "Account");
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            return View();
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Lockout()
        {
            return View();
        }
    }
}
