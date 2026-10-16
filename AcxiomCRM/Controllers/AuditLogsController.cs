using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AcxiomCRM.Models.Enums;
using AcxiomCRM.Services.Interfaces;

namespace AcxiomCRM.Controllers
{
    [Authorize(Roles = RoleConstants.Admin)]
    public class AuditLogsController : Controller
    {
        private readonly IAuditLogService _auditLogService;

        public AuditLogsController(IAuditLogService auditLogService)
        {
            _auditLogService = auditLogService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            string? searchTerm,
            string? actionFilter,
            string? entityFilter,
            DateTime? startDate,
            DateTime? endDate,
            int page = 1)
        {
            const int pageSize = 25;
            var logs = await _auditLogService.GetLogsAsync(searchTerm, actionFilter, entityFilter, startDate, endDate, page, pageSize);
            var totalCount = await _auditLogService.GetLogsCountAsync(searchTerm, actionFilter, entityFilter, startDate, endDate);

            ViewBag.SearchTerm = searchTerm;
            ViewBag.ActionFilter = actionFilter;
            ViewBag.EntityFilter = entityFilter;
            ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");
            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalCount = totalCount;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            return View(logs);
        }
    }
}
