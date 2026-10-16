using AcxiomCRM.Models;

namespace AcxiomCRM.ViewModels
{
    public class DashboardViewModel
    {
        // Role & Scope
        public string UserRole { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string ScopeDescription { get; set; } = string.Empty;

        // Date Filter
        public string DateFilter { get; set; } = "ThisMonth"; // Today, ThisWeek, ThisMonth, Custom
        public DateTime? CustomStartDate { get; set; }
        public DateTime? CustomEndDate { get; set; }

        // Section 17.11 KPI Cards
        public int TotalCustomers { get; set; }
        public int TotalLeads { get; set; }
        public int OpenLeads { get; set; }
        public int TotalOpportunities { get; set; }
        public int OpenOpportunities { get; set; }
        public int WonOpportunities { get; set; }
        public int LostOpportunities { get; set; }
        public decimal TotalPipelineValue { get; set; }
        public decimal TotalWeightedPipelineValue { get; set; }
        public int PendingFollowUps { get; set; }
        public int OverdueFollowUps { get; set; }

        // Chart Data: Lead Status
        public List<string> LeadStatusLabels { get; set; } = new();
        public List<int> LeadStatusCounts { get; set; } = new();

        // Chart Data: Opportunity Pipeline
        public List<string> PipelineStageLabels { get; set; } = new();
        public List<decimal> PipelineStageAmounts { get; set; } = new();
        public List<int> PipelineStageCounts { get; set; } = new();

        // Chart Data: Monthly Sales Outcomes
        public List<string> MonthlySalesLabels { get; set; } = new();
        public List<decimal> MonthlyWonAmounts { get; set; } = new();
        public List<decimal> MonthlyLostAmounts { get; set; } = new();

        // Recent Lists
        public List<FollowUp> UpcomingFollowUps { get; set; } = new();
        public List<Activity> RecentActivities { get; set; } = new();
        public List<Lead> RecentLeads { get; set; } = new();
        public List<Opportunity> RecentOpportunities { get; set; } = new();
    }
}
