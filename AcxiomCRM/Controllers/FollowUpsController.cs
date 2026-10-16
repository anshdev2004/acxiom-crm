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
    public class FollowUpsController : Controller
    {
        private readonly IFollowUpService _followUpService;
        private readonly IUserScopeService _userScopeService;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public FollowUpsController(
            IFollowUpService followUpService,
            IUserScopeService userScopeService,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _followUpService = followUpService;
            _userScopeService = userScopeService;
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            string? scope,
            string? statusFilter,
            string? typeFilter,
            string? assignedToFilter,
            string? searchTerm,
            int page = 1)
        {
            var model = await _followUpService.GetFollowUpsAsync(User, scope, statusFilter, typeFilter, assignedToFilter, searchTerm, page, 10);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Create(int? customerId = null, int? leadId = null, int? opportunityId = null)
        {
            var model = new FollowUpEditViewModel
            {
                CustomerId = customerId,
                LeadId = leadId,
                OpportunityId = opportunityId,
                FollowUpDate = DateTime.Today.AddDays(1).AddHours(10)
            };

            await PopulateDropdownsAsync(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(FollowUpEditViewModel model)
        {
            // Business rule: Planned follow-up date cannot be earlier than today
            if (model.FollowUpDate.Date < DateTime.Today)
            {
                ModelState.AddModelError("FollowUpDate", "Follow-up date cannot be earlier than today.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync(model);
                return View(model);
            }

            try
            {
                var dto = new CreateFollowUpDto
                {
                    CustomerId = model.CustomerId,
                    LeadId = model.LeadId,
                    OpportunityId = model.OpportunityId,
                    Subject = model.Subject,
                    FollowUpDate = model.FollowUpDate,
                    FollowUpType = model.FollowUpType,
                    Remarks = model.Remarks,
                    AssignedTo = model.AssignedTo
                };

                var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
                var fu = await _followUpService.CreateFollowUpAsync(User, dto, ip);

                TempData["SuccessMessage"] = $"Follow-up '{fu.Subject}' scheduled successfully.";
                return RedirectToAction(nameof(Index));
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
            var fu = await _followUpService.GetFollowUpByIdAsync(User, id);
            if (fu == null)
            {
                if (!await _userScopeService.CanAccessFollowUpAsync(User, id))
                    return Forbid();

                return NotFound();
            }

            var model = new FollowUpEditViewModel
            {
                FollowUpId = fu.FollowUpId,
                CustomerId = fu.CustomerId,
                LeadId = fu.LeadId,
                OpportunityId = fu.OpportunityId,
                Subject = fu.Subject,
                FollowUpDate = fu.FollowUpDate,
                FollowUpType = fu.FollowUpType,
                Remarks = fu.Remarks,
                Status = fu.Status,
                AssignedTo = fu.AssignedTo
            };

            await PopulateDropdownsAsync(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, FollowUpEditViewModel model)
        {
            if (id != model.FollowUpId)
                return BadRequest();

            if (model.Status == FollowUpStatuses.Planned && model.FollowUpDate.Date < DateTime.Today)
            {
                ModelState.AddModelError("FollowUpDate", "Follow-up date cannot be earlier than today for a planned activity.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync(model);
                return View(model);
            }

            try
            {
                var dto = new UpdateFollowUpDto
                {
                    Subject = model.Subject,
                    FollowUpDate = model.FollowUpDate,
                    FollowUpType = model.FollowUpType,
                    Remarks = model.Remarks,
                    Status = model.Status,
                    AssignedTo = model.AssignedTo
                };

                var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
                await _followUpService.UpdateFollowUpAsync(User, id, dto, ip);

                TempData["SuccessMessage"] = $"Follow-up '{model.Subject}' updated successfully.";
                return RedirectToAction(nameof(Index));
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
        public async Task<IActionResult> Complete(int id, string? remarks)
        {
            try
            {
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
                await _followUpService.CompleteFollowUpAsync(User, id, remarks, ip);
                TempData["SuccessMessage"] = "Follow-up marked as completed.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            try
            {
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
                await _followUpService.CancelFollowUpAsync(User, id, ip);
                TempData["SuccessMessage"] = "Follow-up cancelled.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateDropdownsAsync(FollowUpEditViewModel model)
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

            model.AvailableOpportunities = await _context.Opportunities
                .Where(o => accessible.Contains(o.AssignedTo))
                .OrderBy(o => o.OpportunityName)
                .Select(o => new SelectListItem { Value = o.OpportunityId.ToString(), Text = $"{o.OpportunityName}", Selected = o.OpportunityId == model.OpportunityId })
                .ToListAsync();

            model.AvailableTypes = FollowUpTypes.All
                .Select(t => new SelectListItem { Value = t, Text = t, Selected = t == model.FollowUpType })
                .ToList();

            model.AvailableStatuses = FollowUpStatuses.All
                .Select(s => new SelectListItem { Value = s, Text = s, Selected = s == model.Status })
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
