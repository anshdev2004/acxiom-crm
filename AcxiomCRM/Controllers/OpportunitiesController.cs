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
    public class OpportunitiesController : Controller
    {
        private readonly IOpportunityService _opportunityService;
        private readonly IUserScopeService _userScopeService;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public OpportunitiesController(
            IOpportunityService opportunityService,
            IUserScopeService userScopeService,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _opportunityService = opportunityService;
            _userScopeService = userScopeService;
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            string? searchTerm,
            string? stageFilter,
            string? statusFilter,
            string? ownerFilter,
            string? sortBy = "DateDesc",
            int page = 1)
        {
            var model = await _opportunityService.GetOpportunitiesAsync(User, searchTerm, stageFilter, statusFilter, ownerFilter, sortBy, page, 10);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var model = await _opportunityService.GetOpportunityDetailsAsync(User, id);
            if (model == null)
            {
                if (!await _userScopeService.CanAccessOpportunityAsync(User, id))
                    return Forbid();

                return NotFound();
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Create(int? customerId = null, int? leadId = null)
        {
            var model = new OpportunityEditViewModel
            {
                CustomerId = customerId ?? 0,
                LeadId = leadId,
                ExpectedCloseDate = DateTime.Today.AddDays(30)
            };

            await PopulateDropdownsAsync(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(OpportunityEditViewModel model)
        {
            // Business Validation Rules
            if (model.Amount <= 0)
                ModelState.AddModelError("Amount", "Opportunity Amount must be greater than 0.");

            if (model.Probability < 0 || model.Probability > 100)
                ModelState.AddModelError("Probability", "Probability must be between 0 and 100.");

            if (!OpportunityStages.IsClosed(model.Stage) && model.ExpectedCloseDate.Date < DateTime.Today)
                ModelState.AddModelError("ExpectedCloseDate", "Expected Close Date cannot be in the past.");

            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync(model);
                return View(model);
            }

            try
            {
                var dto = new CreateOpportunityDto
                {
                    OpportunityName = model.OpportunityName,
                    CustomerId = model.CustomerId,
                    LeadId = model.LeadId,
                    Amount = model.Amount,
                    Stage = model.Stage,
                    Probability = model.Probability,
                    ExpectedCloseDate = model.ExpectedCloseDate,
                    AssignedTo = model.AssignedTo,
                    Notes = model.Notes
                };

                var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
                var opp = await _opportunityService.CreateOpportunityAsync(User, dto, ip);

                TempData["SuccessMessage"] = $"Opportunity '{opp.OpportunityName}' created successfully.";
                return RedirectToAction(nameof(Details), new { id = opp.OpportunityId });
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
            var opp = await _opportunityService.GetOpportunityByIdAsync(User, id);
            if (opp == null)
            {
                if (!await _userScopeService.CanAccessOpportunityAsync(User, id))
                    return Forbid();

                return NotFound();
            }

            var model = new OpportunityEditViewModel
            {
                OpportunityId = opp.OpportunityId,
                OpportunityName = opp.OpportunityName,
                CustomerId = opp.CustomerId,
                LeadId = opp.LeadId,
                Amount = opp.Amount,
                Stage = opp.Stage,
                Probability = opp.Probability,
                ExpectedCloseDate = opp.ExpectedCloseDate,
                Status = opp.Status,
                AssignedTo = opp.AssignedTo,
                Notes = opp.Notes
            };

            await PopulateDropdownsAsync(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, OpportunityEditViewModel model)
        {
            if (id != model.OpportunityId)
                return BadRequest();

            if (model.Amount <= 0)
                ModelState.AddModelError("Amount", "Opportunity Amount must be greater than 0.");

            if (model.Probability < 0 || model.Probability > 100)
                ModelState.AddModelError("Probability", "Probability must be between 0 and 100.");

            if (!OpportunityStages.IsClosed(model.Stage) && model.ExpectedCloseDate.Date < DateTime.Today)
                ModelState.AddModelError("ExpectedCloseDate", "Expected Close Date cannot be in the past.");

            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync(model);
                return View(model);
            }

            try
            {
                var dto = new UpdateOpportunityDto
                {
                    OpportunityName = model.OpportunityName,
                    CustomerId = model.CustomerId,
                    LeadId = model.LeadId,
                    Amount = model.Amount,
                    Stage = model.Stage,
                    Probability = model.Probability,
                    ExpectedCloseDate = model.ExpectedCloseDate,
                    Status = model.Status,
                    AssignedTo = model.AssignedTo,
                    Notes = model.Notes
                };

                var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
                await _opportunityService.UpdateOpportunityAsync(User, id, dto, ip);

                TempData["SuccessMessage"] = $"Opportunity '{model.OpportunityName}' updated successfully.";
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
                await _opportunityService.DeleteOpportunityAsync(User, id, ip);
                TempData["SuccessMessage"] = "Opportunity deleted successfully.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateDropdownsAsync(OpportunityEditViewModel model)
        {
            var accessible = await _userScopeService.GetAccessibleUserIdsAsync(User);
            var role = await _userScopeService.GetPrimaryRoleAsync(User);

            model.AvailableCustomers = await _context.Customers
                .Where(c => accessible.Contains(c.OwnerId) && c.IsActive)
                .OrderBy(c => c.CustomerName)
                .Select(c => new SelectListItem { Value = c.CustomerId.ToString(), Text = $"{c.CustomerName} ({c.CustomerCode})", Selected = c.CustomerId == model.CustomerId })
                .ToListAsync();

            model.AvailableLeads = await _context.Leads
                .Where(l => accessible.Contains(l.AssignedTo))
                .OrderBy(l => l.LeadName)
                .Select(l => new SelectListItem { Value = l.LeadId.ToString(), Text = $"{l.LeadName} ({l.LeadCode})", Selected = l.LeadId == model.LeadId })
                .ToListAsync();

            model.AvailableStages = OpportunityStages.AllStages
                .Select(s => new SelectListItem { Value = s, Text = s, Selected = s == model.Stage })
                .ToList();

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
