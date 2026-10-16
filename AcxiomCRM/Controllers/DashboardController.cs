using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AcxiomCRM.Services.Interfaces;

namespace AcxiomCRM.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly IDashboardService _dashboardService;

        public DashboardController(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string dateFilter = "ThisMonth", DateTime? startDate = null, DateTime? endDate = null)
        {
            var model = await _dashboardService.GetDashboardDataAsync(User, dateFilter, startDate, endDate);
            return View(model);
        }

        /// <summary>
        /// JSON endpoint consumed by Chart.js for real-time chart updates without full-page reloads.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetChartData(string dateFilter = "ThisMonth", DateTime? startDate = null, DateTime? endDate = null)
        {
            var data = await _dashboardService.GetDashboardDataAsync(User, dateFilter, startDate, endDate);
            return Json(new
            {
                leadStatus = new
                {
                    labels = data.LeadStatusLabels,
                    data = data.LeadStatusCounts
                },
                opportunityPipeline = new
                {
                    labels = data.PipelineStageLabels,
                    amounts = data.PipelineStageAmounts,
                    counts = data.PipelineStageCounts
                },
                monthlySales = new
                {
                    labels = data.MonthlySalesLabels,
                    won = data.MonthlyWonAmounts,
                    lost = data.MonthlyLostAmounts
                }
            });
        }
    }
}
