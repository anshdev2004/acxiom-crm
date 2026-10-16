using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Models.Enums;
using AcxiomCRM.Services.Interfaces;
using AcxiomCRM.ViewModels;

namespace AcxiomCRM.Services
{
    public class UserService : IUserService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly ApplicationDbContext _context;
        private readonly IAuditLogService _auditLogService;

        public UserService(
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager,
            ApplicationDbContext context,
            IAuditLogService auditLogService)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _auditLogService = auditLogService;
        }

        public async Task<UserListViewModel> GetUsersAsync(string? searchTerm, string? roleFilter, string? statusFilter)
        {
            var query = _userManager.Users.Include(u => u.Manager).AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                query = query.Where(u =>
                    u.FullName.ToLower().Contains(term) ||
                    (u.Email != null && u.Email.ToLower().Contains(term)) ||
                    (u.PhoneNumber != null && u.PhoneNumber.Contains(term)) ||
                    (u.Department != null && u.Department.ToLower().Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                if (statusFilter == "Active") query = query.Where(u => u.IsActive);
                else if (statusFilter == "Inactive") query = query.Where(u => !u.IsActive);
                else if (statusFilter == "Locked") query = query.Where(u => u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.UtcNow);
            }

            var users = await query.OrderBy(u => u.FullName).ToListAsync();
            var userItems = new List<UserItemViewModel>();

            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);
                var primaryRole = roles.FirstOrDefault() ?? RoleConstants.SalesExecutive;

                if (!string.IsNullOrWhiteSpace(roleFilter) && primaryRole != roleFilter)
                    continue;

                userItems.Add(new UserItemViewModel
                {
                    Id = u.Id,
                    FullName = u.FullName,
                    Email = u.Email ?? string.Empty,
                    PhoneNumber = u.PhoneNumber,
                    Department = u.Department,
                    Role = primaryRole,
                    ManagerName = u.Manager?.FullName,
                    IsActive = u.IsActive,
                    IsLockedOut = u.LockoutEnd.HasValue && u.LockoutEnd.Value > DateTimeOffset.UtcNow,
                    LockoutEnd = u.LockoutEnd?.UtcDateTime,
                    CreatedDate = u.CreatedDate,
                    LastLoginDate = u.LastLoginDate
                });
            }

            return new UserListViewModel
            {
                Users = userItems,
                SearchTerm = searchTerm,
                RoleFilter = roleFilter,
                StatusFilter = statusFilter
            };
        }

        public async Task<UserEditViewModel?> GetUserForEditAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return null;

            var roles = await _userManager.GetRolesAsync(user);
            var primaryRole = roles.FirstOrDefault() ?? RoleConstants.SalesExecutive;

            var availableRoles = RoleConstants.AllRoles
                .Select(r => new SelectListItem { Value = r, Text = r, Selected = r == primaryRole })
                .ToList();

            var managers = await _userManager.Users
                .Where(u => u.Id != userId)
                .OrderBy(u => u.FullName)
                .Select(u => new SelectListItem { Value = u.Id, Text = $"{u.FullName} ({u.Email})", Selected = u.Id == user.ManagerId })
                .ToListAsync();

            return new UserEditViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber,
                Department = user.Department,
                Role = primaryRole,
                ManagerId = user.ManagerId,
                IsActive = user.IsActive,
                AvailableRoles = availableRoles,
                AvailableManagers = managers
            };
        }

        public async Task<(bool Success, string Message, string? UserId)> CreateUserAsync(
            UserEditViewModel model,
            string? adminUserId,
            string? ipAddress)
        {
            var adminUser = !string.IsNullOrEmpty(adminUserId) ? await _userManager.FindByIdAsync(adminUserId) : null;

            if (string.IsNullOrWhiteSpace(model.Password))
                return (false, "Password is required for a new user.", null);

            var existing = await _userManager.FindByEmailAsync(model.Email.Trim());
            if (existing != null)
                return (false, $"A user with email '{model.Email}' already exists.", null);

            var user = new ApplicationUser
            {
                UserName = model.Email.Trim(),
                Email = model.Email.Trim(),
                FullName = model.FullName.Trim(),
                PhoneNumber = model.PhoneNumber?.Trim(),
                Department = model.Department?.Trim(),
                ManagerId = !string.IsNullOrWhiteSpace(model.ManagerId) ? model.ManagerId : null,
                IsActive = model.IsActive,
                EmailConfirmed = true,
                CreatedDate = DateTime.UtcNow
            };

            var createResult = await _userManager.CreateAsync(user, model.Password);
            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                return (false, errors, null);
            }

            // Assign exactly one primary role
            var role = RoleConstants.AllRoles.Contains(model.Role) ? model.Role : RoleConstants.SalesExecutive;
            await _userManager.AddToRoleAsync(user, role);

            await _auditLogService.LogAsync(
                adminUser?.Id,
                adminUser?.Email,
                AuditActions.Create,
                nameof(ApplicationUser),
                user.Id,
                null,
                new { user.Email, user.FullName, Role = role, user.IsActive, user.ManagerId },
                "Success",
                $"Admin created user '{user.Email}' with role '{role}'.",
                ipAddress);

            return (true, "User created successfully.", user.Id);
        }

        public async Task<(bool Success, string Message)> UpdateUserAsync(
            UserEditViewModel model,
            string? adminUserId,
            string? ipAddress)
        {
            if (string.IsNullOrEmpty(model.Id))
                return (false, "User ID is required.");

            var adminUser = !string.IsNullOrEmpty(adminUserId) ? await _userManager.FindByIdAsync(adminUserId) : null;
            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null)
                return (false, "User not found.");

            var currentRoles = await _userManager.GetRolesAsync(user);
            var currentRole = currentRoles.FirstOrDefault() ?? RoleConstants.SalesExecutive;
            var oldState = new { user.Email, user.FullName, Role = currentRole, user.IsActive, user.ManagerId };

            user.FullName = model.FullName.Trim();
            user.PhoneNumber = model.PhoneNumber?.Trim();
            user.Department = model.Department?.Trim();
            user.IsActive = model.IsActive;
            user.ManagerId = !string.IsNullOrWhiteSpace(model.ManagerId) ? model.ManagerId : null;

            if (user.Email != model.Email.Trim())
            {
                var checkEmail = await _userManager.FindByEmailAsync(model.Email.Trim());
                if (checkEmail != null && checkEmail.Id != user.Id)
                    return (false, "Another user already uses this email address.");

                user.Email = model.Email.Trim();
                user.UserName = model.Email.Trim();
            }

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                var errors = string.Join(", ", updateResult.Errors.Select(e => e.Description));
                return (false, errors);
            }

            // Update primary role if changed
            if (model.Role != currentRole && RoleConstants.AllRoles.Contains(model.Role))
            {
                if (currentRoles.Any())
                    await _userManager.RemoveFromRolesAsync(user, currentRoles);

                await _userManager.AddToRoleAsync(user, model.Role);

                await _auditLogService.LogAsync(
                    adminUser?.Id,
                    adminUser?.Email,
                    AuditActions.RoleChange,
                    nameof(ApplicationUser),
                    user.Id,
                    new { Role = currentRole },
                    new { Role = model.Role },
                    "Success",
                    $"User '{user.Email}' role changed from '{currentRole}' to '{model.Role}'.",
                    ipAddress);
            }

            await _auditLogService.LogAsync(
                adminUser?.Id,
                adminUser?.Email,
                AuditActions.Update,
                nameof(ApplicationUser),
                user.Id,
                oldState,
                new { user.Email, user.FullName, Role = model.Role, user.IsActive, user.ManagerId },
                "Success",
                $"Admin updated user profile for '{user.Email}'.",
                ipAddress);

            return (true, "User updated successfully.");
        }

        public async Task<(bool Success, string Message)> ToggleUserStatusAsync(
            string userId,
            string? adminUserId,
            string? ipAddress)
        {
            var adminUser = !string.IsNullOrEmpty(adminUserId) ? await _userManager.FindByIdAsync(adminUserId) : null;
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return (false, "User not found.");

            if (user.Id == adminUserId)
                return (false, "You cannot deactivate your own account.");

            user.IsActive = !user.IsActive;
            await _userManager.UpdateAsync(user);

            var actionDesc = user.IsActive ? "Activated" : "Deactivated";
            await _auditLogService.LogAsync(
                adminUser?.Id,
                adminUser?.Email,
                AuditActions.Security,
                nameof(ApplicationUser),
                user.Id,
                new { IsActive = !user.IsActive },
                new { user.IsActive },
                "Success",
                $"Admin {actionDesc.ToLower()} user '{user.Email}'.",
                ipAddress);

            return (true, $"User account {actionDesc.ToLower()} successfully.");
        }

        public async Task<(bool Success, string Message)> UnlockUserAsync(
            string userId,
            string? adminUserId,
            string? ipAddress)
        {
            var adminUser = !string.IsNullOrEmpty(adminUserId) ? await _userManager.FindByIdAsync(adminUserId) : null;
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return (false, "User not found.");

            var unlockResult = await _userManager.SetLockoutEndDateAsync(user, null);
            if (!unlockResult.Succeeded)
                return (false, "Failed to unlock account.");

            await _userManager.ResetAccessFailedCountAsync(user);

            await _auditLogService.LogAsync(
                adminUser?.Id,
                adminUser?.Email,
                AuditActions.AccountUnlock,
                nameof(ApplicationUser),
                user.Id,
                new { IsLockedOut = true },
                new { IsLockedOut = false },
                "Success",
                $"Admin unlocked user '{user.Email}'.",
                ipAddress);

            return (true, $"User '{user.Email}' has been unlocked.");
        }

        public async Task<(bool Success, string Message)> AdminResetPasswordAsync(
            AdminResetPasswordViewModel model,
            string? adminUserId,
            string? ipAddress)
        {
            var adminUser = !string.IsNullOrEmpty(adminUserId) ? await _userManager.FindByIdAsync(adminUserId) : null;
            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
                return (false, "User not found.");

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);

            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return (false, errors);
            }

            await _auditLogService.LogAsync(
                adminUser?.Id,
                adminUser?.Email,
                AuditActions.PasswordReset,
                nameof(ApplicationUser),
                user.Id,
                null,
                null, // Never log passwords or hashes!
                "Success",
                $"Admin reset password for user '{user.Email}'.",
                ipAddress);

            return (true, "Password reset successfully.");
        }
    }
}
