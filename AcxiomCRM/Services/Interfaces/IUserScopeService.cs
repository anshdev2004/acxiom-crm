using System.Security.Claims;
using AcxiomCRM.Models;

namespace AcxiomCRM.Services.Interfaces
{
    public interface IUserScopeService
    {
        Task<ApplicationUser?> GetCurrentUserAsync(ClaimsPrincipal principal);
        Task<string> GetPrimaryRoleAsync(ApplicationUser user);
        Task<string> GetPrimaryRoleAsync(ClaimsPrincipal principal);
        Task<List<string>> GetAccessibleUserIdsAsync(ClaimsPrincipal principal);
        Task<bool> CanAccessCustomerAsync(ClaimsPrincipal principal, int customerId);
        Task<bool> CanAccessLeadAsync(ClaimsPrincipal principal, int leadId);
        Task<bool> CanAccessOpportunityAsync(ClaimsPrincipal principal, int opportunityId);
        Task<bool> CanAccessFollowUpAsync(ClaimsPrincipal principal, int followUpId);
        Task<bool> CanAccessActivityAsync(ClaimsPrincipal principal, int activityId);
        Task<bool> CanAccessUserRecordAsync(ClaimsPrincipal principal, string targetUserId);
    }
}
