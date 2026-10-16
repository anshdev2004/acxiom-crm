using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AcxiomCRM.Services.Interfaces;
using AcxiomCRM.ViewModels;

namespace AcxiomCRM.Controllers
{
    [Authorize]
    public class ReportsController : Controller
    {
        private readonly IReportService _reportService;

        public ReportsController(IReportService reportService)
        {
            _reportService = reportService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            string reportType = "Pipeline",
            DateTime? startDate = null,
            DateTime? endDate = null,
            string? ownerId = null,
            string? status = null,
            string? stage = null)
        {
            var filter = new ReportFilterViewModel
            {
                ReportType = reportType,
                StartDate = startDate,
                EndDate = endDate,
                OwnerId = ownerId,
                Status = status,
                Stage = stage
            };

            try
            {
                var model = await _reportService.GenerateReportAsync(User, filter);
                return View(model);
            }
            catch (UnauthorizedAccessException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction(nameof(Index), new { reportType = "Pipeline" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportCsv(
            string reportType = "Pipeline",
            DateTime? startDate = null,
            DateTime? endDate = null,
            string? ownerId = null,
            string? status = null,
            string? stage = null)
        {
            var filter = new ReportFilterViewModel
            {
                ReportType = reportType,
                StartDate = startDate,
                EndDate = endDate,
                OwnerId = ownerId,
                Status = status,
                Stage = stage
            };

            try
            {
                var csvBytes = await _reportService.ExportReportToCsvAsync(User, filter);
                var filename = $"{reportType}_Report_{DateTime.UtcNow:yyyyMMdd_HHmm}.csv";
                return File(csvBytes, "text/csv", filename);
            }
            catch (UnauthorizedAccessException ex)
            {
                TempData["ErrorMessage"] = ex.Message;
                return RedirectToAction(nameof(Index), new { reportType });
            }
        }
    }
}
