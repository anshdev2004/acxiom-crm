using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Models.Enums;
using AcxiomCRM.Services.Interfaces;

namespace AcxiomCRM.Services
{
    public class UserScopeService : IUserScopeService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public UserScopeService(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public async Task<ApplicationUser?> GetCurrentUserAsync(ClaimsPrincipal principal)
        {
            if (principal?.Identity == null || !principal.Identity.IsAuthenticated)
                return null;

            return await _userManager.GetUserAsync(principal);
        }

        public async Task<string> GetPrimaryRoleAsync(ApplicationUser user)
        {
            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Contains(RoleConstants.Admin)) return RoleConstants.Admin;
            if (roles.Contains(RoleConstants.Manager)) return RoleConstants.Manager;
            return RoleConstants.SalesExecutive;
        }

        public async Task<string> GetPrimaryRoleAsync(ClaimsPrincipal principal)
        {
            var user = await GetCurrentUserAsync(principal);
            if (user == null) return string.Empty;
            return await GetPrimaryRoleAsync(user);
        }

        public async Task<List<string>> GetAccessibleUserIdsAsync(ClaimsPrincipal principal)
        {
            var user = await GetCurrentUserAsync(principal);
            if (user == null) return new List<string>();

            var role = await GetPrimaryRoleAsync(user);

            if (role == RoleConstants.Admin)
            {
                // Admin can access all records
                return await _userManager.Users.Select(u => u.Id).ToListAsync();
            }

            if (role == RoleConstants.Manager)
            {
                // Manager can access their own records and all subordinates reporting to them
                var teamUserIds = await _userManager.Users
                    .Where(u => u.ManagerId == user.Id || u.Id == user.Id)
                    .Select(u => u.Id)
                    .ToListAsync();
                return teamUserIds;
            }

            // SalesExecutive can only access their own records
            return new List<string> { user.Id };
        }

        public async Task<bool> CanAccessCustomerAsync(ClaimsPrincipal principal, int customerId)
        {
            var user = await GetCurrentUserAsync(principal);
            if (user == null) return false;

            var role = await GetPrimaryRoleAsync(user);
            if (role == RoleConstants.Admin) return true;

            var accessibleUserIds = await GetAccessibleUserIdsAsync(principal);
            return await _context.Customers.AnyAsync(c => c.CustomerId == customerId && accessibleUserIds.Contains(c.OwnerId));
        }

        public async Task<bool> CanAccessLeadAsync(ClaimsPrincipal principal, int leadId)
        {
            var user = await GetCurrentUserAsync(principal);
            if (user == null) return false;

            var role = await GetPrimaryRoleAsync(user);
            if (role == RoleConstants.Admin) return true;

            var accessibleUserIds = await GetAccessibleUserIdsAsync(principal);
            return await _context.Leads.AnyAsync(l => l.LeadId == leadId && accessibleUserIds.Contains(l.AssignedTo));
        }

        public async Task<bool> CanAccessOpportunityAsync(ClaimsPrincipal principal, int opportunityId)
        {
            var user = await GetCurrentUserAsync(principal);
            if (user == null) return false;

            var role = await GetPrimaryRoleAsync(user);
            if (role == RoleConstants.Admin) return true;

            var accessibleUserIds = await GetAccessibleUserIdsAsync(principal);
            return await _context.Opportunities.AnyAsync(o => o.OpportunityId == opportunityId && accessibleUserIds.Contains(o.AssignedTo));
        }

        public async Task<bool> CanAccessFollowUpAsync(ClaimsPrincipal principal, int followUpId)
        {
            var user = await GetCurrentUserAsync(principal);
            if (user == null) return false;

            var role = await GetPrimaryRoleAsync(user);
            if (role == RoleConstants.Admin) return true;

            var accessibleUserIds = await GetAccessibleUserIdsAsync(principal);
            return await _context.FollowUps.AnyAsync(f => f.FollowUpId == followUpId && accessibleUserIds.Contains(f.AssignedTo));
        }

        public async Task<bool> CanAccessActivityAsync(ClaimsPrincipal principal, int activityId)
        {
            var user = await GetCurrentUserAsync(principal);
            if (user == null) return false;

            var role = await GetPrimaryRoleAsync(user);
            if (role == RoleConstants.Admin) return true;

            var accessibleUserIds = await GetAccessibleUserIdsAsync(principal);
            return await _context.Activities.AnyAsync(a => a.ActivityId == activityId && accessibleUserIds.Contains(a.AssignedTo));
        }

        public async Task<bool> CanAccessUserRecordAsync(ClaimsPrincipal principal, string targetUserId)
        {
            var accessibleUserIds = await GetAccessibleUserIdsAsync(principal);
            return accessibleUserIds.Contains(targetUserId);
        }
    }
}
