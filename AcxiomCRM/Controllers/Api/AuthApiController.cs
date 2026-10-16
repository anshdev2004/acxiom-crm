using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Models.Enums;
using AcxiomCRM.Services.Interfaces;

namespace AcxiomCRM.Controllers.Api
{
    [ApiController]
    [Route("api/auth")]
    [Produces("application/json")]
    public class AuthApiController : ControllerBase
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuditLogService _auditLogService;

        public AuthApiController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IAuditLogService auditLogService)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _auditLogService = auditLogService;
        }

        /// <summary>
        /// Authenticates a user and establishes an authenticated session cookie.
        /// </summary>
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginDto model)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return BadRequest(ApiResponse<AuthResponseDto>.Fail(errors, "Invalid login payload."));
            }

            var user = await _userManager.FindByEmailAsync(model.Email.Trim());
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

            if (user == null)
            {
                await _auditLogService.LogAsync(null, model.Email, AuditActions.FailedLogin, "Authentication", null, null, null, "Failed", "User email does not exist.", ip);
                return Unauthorized(ApiResponse<AuthResponseDto>.Fail("Invalid email or password."));
            }

            if (!user.IsActive)
            {
                await _auditLogService.LogAsync(user.Id, user.Email, AuditActions.FailedLogin, "Authentication", user.Id, null, null, "Failed", "Account is deactivated.", ip);
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<AuthResponseDto>.Fail("Your account has been deactivated. Please contact an administrator."));
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

                var roles = await _userManager.GetRolesAsync(user);
                var primaryRole = roles.FirstOrDefault() ?? RoleConstants.SalesExecutive;

                await _auditLogService.LogAsync(user.Id, user.Email, AuditActions.Login, "Authentication", user.Id, null, null, "Success", "Successful API login.", ip);

                var resp = new AuthResponseDto
                {
                    Succeeded = true,
                    Message = "Authentication successful.",
                    UserId = user.Id,
                    Email = user.Email,
                    FullName = user.FullName,
                    Role = primaryRole,
                    IsLockedOut = false
                };

                return Ok(ApiResponse<AuthResponseDto>.Ok(resp, "Login successful."));
            }

            if (result.IsLockedOut)
            {
                await _auditLogService.LogAsync(user.Id, user.Email, AuditActions.AccountLockout, "Authentication", user.Id, null, null, "Locked", "Account locked out due to repeated failed logins.", ip);
                return StatusCode(StatusCodes.Status423Locked, ApiResponse<AuthResponseDto>.Fail("This account has been temporarily locked out due to repeated failed login attempts. Please try again later or contact an administrator."));
            }

            await _auditLogService.LogAsync(user.Id, user.Email, AuditActions.FailedLogin, "Authentication", user.Id, null, null, "Failed", "Incorrect password attempt.", ip);
            return Unauthorized(ApiResponse<AuthResponseDto>.Fail("Invalid email or password."));
        }

        /// <summary>
        /// Terminates the current authenticated session.
        /// </summary>
        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            var user = await _userManager.GetUserAsync(User);
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

            await _signInManager.SignOutAsync();

            if (user != null)
            {
                await _auditLogService.LogAsync(user.Id, user.Email, AuditActions.Logout, "Authentication", user.Id, null, null, "Success", "API logout successful.", ip);
            }

            return Ok(ApiResponse<string>.Ok("Logged out successfully."));
        }
    }
}
