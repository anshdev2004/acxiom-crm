using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Models.Enums;
using AcxiomCRM.Services.Interfaces;
using AcxiomCRM.ViewModels;

namespace AcxiomCRM.Controllers
{
    [Authorize]
    public class CustomersController : Controller
    {
        private readonly ICustomerService _customerService;
        private readonly IUserScopeService _userScopeService;
        private readonly UserManager<ApplicationUser> _userManager;

        public CustomersController(
            ICustomerService customerService,
            IUserScopeService userScopeService,
            UserManager<ApplicationUser> userManager)
        {
            _customerService = customerService;
            _userScopeService = userScopeService;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            string? searchTerm,
            string? statusFilter,
            string? ownerFilter,
            string? sortBy = "NameAsc",
            int page = 1)
        {
            var model = await _customerService.GetCustomersAsync(User, searchTerm, statusFilter, ownerFilter, sortBy, page, 10);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var model = await _customerService.GetCustomerDetailsAsync(User, id);
            if (model == null)
            {
                if (!await _userScopeService.CanAccessCustomerAsync(User, id))
                    return Forbid();

                return NotFound();
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var model = new CustomerEditViewModel();
            await PopulateOwnersDropdownAsync(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CustomerEditViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await PopulateOwnersDropdownAsync(model);
                return View(model);
            }

            try
            {
                var dto = new CreateCustomerDto
                {
                    CustomerName = model.CustomerName,
                    Email = model.Email,
                    Phone = model.Phone,
                    CompanyName = model.CompanyName,
                    Address = model.Address,
                    City = model.City,
                    State = model.State,
                    Status = model.Status,
                    OwnerId = model.OwnerId
                };

                var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
                var customer = await _customerService.CreateCustomerAsync(User, dto, User.Identity?.Name ?? "User", ip);

                TempData["SuccessMessage"] = $"Customer '{customer.CustomerName}' ({customer.CustomerCode}) created successfully.";
                return RedirectToAction(nameof(Details), new { id = customer.CustomerId });
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                await PopulateOwnersDropdownAsync(model);
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var customer = await _customerService.GetCustomerByIdAsync(User, id);
            if (customer == null)
            {
                if (!await _userScopeService.CanAccessCustomerAsync(User, id))
                    return Forbid();

                return NotFound();
            }

            var model = new CustomerEditViewModel
            {
                CustomerId = customer.CustomerId,
                CustomerCode = customer.CustomerCode,
                CustomerName = customer.CustomerName,
                Email = customer.Email,
                Phone = customer.Phone,
                CompanyName = customer.CompanyName,
                Address = customer.Address,
                City = customer.City,
                State = customer.State,
                Status = customer.Status,
                OwnerId = customer.OwnerId,
                IsActive = customer.IsActive
            };

            await PopulateOwnersDropdownAsync(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, CustomerEditViewModel model)
        {
            if (id != model.CustomerId)
                return BadRequest();

            if (!ModelState.IsValid)
            {
                await PopulateOwnersDropdownAsync(model);
                return View(model);
            }

            try
            {
                var dto = new UpdateCustomerDto
                {
                    CustomerName = model.CustomerName,
                    Email = model.Email,
                    Phone = model.Phone,
                    CompanyName = model.CompanyName,
                    Address = model.Address,
                    City = model.City,
                    State = model.State,
                    Status = model.Status,
                    OwnerId = model.OwnerId,
                    IsActive = model.IsActive
                };

                var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
                await _customerService.UpdateCustomerAsync(User, id, dto, ip);

                TempData["SuccessMessage"] = $"Customer '{model.CustomerName}' updated successfully.";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                await PopulateOwnersDropdownAsync(model);
                return View(model);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
                await _customerService.DeleteOrDeactivateCustomerAsync(User, id, ip);
                TempData["SuccessMessage"] = "Customer successfully deleted or deactivated.";
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateOwnersDropdownAsync(CustomerEditViewModel model)
        {
            var role = await _userScopeService.GetPrimaryRoleAsync(User);
            var accessible = await _userScopeService.GetAccessibleUserIdsAsync(User);

            if (role != RoleConstants.SalesExecutive)
            {
                model.AvailableOwners = await _userManager.Users
                    .Where(u => accessible.Contains(u.Id))
                    .OrderBy(u => u.FullName)
                    .Select(u => new SelectListItem { Value = u.Id, Text = $"{u.FullName} ({u.Email})", Selected = u.Id == model.OwnerId })
                    .ToListAsync();
            }
        }
    }
}
