using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Models.Enums;
using AcxiomCRM.Services.Interfaces;
using AcxiomCRM.ViewModels;

namespace AcxiomCRM.Services
{
    public class FollowUpService : IFollowUpService
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserScopeService _userScopeService;
        private readonly IAuditLogService _auditLogService;
        private readonly UserManager<ApplicationUser> _userManager;

        public FollowUpService(
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

        public async Task<FollowUpListViewModel> GetFollowUpsAsync(
            ClaimsPrincipal principal,
            string? filterScope,
            string? status,
            string? type,
            string? assignedTo,
            string? searchTerm,
            int page,
            int pageSize)
        {
            var accessibleUserIds = await _userScopeService.GetAccessibleUserIdsAsync(principal);
            var query = _context.FollowUps
                .Include(f => f.Customer)
                .Include(f => f.Lead)
                .Include(f => f.Opportunity)
                .Include(f => f.AssignedUser)
                .Where(f => accessibleUserIds.Contains(f.AssignedTo));

            var now = DateTime.Now;
            var today = DateTime.Today;

            // Counts for badges
            var overdueCount = await query.CountAsync(f => f.Status == FollowUpStatuses.Planned && f.FollowUpDate < now);
            var upcomingCount = await query.CountAsync(f => f.Status == FollowUpStatuses.Planned && f.FollowUpDate >= now);
            var pendingCount = await query.CountAsync(f => f.Status == FollowUpStatuses.Planned);

            if (!string.IsNullOrWhiteSpace(filterScope))
            {
                query = filterScope.ToLower() switch
                {
                    "overdue" => query.Where(f => f.Status == FollowUpStatuses.Planned && f.FollowUpDate < now),
                    "upcoming" => query.Where(f => f.Status == FollowUpStatuses.Planned && f.FollowUpDate >= now),
                    "pending" => query.Where(f => f.Status == FollowUpStatuses.Planned),
                    _ => query
                };
            }

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(f => f.Status == status);

            if (!string.IsNullOrWhiteSpace(type))
                query = query.Where(f => f.FollowUpType == type);

            if (!string.IsNullOrWhiteSpace(assignedTo) && accessibleUserIds.Contains(assignedTo))
                query = query.Where(f => f.AssignedTo == assignedTo);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                query = query.Where(f =>
                    f.Subject.ToLower().Contains(term) ||
                    (f.Remarks != null && f.Remarks.ToLower().Contains(term)) ||
                    (f.Customer != null && f.Customer.CustomerName.ToLower().Contains(term)) ||
                    (f.Lead != null && f.Lead.LeadName.ToLower().Contains(term)));
            }

            var totalCount = await query.CountAsync();
            var followUps = await query
                .OrderBy(f => f.FollowUpDate)
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

            return new FollowUpListViewModel
            {
                FollowUps = followUps,
                FilterScope = filterScope,
                StatusFilter = status,
                TypeFilter = type,
                AssignedToFilter = assignedTo,
                SearchTerm = searchTerm,
                CurrentPage = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                OverdueCount = overdueCount,
                UpcomingCount = upcomingCount,
                PendingCount = pendingCount,
                AvailableUsers = availableUsers
            };
        }

        public async Task<FollowUp?> GetFollowUpByIdAsync(ClaimsPrincipal principal, int id)
        {
            if (!await _userScopeService.CanAccessFollowUpAsync(principal, id))
                return null;

            return await _context.FollowUps
                .Include(f => f.Customer)
                .Include(f => f.Lead)
                .Include(f => f.Opportunity)
                .Include(f => f.AssignedUser)
                .FirstOrDefaultAsync(f => f.FollowUpId == id);
        }

        public async Task<FollowUp> CreateFollowUpAsync(ClaimsPrincipal principal, CreateFollowUpDto dto, string? ipAddress)
        {
            var currentUser = await _userScopeService.GetCurrentUserAsync(principal);
            if (currentUser == null)
                throw new UnauthorizedAccessException("User is not authenticated.");

            // Business rule: Follow-up date cannot be earlier than today for a new/planned follow-up
            if (dto.FollowUpDate.Date < DateTime.Today)
                throw new InvalidOperationException("Follow-up date cannot be earlier than today.");

            // Verify relations
            if (dto.CustomerId.HasValue && !await _userScopeService.CanAccessCustomerAsync(principal, dto.CustomerId.Value))
                throw new UnauthorizedAccessException("Invalid customer selection.");

            if (dto.LeadId.HasValue && !await _userScopeService.CanAccessLeadAsync(principal, dto.LeadId.Value))
                throw new UnauthorizedAccessException("Invalid lead selection.");

            if (dto.OpportunityId.HasValue && !await _userScopeService.CanAccessOpportunityAsync(principal, dto.OpportunityId.Value))
                throw new UnauthorizedAccessException("Invalid opportunity selection.");

            var role = await _userScopeService.GetPrimaryRoleAsync(currentUser);
            var assignedTo = currentUser.Id;
            if (role != RoleConstants.SalesExecutive && !string.IsNullOrWhiteSpace(dto.AssignedTo))
            {
                var accessible = await _userScopeService.GetAccessibleUserIdsAsync(principal);
                if (accessible.Contains(dto.AssignedTo))
                    assignedTo = dto.AssignedTo;
            }

            var followUp = new FollowUp
            {
                CustomerId = dto.CustomerId,
                LeadId = dto.LeadId,
                OpportunityId = dto.OpportunityId,
                Subject = dto.Subject.Trim(),
                FollowUpDate = dto.FollowUpDate,
                FollowUpType = dto.FollowUpType,
                Remarks = dto.Remarks?.Trim(),
                Status = FollowUpStatuses.Planned,
                AssignedTo = assignedTo,
                CreatedDate = DateTime.UtcNow
            };

            _context.FollowUps.Add(followUp);
            await _context.SaveChangesAsync();

            await _auditLogService.LogAsync(
                currentUser.Id,
                currentUser.Email,
                AuditActions.Create,
                nameof(FollowUp),
                followUp.FollowUpId.ToString(),
                null,
                new { followUp.Subject, followUp.FollowUpDate, followUp.FollowUpType, followUp.AssignedTo },
                "Success",
                $"Follow-up '{followUp.Subject}' scheduled for {followUp.FollowUpDate:g}.",
                ipAddress);

            return followUp;
        }

