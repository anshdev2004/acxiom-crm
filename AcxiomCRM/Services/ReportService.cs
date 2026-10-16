using System.Security.Claims;
using System.Text;
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
    public class ReportService : IReportService
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserScopeService _userScopeService;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReportService(
            ApplicationDbContext context,
            IUserScopeService userScopeService,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userScopeService = userScopeService;
            _userManager = userManager;
        }

        public async Task<ReportContainerViewModel> GenerateReportAsync(ClaimsPrincipal principal, ReportFilterViewModel filter)
        {
            var accessibleUserIds = await _userScopeService.GetAccessibleUserIdsAsync(principal);
            var role = await _userScopeService.GetPrimaryRoleAsync(principal);

            var result = new ReportContainerViewModel { Filter = filter };

            // Populate filter dropdowns
            filter.AvailableUsers = await _userManager.Users
                .Where(u => accessibleUserIds.Contains(u.Id))
                .OrderBy(u => u.FullName)
                .Select(u => new SelectListItem { Value = u.Id, Text = $"{u.FullName} ({u.Email})" })
                .ToListAsync();

            filter.AvailableStages = OpportunityStages.AllStages
                .Select(s => new SelectListItem { Value = s, Text = s })
                .ToList();

            filter.AvailableStatuses = LeadStatuses.AllStatuses
                .Select(s => new SelectListItem { Value = s, Text = s })
                .ToList();

            switch (filter.ReportType)
            {
                case "Customer":
                    var custQuery = _context.Customers
                        .Include(c => c.Owner)
                        .Where(c => accessibleUserIds.Contains(c.OwnerId));

                    if (!string.IsNullOrWhiteSpace(filter.OwnerId))
                        custQuery = custQuery.Where(c => c.OwnerId == filter.OwnerId);
                    if (!string.IsNullOrWhiteSpace(filter.Status))
                        custQuery = custQuery.Where(c => c.Status == filter.Status);
                    if (filter.StartDate.HasValue)
                        custQuery = custQuery.Where(c => c.CreatedDate >= filter.StartDate.Value);
                    if (filter.EndDate.HasValue)
                        custQuery = custQuery.Where(c => c.CreatedDate <= filter.EndDate.Value.AddDays(1).AddTicks(-1));

                    result.Customers = await custQuery.OrderBy(c => c.CustomerName).ToListAsync();
                    break;

                case "Lead":
                    var leadQuery = _context.Leads
                        .Include(l => l.AssignedUser)
                        .Where(l => accessibleUserIds.Contains(l.AssignedTo));

                    if (!string.IsNullOrWhiteSpace(filter.OwnerId))
                        leadQuery = leadQuery.Where(l => l.AssignedTo == filter.OwnerId);
                    if (!string.IsNullOrWhiteSpace(filter.Status))
                        leadQuery = leadQuery.Where(l => l.Status == filter.Status);
                    if (filter.StartDate.HasValue)
                        leadQuery = leadQuery.Where(l => l.CreatedDate >= filter.StartDate.Value);
                    if (filter.EndDate.HasValue)
                        leadQuery = leadQuery.Where(l => l.CreatedDate <= filter.EndDate.Value.AddDays(1).AddTicks(-1));

                    result.Leads = await leadQuery.OrderByDescending(l => l.CreatedDate).ToListAsync();
                    break;

                case "FollowUp":
                    var fQuery = _context.FollowUps
                        .Include(f => f.Customer)
                        .Include(f => f.Lead)
                        .Include(f => f.AssignedUser)
                        .Where(f => accessibleUserIds.Contains(f.AssignedTo));

                    if (!string.IsNullOrWhiteSpace(filter.OwnerId))
                        fQuery = fQuery.Where(f => f.AssignedTo == filter.OwnerId);
                    if (!string.IsNullOrWhiteSpace(filter.Status))
                        fQuery = fQuery.Where(f => f.Status == filter.Status);
                    if (filter.StartDate.HasValue)
                        fQuery = fQuery.Where(f => f.FollowUpDate >= filter.StartDate.Value);
                    if (filter.EndDate.HasValue)
                        fQuery = fQuery.Where(f => f.FollowUpDate <= filter.EndDate.Value.AddDays(1).AddTicks(-1));

                    result.FollowUps = await fQuery.OrderBy(f => f.FollowUpDate).ToListAsync();
                    break;

                case "Opportunity":
                case "Pipeline":
                    var oppQuery = _context.Opportunities
                        .Include(o => o.Customer)
                        .Include(o => o.AssignedUser)
                        .Where(o => accessibleUserIds.Contains(o.AssignedTo));

                    if (!string.IsNullOrWhiteSpace(filter.OwnerId))
                        oppQuery = oppQuery.Where(o => o.AssignedTo == filter.OwnerId);
                    if (!string.IsNullOrWhiteSpace(filter.Stage))
                        oppQuery = oppQuery.Where(o => o.Stage == filter.Stage);
                    if (!string.IsNullOrWhiteSpace(filter.Status))
                        oppQuery = oppQuery.Where(o => o.Status == filter.Status);
                    if (filter.StartDate.HasValue)
                        oppQuery = oppQuery.Where(o => o.CreatedDate >= filter.StartDate.Value);
                    if (filter.EndDate.HasValue)
                        oppQuery = oppQuery.Where(o => o.CreatedDate <= filter.EndDate.Value.AddDays(1).AddTicks(-1));

                    result.Opportunities = await oppQuery.OrderByDescending(o => o.Amount).ToListAsync();
                    result.TotalAmount = result.Opportunities.Sum(o => o.Amount);
                    result.TotalWeightedAmount = result.Opportunities.Sum(o => o.WeightedAmount);
                    break;

                case "Conversion":
                    var convQuery = _context.Leads
                        .Include(l => l.AssignedUser)
                        .Where(l => accessibleUserIds.Contains(l.AssignedTo));

                    if (!string.IsNullOrWhiteSpace(filter.OwnerId))
                        convQuery = convQuery.Where(l => l.AssignedTo == filter.OwnerId);
                    if (filter.StartDate.HasValue)
                        convQuery = convQuery.Where(l => l.CreatedDate >= filter.StartDate.Value);
                    if (filter.EndDate.HasValue)
                        convQuery = convQuery.Where(l => l.CreatedDate <= filter.EndDate.Value.AddDays(1).AddTicks(-1));

                    result.Leads = await convQuery.ToListAsync();
                    result.TotalLeadsCount = result.Leads.Count;
                    result.ConvertedLeadsCount = result.Leads.Count(l => l.Status == LeadStatuses.Converted);
                    break;

                case "UserActivity":
                    var actQuery = _context.Activities
                        .Include(a => a.AssignedUser)
                        .Where(a => accessibleUserIds.Contains(a.AssignedTo));

                    if (!string.IsNullOrWhiteSpace(filter.OwnerId))
                        actQuery = actQuery.Where(a => a.AssignedTo == filter.OwnerId);
                    if (filter.StartDate.HasValue)
                        actQuery = actQuery.Where(a => a.ActivityDate >= filter.StartDate.Value);
                    if (filter.EndDate.HasValue)
                        actQuery = actQuery.Where(a => a.ActivityDate <= filter.EndDate.Value.AddDays(1).AddTicks(-1));

                    result.Activities = await actQuery.OrderByDescending(a => a.ActivityDate).ToListAsync();
                    result.UserActivityCounts = result.Activities
                        .GroupBy(a => a.AssignedUser != null ? a.AssignedUser.FullName : "Unassigned")
                        .ToDictionary(g => g.Key, g => g.Count());
                    break;

                case "Audit":
                    if (role != RoleConstants.Admin)
                        throw new UnauthorizedAccessException("Audit reports are restricted to Administrators.");

                    var auditQuery = _context.AuditLogs.AsQueryable();
                    if (filter.StartDate.HasValue)
                        auditQuery = auditQuery.Where(a => a.CreatedDate >= filter.StartDate.Value);
                    if (filter.EndDate.HasValue)
                        auditQuery = auditQuery.Where(a => a.CreatedDate <= filter.EndDate.Value.AddDays(1).AddTicks(-1));

                    result.AuditLogs = await auditQuery.OrderByDescending(a => a.CreatedDate).Take(200).ToListAsync();
                    break;
            }

            return result;
        }

        public async Task<byte[]> ExportReportToCsvAsync(ClaimsPrincipal principal, ReportFilterViewModel filter)
        {
            var data = await GenerateReportAsync(principal, filter);
            var sb = new StringBuilder();

            switch (filter.ReportType)
            {
                case "Customer":
                    sb.AppendLine("CustomerCode,CustomerName,Email,Phone,CompanyName,City,State,Status,Owner,CreatedDate");
                    foreach (var c in data.Customers)
                    {
                        sb.AppendLine($"\"{c.CustomerCode}\",\"{EscapeCsv(c.CustomerName)}\",\"{EscapeCsv(c.Email)}\",\"{EscapeCsv(c.Phone)}\",\"{EscapeCsv(c.CompanyName)}\",\"{EscapeCsv(c.City)}\",\"{EscapeCsv(c.State)}\",\"{c.Status}\",\"{EscapeCsv(c.Owner?.FullName)}\",\"{c.CreatedDate:yyyy-MM-dd}\"");
                    }
                    break;

                case "Lead":
                    sb.AppendLine("LeadCode,LeadName,Email,Phone,Company,Source,Status,Priority,ExpectedValue,AssignedTo,CreatedDate");
                    foreach (var l in data.Leads)
                    {
                        sb.AppendLine($"\"{l.LeadCode}\",\"{EscapeCsv(l.LeadName)}\",\"{EscapeCsv(l.Email)}\",\"{EscapeCsv(l.Phone)}\",\"{EscapeCsv(l.CompanyName)}\",\"{l.Source}\",\"{l.Status}\",\"{l.Priority}\",{l.ExpectedValue},\"{EscapeCsv(l.AssignedUser?.FullName)}\",\"{l.CreatedDate:yyyy-MM-dd}\"");
                    }
                    break;

                case "FollowUp":
                    sb.AppendLine("FollowUpId,Subject,FollowUpDate,Type,Status,Customer,Lead,AssignedTo,Remarks");
                    foreach (var f in data.FollowUps)
                    {
                        sb.AppendLine($"{f.FollowUpId},\"{EscapeCsv(f.Subject)}\",\"{f.FollowUpDate:yyyy-MM-dd HH:mm}\",\"{f.FollowUpType}\",\"{f.Status}\",\"{EscapeCsv(f.Customer?.CustomerName)}\",\"{EscapeCsv(f.Lead?.LeadName)}\",\"{EscapeCsv(f.AssignedUser?.FullName)}\",\"{EscapeCsv(f.Remarks)}\"");
                    }
                    break;

                case "Opportunity":
                case "Pipeline":
                    sb.AppendLine("OpportunityId,Name,Customer,Amount,Stage,Probability,WeightedAmount,ExpectedCloseDate,Status,AssignedTo");
                    foreach (var o in data.Opportunities)
                    {
                        sb.AppendLine($"{o.OpportunityId},\"{EscapeCsv(o.OpportunityName)}\",\"{EscapeCsv(o.Customer?.CustomerName)}\",{o.Amount},\"{o.Stage}\",{o.Probability},{o.WeightedAmount},\"{o.ExpectedCloseDate:yyyy-MM-dd}\",\"{o.Status}\",\"{EscapeCsv(o.AssignedUser?.FullName)}\"");
                    }
                    break;

                case "Conversion":
                    sb.AppendLine("Metric,Value");
                    sb.AppendLine($"Total Leads,{data.TotalLeadsCount}");
                    sb.AppendLine($"Converted Leads,{data.ConvertedLeadsCount}");
                    sb.AppendLine($"Conversion Rate,{data.ConversionRate}%");
                    sb.AppendLine();
                    sb.AppendLine("LeadCode,LeadName,Email,Status,ConversionDate,AssignedTo");
                    foreach (var l in data.Leads)
                    {
                        sb.AppendLine($"\"{l.LeadCode}\",\"{EscapeCsv(l.LeadName)}\",\"{EscapeCsv(l.Email)}\",\"{l.Status}\",\"{l.ConversionDate:yyyy-MM-dd}\",\"{EscapeCsv(l.AssignedUser?.FullName)}\"");
                    }
                    break;

                case "UserActivity":
                    sb.AppendLine("User,ActivityCount");
                    foreach (var kvp in data.UserActivityCounts)
                    {
                        sb.AppendLine($"\"{EscapeCsv(kvp.Key)}\",{kvp.Value}");
                    }
                    break;

                case "Audit":
                    sb.AppendLine("AuditLogId,Timestamp,UserEmail,Action,EntityName,RecordId,Result,IpAddress,Details");
                    foreach (var a in data.AuditLogs)
                    {
                        sb.AppendLine($"{a.AuditLogId},\"{a.CreatedDate:yyyy-MM-dd HH:mm:ss}\",\"{EscapeCsv(a.UserEmail)}\",\"{a.Action}\",\"{a.EntityName}\",\"{a.RecordId}\",\"{a.Result}\",\"{a.IpAddress}\",\"{EscapeCsv(a.Details)}\"");
                    }
                    break;
            }

            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        private static string EscapeCsv(string? value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace("\"", "\"\"");
        }
    }
}
