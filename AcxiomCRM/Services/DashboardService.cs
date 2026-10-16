using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models.Enums;
using AcxiomCRM.Services.Interfaces;
using AcxiomCRM.ViewModels;

namespace AcxiomCRM.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserScopeService _userScopeService;

        public DashboardService(ApplicationDbContext context, IUserScopeService userScopeService)
        {
            _context = context;
            _userScopeService = userScopeService;
        }

        public async Task<DashboardViewModel> GetDashboardDataAsync(
            ClaimsPrincipal principal,
            string dateFilter = "ThisMonth",
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            var user = await _userScopeService.GetCurrentUserAsync(principal);
            if (user == null)
                throw new UnauthorizedAccessException("User is not authenticated.");

            var role = await _userScopeService.GetPrimaryRoleAsync(user);
            var accessibleUserIds = await _userScopeService.GetAccessibleUserIdsAsync(principal);

            // Compute date range
            var (rangeStart, rangeEnd) = ResolveDateRange(dateFilter, startDate, endDate);

            // Scope description
            var scopeDesc = role switch
            {
                RoleConstants.Admin => "Global Organization View (All CRM Data)",
                RoleConstants.Manager => "Team View (Direct Reports & Team Pipeline)",
                _ => $"Personal View ({user.FullName})"
            };

            // Queries scoped to accessible users
            var custQuery = _context.Customers.Where(c => accessibleUserIds.Contains(c.OwnerId));
            var leadQuery = _context.Leads.Where(l => accessibleUserIds.Contains(l.AssignedTo));
            var oppQuery = _context.Opportunities.Where(o => accessibleUserIds.Contains(o.AssignedTo));
            var followUpQuery = _context.FollowUps.Where(f => accessibleUserIds.Contains(f.AssignedTo));
            var activityQuery = _context.Activities.Where(a => accessibleUserIds.Contains(a.AssignedTo));

            // Apply date range filter to metrics where relevant
            if (rangeStart.HasValue && rangeEnd.HasValue)
            {
                leadQuery = leadQuery.Where(l => l.CreatedDate >= rangeStart.Value && l.CreatedDate <= rangeEnd.Value);
            }

            // Section 17.11 KPI Cards
            var totalCustomers = await custQuery.CountAsync();
            var totalLeads = await leadQuery.CountAsync();
            var openLeads = await leadQuery.CountAsync(l => l.Status != LeadStatuses.Converted && l.Status != LeadStatuses.Lost && l.Status != LeadStatuses.Unqualified);

            var totalOpportunities = await oppQuery.CountAsync();
            var openOpportunities = await oppQuery.CountAsync(o => o.Status == "Open");
            var wonOpportunities = await oppQuery.CountAsync(o => o.Status == "Won");
            var lostOpportunities = await oppQuery.CountAsync(o => o.Status == "Lost");

            var openOpps = await oppQuery.Where(o => o.Status == "Open").Select(o => new { o.Amount, o.Probability }).ToListAsync();
            var totalPipeline = openOpps.Sum(o => o.Amount);
            var weightedPipeline = openOpps.Sum(o => Math.Round(o.Amount * o.Probability / 100m, 2));

            var now = DateTime.Now;
            var pendingFollowUps = await followUpQuery.CountAsync(f => f.Status == FollowUpStatuses.Planned);
            var overdueFollowUps = await followUpQuery.CountAsync(f => f.Status == FollowUpStatuses.Planned && f.FollowUpDate < now);

            // Chart 1: Lead Status (New, Contacted, Qualified, Lost, Converted)
            var leadStatuses = new[] { LeadStatuses.New, LeadStatuses.Contacted, LeadStatuses.Qualified, LeadStatuses.Lost, LeadStatuses.Converted };
            var leadCounts = new List<int>();
            foreach (var st in leadStatuses)
            {
                var count = await leadQuery.CountAsync(l => l.Status == st);
                leadCounts.Add(count);
            }

            // Chart 2: Opportunity Pipeline (Qualification, Proposal, Negotiation, Won, Lost)
            var oppStages = OpportunityStages.AllStages;
            var stageAmounts = new List<decimal>();
            var stageCounts = new List<int>();
            foreach (var stg in oppStages)
            {
                var stgOpps = await oppQuery.Where(o => o.Stage == stg).ToListAsync();
                stageAmounts.Add(stgOpps.Sum(o => o.Amount));
                stageCounts.Add(stgOpps.Count);
            }

            // Chart 3: Monthly Sales/Outcome Totals (Last 6 months using OutcomeDate or CreatedDate)
            var monthlyLabels = new List<string>();
            var monthlyWon = new List<decimal>();
            var monthlyLost = new List<decimal>();

            var currentMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
            for (int i = 5; i >= 0; i--)
            {
                var mStart = currentMonth.AddMonths(-i);
                var mEnd = mStart.AddMonths(1).AddTicks(-1);
                monthlyLabels.Add(mStart.ToString("MMM yyyy"));

                var wonAmounts = await oppQuery
                    .Where(o => o.Status == "Won" && (o.OutcomeDate ?? o.CreatedDate) >= mStart && (o.OutcomeDate ?? o.CreatedDate) <= mEnd)
                    .Select(o => o.Amount)
                    .ToListAsync();
                var wonSum = wonAmounts.Sum();

                var lostAmounts = await oppQuery
                    .Where(o => o.Status == "Lost" && (o.OutcomeDate ?? o.CreatedDate) >= mStart && (o.OutcomeDate ?? o.CreatedDate) <= mEnd)
                    .Select(o => o.Amount)
                    .ToListAsync();
                var lostSum = lostAmounts.Sum();

                monthlyWon.Add(wonSum);
                monthlyLost.Add(lostSum);
            }

            // Recent items
            var upcomingFollowUps = await followUpQuery
                .Include(f => f.Customer)
                .Include(f => f.Lead)
                .Where(f => f.Status == FollowUpStatuses.Planned)
                .OrderBy(f => f.FollowUpDate)
                .Take(5)
                .ToListAsync();

            var recentActivities = await activityQuery
                .Include(a => a.Customer)
                .Include(a => a.Lead)
                .OrderByDescending(a => a.ActivityDate)
                .Take(5)
                .ToListAsync();

            var recentLeads = await leadQuery
                .OrderByDescending(l => l.CreatedDate)
                .Take(5)
                .ToListAsync();

            var recentOpportunities = await oppQuery
                .Include(o => o.Customer)
                .OrderByDescending(o => o.CreatedDate)
                .Take(5)
                .ToListAsync();

            return new DashboardViewModel
            {
                UserRole = role,
                UserName = user.FullName,
                ScopeDescription = scopeDesc,
                DateFilter = dateFilter,
                CustomStartDate = startDate,
                CustomEndDate = endDate,

                TotalCustomers = totalCustomers,
                TotalLeads = totalLeads,
                OpenLeads = openLeads,
                TotalOpportunities = totalOpportunities,
                OpenOpportunities = openOpportunities,
                WonOpportunities = wonOpportunities,
                LostOpportunities = lostOpportunities,
                TotalPipelineValue = totalPipeline,
                TotalWeightedPipelineValue = weightedPipeline,
                PendingFollowUps = pendingFollowUps,
                OverdueFollowUps = overdueFollowUps,

                LeadStatusLabels = leadStatuses.ToList(),
                LeadStatusCounts = leadCounts,

                PipelineStageLabels = oppStages.ToList(),
                PipelineStageAmounts = stageAmounts,
                PipelineStageCounts = stageCounts,

                MonthlySalesLabels = monthlyLabels,
                MonthlyWonAmounts = monthlyWon,
                MonthlyLostAmounts = monthlyLost,

                UpcomingFollowUps = upcomingFollowUps,
                RecentActivities = recentActivities,
                RecentLeads = recentLeads,
                RecentOpportunities = recentOpportunities
            };
        }

        private static (DateTime? Start, DateTime? End) ResolveDateRange(string filter, DateTime? customStart, DateTime? customEnd)
        {
            var now = DateTime.UtcNow;
            return filter.ToLower() switch
            {
                "today" => (now.Date, now.Date.AddDays(1).AddTicks(-1)),
                "thisweek" => (now.Date.AddDays(-(int)now.DayOfWeek), now.Date.AddDays(7 - (int)now.DayOfWeek).AddTicks(-1)),
                "thismonth" => (new DateTime(now.Year, now.Month, 1), new DateTime(now.Year, now.Month, 1).AddMonths(1).AddTicks(-1)),
                "custom" => (customStart?.Date, customEnd?.Date.AddDays(1).AddTicks(-1)),
                _ => (null, null)
            };
        }
    }
}