        public async Task<FollowUp> UpdateFollowUpAsync(ClaimsPrincipal principal, int id, UpdateFollowUpDto dto, string? ipAddress)
        {
            var currentUser = await _userScopeService.GetCurrentUserAsync(principal);
            if (currentUser == null || !await _userScopeService.CanAccessFollowUpAsync(principal, id))
                throw new UnauthorizedAccessException("You do not have permission to modify this follow-up.");

            var followUp = await _context.FollowUps.FindAsync(id);
            if (followUp == null)
                throw new KeyNotFoundException($"Follow-up with ID {id} not found.");

            var oldDate = followUp.FollowUpDate;
            var isRescheduled = oldDate.Date != dto.FollowUpDate.Date;

            // If planning for future or changing to planned, cannot be in past
            if (dto.Status == FollowUpStatuses.Planned && dto.FollowUpDate.Date < DateTime.Today)
                throw new InvalidOperationException("Follow-up date cannot be earlier than today for planned follow-up.");

            var oldState = new { followUp.Subject, followUp.FollowUpDate, followUp.Status, followUp.Remarks };

            followUp.Subject = dto.Subject.Trim();
            followUp.FollowUpDate = dto.FollowUpDate;
            followUp.FollowUpType = dto.FollowUpType;
            followUp.Remarks = dto.Remarks?.Trim();
            followUp.Status = dto.Status;

            if (dto.Status == FollowUpStatuses.Completed && !followUp.CompletedDate.HasValue)
                followUp.CompletedDate = DateTime.UtcNow;

            var role = await _userScopeService.GetPrimaryRoleAsync(currentUser);
            if (role != RoleConstants.SalesExecutive && !string.IsNullOrWhiteSpace(dto.AssignedTo))
            {
                var accessible = await _userScopeService.GetAccessibleUserIdsAsync(principal);
                if (accessible.Contains(dto.AssignedTo))
                    followUp.AssignedTo = dto.AssignedTo;
            }

            await _context.SaveChangesAsync();

            var actionName = isRescheduled ? "Rescheduled" : AuditActions.Update;
            await _auditLogService.LogAsync(
                currentUser.Id,
                currentUser.Email,
                actionName,
                nameof(FollowUp),
                followUp.FollowUpId.ToString(),
                oldState,
                new { followUp.Subject, followUp.FollowUpDate, followUp.Status, followUp.Remarks },
                "Success",
                isRescheduled ? $"Follow-up rescheduled from {oldDate:g} to {followUp.FollowUpDate:g}." : $"Follow-up '{followUp.Subject}' updated.",
                ipAddress);

            return followUp;
        }

        public async Task<bool> CompleteFollowUpAsync(ClaimsPrincipal principal, int id, string? remarks, string? ipAddress)
        {
            var currentUser = await _userScopeService.GetCurrentUserAsync(principal);
            if (currentUser == null || !await _userScopeService.CanAccessFollowUpAsync(principal, id))
                throw new UnauthorizedAccessException("You do not have permission to complete this follow-up.");

            var followUp = await _context.FollowUps.FindAsync(id);
            if (followUp == null) return false;

            followUp.Status = FollowUpStatuses.Completed;
            followUp.CompletedDate = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(remarks))
            {
                followUp.Remarks = string.IsNullOrWhiteSpace(followUp.Remarks)
                    ? remarks.Trim()
                    : $"{followUp.Remarks}\n[Completed Notes: {remarks.Trim()}]";
            }

            await _context.SaveChangesAsync();

            await _auditLogService.LogAsync(
                currentUser.Id,
                currentUser.Email,
                "Completed",
                nameof(FollowUp),
                followUp.FollowUpId.ToString(),
                new { Status = FollowUpStatuses.Planned },
                new { Status = FollowUpStatuses.Completed, followUp.CompletedDate },
                "Success",
                $"Follow-up '{followUp.Subject}' marked as completed.",
                ipAddress);

            return true;
        }

        public async Task<bool> CancelFollowUpAsync(ClaimsPrincipal principal, int id, string? ipAddress)
        {
            var currentUser = await _userScopeService.GetCurrentUserAsync(principal);
            if (currentUser == null || !await _userScopeService.CanAccessFollowUpAsync(principal, id))
                throw new UnauthorizedAccessException("You do not have permission to cancel this follow-up.");

            var followUp = await _context.FollowUps.FindAsync(id);
            if (followUp == null) return false;

            followUp.Status = FollowUpStatuses.Cancelled;
            await _context.SaveChangesAsync();

            await _auditLogService.LogAsync(
                currentUser.Id,
                currentUser.Email,
                "Cancelled",
                nameof(FollowUp),
                followUp.FollowUpId.ToString(),
                new { Status = FollowUpStatuses.Planned },
                new { Status = FollowUpStatuses.Cancelled },
                "Success",
                $"Follow-up '{followUp.Subject}' cancelled.",
                ipAddress);

            return true;
        }
    }
}
