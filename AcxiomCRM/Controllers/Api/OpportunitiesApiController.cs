using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Services.Interfaces;

namespace AcxiomCRM.Controllers.Api
{
    [ApiController]
    [Route("api/opportunities")]
    [Authorize]
    [Produces("application/json")]
    public class OpportunitiesApiController : ControllerBase
    {
        private readonly IOpportunityService _opportunityService;
        private readonly IUserScopeService _userScopeService;

        public OpportunitiesApiController(IOpportunityService opportunityService, IUserScopeService userScopeService)
        {
            _opportunityService = opportunityService;
            _userScopeService = userScopeService;
        }

        [HttpGet]
        public async Task<IActionResult> GetOpportunities(
            [FromQuery] string? search,
            [FromQuery] string? stage,
            [FromQuery] string? status,
            [FromQuery] string? ownerId,
            [FromQuery] string? sortBy,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _opportunityService.GetOpportunitiesAsync(User, search, stage, status, ownerId, sortBy, page, pageSize);
            var dtos = result.Opportunities.Select(MapToDto).ToList();

            return Ok(new
            {
                success = true,
                totalCount = result.TotalCount,
                currentPage = result.CurrentPage,
                pageSize = result.PageSize,
                totalPages = result.TotalPages,
                totalPipelineValue = result.TotalPipelineValue,
                totalWeightedPipelineValue = result.TotalWeightedPipelineValue,
                data = dtos
            });
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetOpportunity(int id)
        {
            var opp = await _opportunityService.GetOpportunityByIdAsync(User, id);
            if (opp == null)
            {
                if (!await _userScopeService.CanAccessOpportunityAsync(User, id))
                    return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<string>.Fail("Access denied to opportunity outside your role scope."));

                return NotFound(ApiResponse<string>.Fail($"Opportunity with ID {id} not found."));
            }

            return Ok(ApiResponse<OpportunityDto>.Ok(MapToDto(opp)));
        }

        [HttpPost]
        public async Task<IActionResult> CreateOpportunity([FromBody] CreateOpportunityDto dto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return BadRequest(ApiResponse<OpportunityDto>.Fail(errors, "Validation failed."));
            }

            try
            {
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
                var opp = await _opportunityService.CreateOpportunityAsync(User, dto, ip);
                return CreatedAtAction(nameof(GetOpportunity), new { id = opp.OpportunityId }, ApiResponse<OpportunityDto>.Ok(MapToDto(opp), "Opportunity created successfully."));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<OpportunityDto>.Fail(ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<OpportunityDto>.Fail(ex.Message));
            }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateOpportunity(int id, [FromBody] UpdateOpportunityDto dto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return BadRequest(ApiResponse<OpportunityDto>.Fail(errors, "Validation failed."));
            }

            try
            {
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
                var opp = await _opportunityService.UpdateOpportunityAsync(User, id, dto, ip);
                return Ok(ApiResponse<OpportunityDto>.Ok(MapToDto(opp), "Opportunity updated successfully."));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<OpportunityDto>.Fail(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<OpportunityDto>.Fail(ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<OpportunityDto>.Fail(ex.Message));
            }
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteOpportunity(int id)
        {
            try
            {
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
                var success = await _opportunityService.DeleteOpportunityAsync(User, id, ip);
                if (!success)
                    return NotFound(ApiResponse<string>.Fail($"Opportunity with ID {id} not found."));

                return Ok(ApiResponse<string>.Ok("Opportunity deleted successfully."));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<string>.Fail(ex.Message));
            }
        }

        private static OpportunityDto MapToDto(Opportunity o) => new()
        {
            OpportunityId = o.OpportunityId,
            OpportunityName = o.OpportunityName,
            CustomerId = o.CustomerId,
            CustomerName = o.Customer?.CustomerName,
            LeadId = o.LeadId,
            LeadName = o.Lead?.LeadName,
            Amount = o.Amount,
            Stage = o.Stage,
            Probability = o.Probability,
            ExpectedCloseDate = o.ExpectedCloseDate,
            Status = o.Status,
            CreatedDate = o.CreatedDate,
            AssignedTo = o.AssignedTo,
            AssignedUserName = o.AssignedUser?.FullName,
            Notes = o.Notes,
            OutcomeDate = o.OutcomeDate,
            WeightedAmount = o.WeightedAmount
        };
    }
}
