using AcxiomCRM.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AcxiomCRM.ViewModels
{
    public class ReportFilterViewModel
    {
        public string ReportType { get; set; } = "Pipeline";
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? OwnerId { get; set; }
        public string? Status { get; set; }
        public string? Stage { get; set; }

        public List<SelectListItem> AvailableUsers { get; set; } = new();
        public List<SelectListItem> AvailableStages { get; set; } = new();
        public List<SelectListItem> AvailableStatuses { get; set; } = new();
    }

    public class ReportContainerViewModel
    {
        public ReportFilterViewModel Filter { get; set; } = new();

        public List<Customer> Customers { get; set; } = new();
        public List<Lead> Leads { get; set; } = new();
        public List<FollowUp> FollowUps { get; set; } = new();
        public List<Opportunity> Opportunities { get; set; } = new();
        public List<Activity> Activities { get; set; } = new();
        public List<AuditLog> AuditLogs { get; set; } = new();

        // Aggregates for reports
        public decimal TotalAmount { get; set; }
        public decimal TotalWeightedAmount { get; set; }
        public int ConvertedLeadsCount { get; set; }
        public int TotalLeadsCount { get; set; }
        public decimal ConversionRate => TotalLeadsCount > 0 ? Math.Round((decimal)ConvertedLeadsCount / TotalLeadsCount * 100m, 1) : 0;
        public Dictionary<string, int> UserActivityCounts { get; set; } = new();
    }
}
