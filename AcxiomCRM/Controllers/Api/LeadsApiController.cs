using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Services.Interfaces;

namespace AcxiomCRM.Controllers.Api
{
    [ApiController]
    [Route("api/leads")]
    [Authorize]
    [Produces("application/json")]
    public class LeadsApiController : ControllerBase
    {
        private readonly ILeadService _leadService;
        private readonly IUserScopeService _userScopeService;

        public LeadsApiController(ILeadService leadService, IUserScopeService userScopeService)
        {
            _leadService = leadService;
            _userScopeService = userScopeService;
        }

        [HttpGet]
        public async Task<IActionResult> GetLeads(
            [FromQuery] string? search,
            [FromQuery] string? status,
            [FromQuery] string? priority,
            [FromQuery] string? source,
            [FromQuery] string? assignedTo,
            [FromQuery] string? sortBy,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _leadService.GetLeadsAsync(User, search, status, priority, source, assignedTo, sortBy, page, pageSize);
            var dtos = result.Leads.Select(MapToDto).ToList();

            return Ok(new
            {
                success = true,
                totalCount = result.TotalCount,
                currentPage = result.CurrentPage,
                pageSize = result.PageSize,
                totalPages = result.TotalPages,
                data = dtos
            });
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetLead(int id)
        {
            var lead = await _leadService.GetLeadByIdAsync(User, id);
            if (lead == null)
            {
                if (!await _userScopeService.CanAccessLeadAsync(User, id))
                    return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<string>.Fail("Access denied to lead outside your role scope."));

                return NotFound(ApiResponse<string>.Fail($"Lead with ID {id} not found."));
            }

            return Ok(ApiResponse<LeadDto>.Ok(MapToDto(lead)));
        }

        [HttpPost]
        public async Task<IActionResult> CreateLead([FromBody] CreateLeadDto dto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return BadRequest(ApiResponse<LeadDto>.Fail(errors, "Validation failed."));
            }

            try
            {
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
                var lead = await _leadService.CreateLeadAsync(User, dto, ip);
                return CreatedAtAction(nameof(GetLead), new { id = lead.LeadId }, ApiResponse<LeadDto>.Ok(MapToDto(lead), "Lead created successfully."));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<LeadDto>.Fail(ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<LeadDto>.Fail(ex.Message));
            }
        }

        [HttpPost("{id:int}/convert")]
        public async Task<IActionResult> ConvertLead(int id, [FromBody] ConvertLeadDto dto)
        {
            dto.LeadId = id;
            try
            {
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
                var (customer, opportunity) = await _leadService.ConvertToCustomerAsync(User, dto, ip);

                return Ok(new
                {
                    success = true,
                    message = "Lead successfully converted to Customer.",
                    customerId = customer.CustomerId,
                    customerCode = customer.CustomerCode,
                    opportunityId = opportunity?.OpportunityId
                });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<string>.Fail(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<string>.Fail(ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<string>.Fail(ex.Message));
            }
        }

        private static LeadDto MapToDto(Lead l) => new()
        {
            LeadId = l.LeadId,
            LeadCode = l.LeadCode,
            LeadName = l.LeadName,
            Email = l.Email,
            Phone = l.Phone,
            CompanyName = l.CompanyName,
            Source = l.Source,
            Status = l.Status,
            Priority = l.Priority,
            ExpectedValue = l.ExpectedValue,
            Notes = l.Notes,
            CreatedDate = l.CreatedDate,
            AssignedTo = l.AssignedTo,
            AssignedUserName = l.AssignedUser?.FullName,
            ConvertedCustomerId = l.ConvertedCustomerId,
            ConvertedOpportunityId = l.ConvertedOpportunityId,
            ConversionDate = l.ConversionDate
        };
    }
}
