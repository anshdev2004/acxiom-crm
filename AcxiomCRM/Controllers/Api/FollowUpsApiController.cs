using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Services.Interfaces;

namespace AcxiomCRM.Controllers.Api
{
    [ApiController]
    [Route("api/followups")]
    [Authorize]
    [Produces("application/json")]
    public class FollowUpsApiController : ControllerBase
    {
        private readonly IFollowUpService _followUpService;
        private readonly IUserScopeService _userScopeService;

        public FollowUpsApiController(IFollowUpService followUpService, IUserScopeService userScopeService)
        {
            _followUpService = followUpService;
            _userScopeService = userScopeService;
        }

        [HttpGet]
        public async Task<IActionResult> GetFollowUps(
            [FromQuery] string? scope,
            [FromQuery] string? status,
            [FromQuery] string? type,
            [FromQuery] string? assignedTo,
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _followUpService.GetFollowUpsAsync(User, scope, status, type, assignedTo, search, page, pageSize);
            var dtos = result.FollowUps.Select(MapToDto).ToList();

            return Ok(new
            {
                success = true,
                totalCount = result.TotalCount,
                currentPage = result.CurrentPage,
                pageSize = result.PageSize,
                totalPages = result.TotalPages,
                pendingCount = result.PendingCount,
                overdueCount = result.OverdueCount,
                upcomingCount = result.UpcomingCount,
                data = dtos
            });
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetFollowUp(int id)
        {
            var followUp = await _followUpService.GetFollowUpByIdAsync(User, id);
            if (followUp == null)
            {
                if (!await _userScopeService.CanAccessFollowUpAsync(User, id))
                    return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<string>.Fail("Access denied to follow-up outside your role scope."));

                return NotFound(ApiResponse<string>.Fail($"Follow-up with ID {id} not found."));
            }

            return Ok(ApiResponse<FollowUpDto>.Ok(MapToDto(followUp)));
        }

        [HttpPost]
        public async Task<IActionResult> CreateFollowUp([FromBody] CreateFollowUpDto dto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return BadRequest(ApiResponse<FollowUpDto>.Fail(errors, "Validation failed."));
            }

            try
            {
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
                var followUp = await _followUpService.CreateFollowUpAsync(User, dto, ip);
                return CreatedAtAction(nameof(GetFollowUp), new { id = followUp.FollowUpId }, ApiResponse<FollowUpDto>.Ok(MapToDto(followUp), "Follow-up scheduled successfully."));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<FollowUpDto>.Fail(ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<FollowUpDto>.Fail(ex.Message));
            }
        }

        [HttpPost("{id:int}/complete")]
        public async Task<IActionResult> CompleteFollowUp(int id, [FromBody] string? remarks)
        {
            try
            {
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
                var success = await _followUpService.CompleteFollowUpAsync(User, id, remarks, ip);
                if (!success)
                    return NotFound(ApiResponse<string>.Fail($"Follow-up with ID {id} not found."));

                return Ok(ApiResponse<string>.Ok("Follow-up marked as completed."));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<string>.Fail(ex.Message));
            }
        }

        private static FollowUpDto MapToDto(FollowUp f) => new()
        {
            FollowUpId = f.FollowUpId,
            CustomerId = f.CustomerId,
            CustomerName = f.Customer?.CustomerName,
            LeadId = f.LeadId,
            LeadName = f.Lead?.LeadName,
            OpportunityId = f.OpportunityId,
            OpportunityName = f.Opportunity?.OpportunityName,
            Subject = f.Subject,
            FollowUpDate = f.FollowUpDate,
            FollowUpType = f.FollowUpType,
            Remarks = f.Remarks,
            Status = f.Status,
            AssignedTo = f.AssignedTo,
            AssignedUserName = f.AssignedUser?.FullName,
            CompletedDate = f.CompletedDate,
            CreatedDate = f.CreatedDate
        };
    }
}
