using System.Security.Claims;
using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels;

namespace AcxiomCRM.Services.Interfaces
{
    public interface ICustomerService
    {
        Task<CustomerListViewModel> GetCustomersAsync(ClaimsPrincipal principal, string? searchTerm, string? status, string? ownerId, string? sortBy, int page, int pageSize);
        Task<Customer?> GetCustomerByIdAsync(ClaimsPrincipal principal, int id);
        Task<CustomerDetailsViewModel?> GetCustomerDetailsAsync(ClaimsPrincipal principal, int id);
        Task<Customer> CreateCustomerAsync(ClaimsPrincipal principal, CreateCustomerDto dto, string createdBy, string? ipAddress);
        Task<Customer> UpdateCustomerAsync(ClaimsPrincipal principal, int id, UpdateCustomerDto dto, string? ipAddress);
        Task<bool> DeleteOrDeactivateCustomerAsync(ClaimsPrincipal principal, int id, string? ipAddress);
        Task<bool> IsEmailUniqueAsync(string email, int? excludeCustomerId = null);
        Task<bool> IsPhoneUniqueAsync(string phone, int? excludeCustomerId = null);
    }

    public interface ILeadService
    {
        Task<LeadListViewModel> GetLeadsAsync(ClaimsPrincipal principal, string? searchTerm, string? status, string? priority, string? source, string? assignedTo, string? sortBy, int page, int pageSize);
        Task<Lead?> GetLeadByIdAsync(ClaimsPrincipal principal, int id);
        Task<LeadDetailsViewModel?> GetLeadDetailsAsync(ClaimsPrincipal principal, int id);
        Task<Lead> CreateLeadAsync(ClaimsPrincipal principal, CreateLeadDto dto, string? ipAddress);
        Task<Lead> UpdateLeadAsync(ClaimsPrincipal principal, int id, UpdateLeadDto dto, string? ipAddress);
        Task<bool> DeleteLeadAsync(ClaimsPrincipal principal, int id, string? ipAddress);
        Task<(Customer Customer, Opportunity? Opportunity)> ConvertToCustomerAsync(ClaimsPrincipal principal, ConvertLeadDto dto, string? ipAddress);
    }

    public interface IOpportunityService
    {
        Task<OpportunityListViewModel> GetOpportunitiesAsync(ClaimsPrincipal principal, string? searchTerm, string? stage, string? status, string? ownerId, string? sortBy, int page, int pageSize);
        Task<Opportunity?> GetOpportunityByIdAsync(ClaimsPrincipal principal, int id);
        Task<OpportunityDetailsViewModel?> GetOpportunityDetailsAsync(ClaimsPrincipal principal, int id);
        Task<Opportunity> CreateOpportunityAsync(ClaimsPrincipal principal, CreateOpportunityDto dto, string? ipAddress);
        Task<Opportunity> UpdateOpportunityAsync(ClaimsPrincipal principal, int id, UpdateOpportunityDto dto, string? ipAddress);
        Task<bool> DeleteOpportunityAsync(ClaimsPrincipal principal, int id, string? ipAddress);
        Task<PipelineReportResponseDto> GetPipelineReportAsync(ClaimsPrincipal principal);
    }

    public interface IFollowUpService
    {
        Task<FollowUpListViewModel> GetFollowUpsAsync(ClaimsPrincipal principal, string? filterScope, string? status, string? type, string? assignedTo, string? searchTerm, int page, int pageSize);
        Task<FollowUp?> GetFollowUpByIdAsync(ClaimsPrincipal principal, int id);
        Task<FollowUp> CreateFollowUpAsync(ClaimsPrincipal principal, CreateFollowUpDto dto, string? ipAddress);
        Task<FollowUp> UpdateFollowUpAsync(ClaimsPrincipal principal, int id, UpdateFollowUpDto dto, string? ipAddress);
        Task<bool> CompleteFollowUpAsync(ClaimsPrincipal principal, int id, string? remarks, string? ipAddress);
        Task<bool> CancelFollowUpAsync(ClaimsPrincipal principal, int id, string? ipAddress);
    }

    public interface IActivityService
    {
        Task<ActivityListViewModel> GetActivitiesAsync(ClaimsPrincipal principal, string? type, string? status, string? assignedTo, string? searchTerm, int page, int pageSize);
        Task<Activity?> GetActivityByIdAsync(ClaimsPrincipal principal, int id);
        Task<Activity> CreateActivityAsync(ClaimsPrincipal principal, ActivityEditViewModel model, string? ipAddress);
        Task<Activity> UpdateActivityAsync(ClaimsPrincipal principal, int id, ActivityEditViewModel model, string? ipAddress);
        Task<bool> DeleteActivityAsync(ClaimsPrincipal principal, int id, string? ipAddress);
    }

    public interface IDashboardService
    {
        Task<DashboardViewModel> GetDashboardDataAsync(ClaimsPrincipal principal, string dateFilter = "ThisMonth", DateTime? startDate = null, DateTime? endDate = null);
    }

    public interface IReportService
    {
        Task<ReportContainerViewModel> GenerateReportAsync(ClaimsPrincipal principal, ReportFilterViewModel filter);
        Task<byte[]> ExportReportToCsvAsync(ClaimsPrincipal principal, ReportFilterViewModel filter);
    }

    public interface IUserService
    {
        Task<UserListViewModel> GetUsersAsync(string? searchTerm, string? roleFilter, string? statusFilter);
        Task<UserEditViewModel?> GetUserForEditAsync(string userId);
        Task<(bool Success, string Message, string? UserId)> CreateUserAsync(UserEditViewModel model, string? adminUserId, string? ipAddress);
        Task<(bool Success, string Message)> UpdateUserAsync(UserEditViewModel model, string? adminUserId, string? ipAddress);
        Task<(bool Success, string Message)> ToggleUserStatusAsync(string userId, string? adminUserId, string? ipAddress);
        Task<(bool Success, string Message)> UnlockUserAsync(string userId, string? adminUserId, string? ipAddress);
        Task<(bool Success, string Message)> AdminResetPasswordAsync(AdminResetPasswordViewModel model, string? adminUserId, string? ipAddress);
    }
}
