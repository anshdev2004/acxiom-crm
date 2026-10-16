using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Models.Enums;
using AcxiomCRM.Services.Interfaces;
using AcxiomCRM.ViewModels;

namespace AcxiomCRM.Controllers
{
    [Authorize]
    public class LeadsController : Controller
    {
        private readonly ILeadService _leadService;
        private readonly IUserScopeService _userScopeService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;

        public LeadsController(
            ILeadService leadService,
            IUserScopeService userScopeService,
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext context)
        {
            _leadService = leadService;
            _userScopeService = userScopeService;
            _userManager = userManager;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            string? searchTerm,
            string? statusFilter,
            string? priorityFilter,
            string? sourceFilter,
            string? assignedToFilter,
            string? sortBy = "DateDesc",
            int page = 1)
        {
            var model = await _leadService.GetLeadsAsync(User, searchTerm, statusFilter, priorityFilter, sourceFilter, assignedToFilter, sortBy, page, 10);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var model = await _leadService.GetLeadDetailsAsync(User, id);
            if (model == null)
            {
                if (!await _userScopeService.CanAccessLeadAsync(User, id))
                    return Forbid();

                return NotFound();
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var model = new LeadEditViewModel();
            await PopulateDropdownsAsync(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(LeadEditViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync(model);
                return View(model);
            }

            try
            {
                var dto = new CreateLeadDto
                {
                    LeadName = model.LeadName,
                    Email = model.Email,
                    Phone = model.Phone,
                    CompanyName = model.CompanyName,
                    Source = model.Source,
                    Status = model.Status,
                    Priority = model.Priority,
                    ExpectedValue = model.ExpectedValue,
                    Notes = model.Notes,
                    AssignedTo = model.AssignedTo
                };

                var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
                var lead = await _leadService.CreateLeadAsync(User, dto, ip);

                TempData["SuccessMessage"] = $"Lead '{lead.LeadName}' ({lead.LeadCode}) created successfully.";
                return RedirectToAction(nameof(Details), new { id = lead.LeadId });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                await PopulateDropdownsAsync(model);
                return View(model);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var lead = await _leadService.GetLeadByIdAsync(User, id);
            if (lead == null)
            {
                if (!await _userScopeService.CanAccessLeadAsync(User, id))
                    return Forbid();

                return NotFound();
            }

            var model = new LeadEditViewModel
            {
                LeadId = lead.LeadId,
                LeadCode = lead.LeadCode,
                LeadName = lead.LeadName,
                Email = lead.Email,
                Phone = lead.Phone,
                CompanyName = lead.CompanyName,
                Source = lead.Source,
                Status = lead.Status,
                Priority = lead.Priority,
                ExpectedValue = lead.ExpectedValue,
                Notes = lead.Notes,
                AssignedTo = lead.AssignedTo
            };

            await PopulateDropdownsAsync(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, LeadEditViewModel model)
        {
            if (id != model.LeadId)
                return BadRequest();

            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync(model);
                return View(model);
            }

            try
            {
                var dto = new UpdateLeadDto
                {
                    LeadName = model.LeadName,
                    Email = model.Email,
                    Phone = model.Phone,
                    CompanyName = model.CompanyName,
                    Source = model.Source,
                    Status = model.Status,
                    Priority = model.Priority,
                    ExpectedValue = model.ExpectedValue,
                    Notes = model.Notes,
                    AssignedTo = model.AssignedTo
                };

                var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
                await _leadService.UpdateLeadAsync(User, id, dto, ip);

                TempData["SuccessMessage"] = $"Lead '{model.LeadName}' updated successfully.";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                await PopulateDropdownsAsync(model);
                return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
                await _leadService.DeleteLeadAsync(User, id, ip);
                TempData["SuccessMessage"] = "Lead deleted successfully.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Convert(int id)
        {
            var lead = await _leadService.GetLeadByIdAsync(User, id);
            if (lead == null)
            {
                if (!await _userScopeService.CanAccessLeadAsync(User, id))
                    return Forbid();

                return NotFound();
            }

            if (lead.Status == LeadStatuses.Converted)
            {
                TempData["ErrorMessage"] = $"Lead {lead.LeadCode} is already converted.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Check if customer already exists with this email or phone
            var normEmail = lead.Email.Trim().ToLowerInvariant();
            var existingCustomer = await _context.Customers
                .FirstOrDefaultAsync(c => c.Email.ToLower() == normEmail || c.Phone == lead.Phone);

            var model = new LeadConvertViewModel
            {
                LeadId = lead.LeadId,
                LeadCode = lead.LeadCode,
                LeadName = lead.LeadName,
                Email = lead.Email,
                Phone = lead.Phone,
                CompanyName = lead.CompanyName,
                CreateOpportunity = true,
                OpportunityName = $"{lead.LeadName} - Solution Opportunity",
                OpportunityAmount = lead.ExpectedValue > 0 ? lead.ExpectedValue : 15000m,
                ExpectedCloseDate = DateTime.Today.AddDays(30),
                ExistingCustomerFound = existingCustomer != null,
                ExistingCustomerId = existingCustomer?.CustomerId,
                ExistingCustomerName = existingCustomer?.CustomerName
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Convert(LeadConvertViewModel model)
        {
            if (model.CreateOpportunity)
            {
                if (string.IsNullOrWhiteSpace(model.OpportunityName))
                    ModelState.AddModelError("OpportunityName", "Opportunity Name is required when creating an opportunity.");

                if (!model.OpportunityAmount.HasValue || model.OpportunityAmount <= 0)
                    ModelState.AddModelError("OpportunityAmount", "Opportunity Amount must be greater than 0.");

                if (model.ExpectedCloseDate.HasValue && model.ExpectedCloseDate.Value.Date < DateTime.Today)
                    ModelState.AddModelError("ExpectedCloseDate", "Expected close date cannot be in the past.");
            }

            if (!ModelState.IsValid)
                return View(model);

            try
            {
                var dto = new ConvertLeadDto
                {
                    LeadId = model.LeadId,
                    CreateOpportunity = model.CreateOpportunity,
                    OpportunityName = model.OpportunityName,
                    OpportunityAmount = model.OpportunityAmount,
                    ExpectedCloseDate = model.ExpectedCloseDate
                };

                var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
                var (customer, opportunity) = await _leadService.ConvertToCustomerAsync(User, dto, ip);

                TempData["SuccessMessage"] = $"Lead successfully converted! Customer '{customer.CustomerName}' ({customer.CustomerCode}) created/linked." +
                    (opportunity != null ? $" Opportunity '{opportunity.OpportunityName}' created." : "");

                return RedirectToAction("Details", "Customers", new { id = customer.CustomerId });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                return View(model);
            }
        }

        private async Task PopulateDropdownsAsync(LeadEditViewModel model)
        {
            var role = await _userScopeService.GetPrimaryRoleAsync(User);
            var accessible = await _userScopeService.GetAccessibleUserIdsAsync(User);

            if (role != RoleConstants.SalesExecutive)
            {
                model.AvailableUsers = await _userManager.Users
                    .Where(u => accessible.Contains(u.Id))
                    .OrderBy(u => u.FullName)
                    .Select(u => new SelectListItem { Value = u.Id, Text = $"{u.FullName} ({u.Email})", Selected = u.Id == model.AssignedTo })
                    .ToListAsync();
            }
        }
    }
}
