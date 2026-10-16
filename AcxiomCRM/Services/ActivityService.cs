using System.Security.Claims;
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
    public class ActivityService : IActivityService
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserScopeService _userScopeService;
        private readonly IAuditLogService _auditLogService;
        private readonly UserManager<ApplicationUser> _userManager;

        public ActivityService(
            ApplicationDbContext context,
            IUserScopeService userScopeService,
            IAuditLogService auditLogService,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userScopeService = userScopeService;
            _auditLogService = auditLogService;
            _userManager = userManager;
        }

        public async Task<ActivityListViewModel> GetActivitiesAsync(
            ClaimsPrincipal principal,
            string? type,
            string? status,
            string? assignedTo,
            string? searchTerm,
            int page,
            int pageSize)
        {
            var accessibleUserIds = await _userScopeService.GetAccessibleUserIdsAsync(principal);
            var query = _context.Activities
                .Include(a => a.Customer)
                .Include(a => a.Lead)
                .Include(a => a.AssignedUser)
                .Where(a => accessibleUserIds.Contains(a.AssignedTo));

            if (!string.IsNullOrWhiteSpace(type))
                query = query.Where(a => a.ActivityType == type);

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(a => a.Status == status);

            if (!string.IsNullOrWhiteSpace(assignedTo) && accessibleUserIds.Contains(assignedTo))
                query = query.Where(a => a.AssignedTo == assignedTo);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                query = query.Where(a =>
                    a.Subject.ToLower().Contains(term) ||
                    (a.Description != null && a.Description.ToLower().Contains(term)) ||
                    (a.Customer != null && a.Customer.CustomerName.ToLower().Contains(term)) ||
                    (a.Lead != null && a.Lead.LeadName.ToLower().Contains(term)));
            }

            var totalCount = await query.CountAsync();
            var activities = await query
                .OrderByDescending(a => a.ActivityDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var currentRole = await _userScopeService.GetPrimaryRoleAsync(principal);
            var availableUsers = new List<SelectListItem>();
            if (currentRole != RoleConstants.SalesExecutive)
            {
                availableUsers = await _userManager.Users
                    .Where(u => accessibleUserIds.Contains(u.Id))
                    .OrderBy(u => u.FullName)
                    .Select(u => new SelectListItem { Value = u.Id, Text = $"{u.FullName} ({u.Email})" })
                    .ToListAsync();
            }

            return new ActivityListViewModel
            {
                Activities = activities,
                TypeFilter = type,
                StatusFilter = status,
                AssignedToFilter = assignedTo,
                SearchTerm = searchTerm,
                CurrentPage = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                AvailableUsers = availableUsers
            };
        }

        public async Task<Activity?> GetActivityByIdAsync(ClaimsPrincipal principal, int id)
        {
            if (!await _userScopeService.CanAccessActivityAsync(principal, id))
                return null;

            return await _context.Activities
                .Include(a => a.Customer)
                .Include(a => a.Lead)
                .Include(a => a.AssignedUser)
                .FirstOrDefaultAsync(a => a.ActivityId == id);
        }

        public async Task<Activity> CreateActivityAsync(ClaimsPrincipal principal, ActivityEditViewModel model, string? ipAddress)
        {
            var currentUser = await _userScopeService.GetCurrentUserAsync(principal);
            if (currentUser == null)
                throw new UnauthorizedAccessException("User is not authenticated.");

            if (model.CustomerId.HasValue && !await _userScopeService.CanAccessCustomerAsync(principal, model.CustomerId.Value))
                throw new UnauthorizedAccessException("Invalid customer selection.");

            if (model.LeadId.HasValue && !await _userScopeService.CanAccessLeadAsync(principal, model.LeadId.Value))
                throw new UnauthorizedAccessException("Invalid lead selection.");

            var role = await _userScopeService.GetPrimaryRoleAsync(currentUser);
            var assignedTo = currentUser.Id;
            if (role != RoleConstants.SalesExecutive && !string.IsNullOrWhiteSpace(model.AssignedTo))
            {
                var accessible = await _userScopeService.GetAccessibleUserIdsAsync(principal);
                if (accessible.Contains(model.AssignedTo))
                    assignedTo = model.AssignedTo;
            }

            var activity = new Activity
            {
                ActivityType = model.ActivityType,
                Subject = model.Subject.Trim(),
                Description = model.Description?.Trim(),
                ActivityDate = model.ActivityDate,
                CustomerId = model.CustomerId,
                LeadId = model.LeadId,
                AssignedTo = assignedTo,
                Status = model.Status,
                CreatedDate = DateTime.UtcNow
            };

            _context.Activities.Add(activity);
            await _context.SaveChangesAsync();

            await _auditLogService.LogAsync(
                currentUser.Id,
                currentUser.Email,
                AuditActions.Create,
                nameof(Activity),
                activity.ActivityId.ToString(),
                null,
                new { activity.ActivityType, activity.Subject, activity.ActivityDate, activity.AssignedTo },
                "Success",
                $"Activity '{activity.Subject}' logged.",
                ipAddress);

            return activity;
        }

        public async Task<Activity> UpdateActivityAsync(ClaimsPrincipal principal, int id, ActivityEditViewModel model, string? ipAddress)
        {
            var currentUser = await _userScopeService.GetCurrentUserAsync(principal);
            if (currentUser == null || !await _userScopeService.CanAccessActivityAsync(principal, id))
                throw new UnauthorizedAccessException("You do not have permission to modify this activity.");

            var activity = await _context.Activities.FindAsync(id);
            if (activity == null)
                throw new KeyNotFoundException($"Activity with ID {id} not found.");

            var oldState = new { activity.ActivityType, activity.Subject, activity.Status, activity.ActivityDate };

            activity.ActivityType = model.ActivityType;
            activity.Subject = model.Subject.Trim();
            activity.Description = model.Description?.Trim();
            activity.ActivityDate = model.ActivityDate;
            activity.Status = model.Status;

            await _context.SaveChangesAsync();

            await _auditLogService.LogAsync(
                currentUser.Id,
                currentUser.Email,
                AuditActions.Update,
                nameof(Activity),
                activity.ActivityId.ToString(),
                oldState,
                new { activity.ActivityType, activity.Subject, activity.Status, activity.ActivityDate },
                "Success",
                $"Activity '{activity.Subject}' updated.",
                ipAddress);

            return activity;
        }

        public async Task<bool> DeleteActivityAsync(ClaimsPrincipal principal, int id, string? ipAddress)
        {
            var currentUser = await _userScopeService.GetCurrentUserAsync(principal);
            if (currentUser == null || !await _userScopeService.CanAccessActivityAsync(principal, id))
                throw new UnauthorizedAccessException("You do not have permission to delete this activity.");

            var activity = await _context.Activities.FindAsync(id);
            if (activity == null) return false;

            var oldState = new { activity.ActivityType, activity.Subject };
            _context.Activities.Remove(activity);
            await _context.SaveChangesAsync();

            await _auditLogService.LogAsync(
                currentUser.Id,
                currentUser.Email,
                AuditActions.Delete,
                nameof(Activity),
                id.ToString(),
                oldState,
                null,
                "Success",
                $"Activity '{activity.Subject}' deleted.",
                ipAddress);

            return true;
        }
    }
}
