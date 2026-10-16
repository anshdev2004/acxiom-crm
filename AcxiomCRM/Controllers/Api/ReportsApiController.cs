using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AcxiomCRM.DTOs;
using AcxiomCRM.Services.Interfaces;

namespace AcxiomCRM.Controllers.Api
{
    [ApiController]
    [Route("api/reports")]
    [Authorize]
    [Produces("application/json")]
    public class ReportsApiController : ControllerBase
    {
        private readonly IOpportunityService _opportunityService;

        public ReportsApiController(IOpportunityService opportunityService)
        {
            _opportunityService = opportunityService;
        }

        /// <summary>
        /// Retrieves pipeline report data including stage-wise and owner-wise summaries with weighted amounts.
        /// </summary>
        [HttpGet("pipeline")]
        public async Task<IActionResult> GetPipelineReport()
        {
            var report = await _opportunityService.GetPipelineReportAsync(User);
            return Ok(ApiResponse<PipelineReportResponseDto>.Ok(report));
        }
    }
}
