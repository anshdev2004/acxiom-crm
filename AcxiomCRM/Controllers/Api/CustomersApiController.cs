using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Services.Interfaces;

namespace AcxiomCRM.Controllers.Api
{
    [ApiController]
    [Route("api/customers")]
    [Authorize]
    [Produces("application/json")]
    public class CustomersApiController : ControllerBase
    {
        private readonly ICustomerService _customerService;
        private readonly IUserScopeService _userScopeService;

        public CustomersApiController(ICustomerService customerService, IUserScopeService userScopeService)
        {
            _customerService = customerService;
            _userScopeService = userScopeService;
        }

        [HttpGet]
        public async Task<IActionResult> GetCustomers(
            [FromQuery] string? search,
            [FromQuery] string? status,
            [FromQuery] string? ownerId,
            [FromQuery] string? sortBy,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var result = await _customerService.GetCustomersAsync(User, search, status, ownerId, sortBy, page, pageSize);
            var dtos = result.Customers.Select(MapToDto).ToList();

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
        public async Task<IActionResult> GetCustomer(int id)
        {
            var customer = await _customerService.GetCustomerByIdAsync(User, id);
            if (customer == null)
            {
                if (!await _userScopeService.CanAccessCustomerAsync(User, id))
                    return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<string>.Fail("Access denied to customer outside your role scope."));

                return NotFound(ApiResponse<string>.Fail($"Customer with ID {id} not found."));
            }

            return Ok(ApiResponse<CustomerDto>.Ok(MapToDto(customer)));
        }

        [HttpPost]
        public async Task<IActionResult> CreateCustomer([FromBody] CreateCustomerDto dto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return BadRequest(ApiResponse<CustomerDto>.Fail(errors, "Validation failed."));
            }

            try
            {
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
                var customer = await _customerService.CreateCustomerAsync(User, dto, User.Identity?.Name ?? "API", ip);
                return CreatedAtAction(nameof(GetCustomer), new { id = customer.CustomerId }, ApiResponse<CustomerDto>.Ok(MapToDto(customer), "Customer created successfully."));
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ApiResponse<CustomerDto>.Fail(ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<CustomerDto>.Fail(ex.Message));
            }
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateCustomer(int id, [FromBody] UpdateCustomerDto dto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return BadRequest(ApiResponse<CustomerDto>.Fail(errors, "Validation failed."));
            }

            try
            {
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
                var customer = await _customerService.UpdateCustomerAsync(User, id, dto, ip);
                return Ok(ApiResponse<CustomerDto>.Ok(MapToDto(customer), "Customer updated successfully."));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<CustomerDto>.Fail(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ApiResponse<CustomerDto>.Fail(ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<CustomerDto>.Fail(ex.Message));
            }
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteCustomer(int id)
        {
            try
            {
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
                var success = await _customerService.DeleteOrDeactivateCustomerAsync(User, id, ip);
                if (!success)
                    return NotFound(ApiResponse<string>.Fail($"Customer with ID {id} not found."));

                return Ok(ApiResponse<string>.Ok("Customer deleted or deactivated successfully."));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<string>.Fail(ex.Message));
            }
        }

        private static CustomerDto MapToDto(Customer c) => new()
        {
            CustomerId = c.CustomerId,
            CustomerCode = c.CustomerCode,
            CustomerName = c.CustomerName,
            Email = c.Email,
            Phone = c.Phone,
            CompanyName = c.CompanyName,
            Address = c.Address,
            City = c.City,
            State = c.State,
            Status = c.Status,
            CreatedDate = c.CreatedDate,
            ModifiedDate = c.ModifiedDate,
            CreatedBy = c.CreatedBy,
            OwnerId = c.OwnerId,
            OwnerName = c.Owner?.FullName,
            IsActive = c.IsActive,
            OpportunityCount = c.Opportunities?.Count ?? 0
        };
    }
}
