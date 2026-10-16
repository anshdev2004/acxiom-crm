using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Models.Enums;
using AcxiomCRM.Services.Interfaces;
using AcxiomCRM.ViewModels;

namespace AcxiomCRM.Controllers
{
    [Authorize]
    public class ActivitiesController : Controller
    {
        private readonly IActivityService _activityService;
        private readonly IUserScopeService _userScopeService;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ActivitiesController(
            IActivityService activityService,
            IUserScopeService userScopeService,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _activityService = activityService;
            _userScopeService = userScopeService;
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            string? type,
            string? status,
            string? assignedTo,
            string? search,
            int page = 1)
        {
            var model = await _activityService.GetActivitiesAsync(User, type, status, assignedTo, search, page, 10);
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Create(int? customerId = null, int? leadId = null)
        {
            var model = new ActivityEditViewModel
            {
                CustomerId = customerId,
                LeadId = leadId,
                ActivityDate = DateTime.Now
            };

            await PopulateDropdownsAsync(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ActivityEditViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync(model);
                return View(model);
            }

            try
            {
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
                var activity = await _activityService.CreateActivityAsync(User, model, ip);

                TempData["SuccessMessage"] = $"Activity '{activity.Subject}' logged successfully.";
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
            var act = await _activityService.GetActivityByIdAsync(User, id);
            if (act == null)
            {
                if (!await _userScopeService.CanAccessActivityAsync(User, id))
                    return Forbid();

                return NotFound();
            }

            var model = new ActivityEditViewModel
            {
                ActivityId = act.ActivityId,
                ActivityType = act.ActivityType,
                Subject = act.Subject,
                Description = act.Description,
                ActivityDate = act.ActivityDate,
                CustomerId = act.CustomerId,
                LeadId = act.LeadId,
                AssignedTo = act.AssignedTo,
                Status = act.Status
            };

            await PopulateDropdownsAsync(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ActivityEditViewModel model)
        {
            if (id != model.ActivityId)
                return BadRequest();

            if (!ModelState.IsValid)
            {
                await PopulateDropdownsAsync(model);
                return View(model);
            }

            try
            {
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
                await _activityService.UpdateActivityAsync(User, id, model, ip);

                TempData["SuccessMessage"] = $"Activity '{model.Subject}' updated successfully.";
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
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
                await _activityService.DeleteActivityAsync(User, id, ip);
                TempData["SuccessMessage"] = "Activity deleted successfully.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateDropdownsAsync(ActivityEditViewModel model)
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
