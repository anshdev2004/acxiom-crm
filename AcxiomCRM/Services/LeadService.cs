using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Data;
using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Models.Enums;
using AcxiomCRM.Services.Interfaces;
using AcxiomCRM.ViewModels;

namespace AcxiomCRM.Services
{
    public class LeadService : ILeadService
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserScopeService _userScopeService;
        private readonly IAuditLogService _auditLogService;
        private readonly UserManager<ApplicationUser> _userManager;

        public LeadService(
            ApplicationDbContext context,
            IUserScopeService userScopeService,
            IAuditLogService auditLogService,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userScopeService = userScopeService;
            _auditLogService = auditLogService;
            _userManager = userManager;
        }

        public async Task<LeadListViewModel> GetLeadsAsync(
            ClaimsPrincipal principal,
            string? searchTerm,
            string? status,
            string? priority,
            string? source,
            string? assignedTo,
            string? sortBy,
            int page,
            int pageSize)
        {
            var accessibleUserIds = await _userScopeService.GetAccessibleUserIdsAsync(principal);
            var query = _context.Leads
                .Include(l => l.AssignedUser)
                .Where(l => accessibleUserIds.Contains(l.AssignedTo));

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                query = query.Where(l =>
                    l.LeadName.ToLower().Contains(term) ||
                    l.Email.ToLower().Contains(term) ||
                    l.Phone.ToLower().Contains(term) ||
                    (l.CompanyName != null && l.CompanyName.ToLower().Contains(term)) ||
                    l.LeadCode.ToLower().Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(l => l.Status == status);

            if (!string.IsNullOrWhiteSpace(priority))
                query = query.Where(l => l.Priority == priority);

            if (!string.IsNullOrWhiteSpace(source))
                query = query.Where(l => l.Source == source);

            if (!string.IsNullOrWhiteSpace(assignedTo) && accessibleUserIds.Contains(assignedTo))
                query = query.Where(l => l.AssignedTo == assignedTo);

            query = sortBy switch
            {
                "NameAsc" => query.OrderBy(l => l.LeadName),
                "NameDesc" => query.OrderByDescending(l => l.LeadName),
                "ValueDesc" => query.OrderByDescending(l => l.ExpectedValue),
                "DateAsc" => query.OrderBy(l => l.CreatedDate),
                _ => query.OrderByDescending(l => l.CreatedDate)
            };

            var totalCount = await query.CountAsync();
            var leads = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var currentRole = await _userScopeService.GetPrimaryRoleAsync(principal);
            var availableUsers = new List<SelectListItem>();
            if (currentRole != RoleConstants.SalesExecutive)
            {
                availableUsers = await _userManager.Users
                    .Where(u => accessibleUserIds.Contains(u.Id))
                    .OrderBy(u => u.FullName)
                    .Select(u => new SelectListItem { Value = u.Id, Text = $"{u.FullName} ({u.Email})" })
                    .ToListAsync();
            }

            return new LeadListViewModel
            {
                Leads = leads,
                SearchTerm = searchTerm,
                StatusFilter = status,
                PriorityFilter = priority,
                SourceFilter = source,
                AssignedToFilter = assignedTo,
                SortBy = sortBy,
                CurrentPage = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                AvailableUsers = availableUsers
            };
        }

        public async Task<Lead?> GetLeadByIdAsync(ClaimsPrincipal principal, int id)
        {
            if (!await _userScopeService.CanAccessLeadAsync(principal, id))
                return null;

            return await _context.Leads
                .Include(l => l.AssignedUser)
                .Include(l => l.ConvertedCustomer)
                .Include(l => l.ConvertedOpportunity)
                .FirstOrDefaultAsync(l => l.LeadId == id);
        }

        public async Task<LeadDetailsViewModel?> GetLeadDetailsAsync(ClaimsPrincipal principal, int id)
        {
            if (!await _userScopeService.CanAccessLeadAsync(principal, id))
                return null;

            var lead = await _context.Leads
                .Include(l => l.AssignedUser)
                .Include(l => l.ConvertedCustomer)
                .Include(l => l.ConvertedOpportunity)
                .FirstOrDefaultAsync(l => l.LeadId == id);

            if (lead == null) return null;

            var followUps = await _context.FollowUps
                .Include(f => f.AssignedUser)
                .Where(f => f.LeadId == id)
                .OrderByDescending(f => f.FollowUpDate)
                .ToListAsync();

            var activities = await _context.Activities
                .Include(a => a.AssignedUser)
                .Where(a => a.LeadId == id)
                .OrderByDescending(a => a.ActivityDate)
                .ToListAsync();

            return new LeadDetailsViewModel
            {
                Lead = lead,
                FollowUps = followUps,
                Activities = activities
            };
        }

        public async Task<Lead> CreateLeadAsync(ClaimsPrincipal principal, CreateLeadDto dto, string? ipAddress)
        {
            var currentUser = await _userScopeService.GetCurrentUserAsync(principal);
            if (currentUser == null)
                throw new UnauthorizedAccessException("User is not authenticated.");

            if (!LeadStatuses.IsValid(dto.Status))
                throw new InvalidOperationException($"'{dto.Status}' is not a valid lead status.");

            if (dto.ExpectedValue < 0)
                throw new InvalidOperationException("Expected value cannot be negative.");

            var role = await _userScopeService.GetPrimaryRoleAsync(currentUser);
            var assignedTo = currentUser.Id;
            if (role != RoleConstants.SalesExecutive && !string.IsNullOrWhiteSpace(dto.AssignedTo))
            {
                var accessible = await _userScopeService.GetAccessibleUserIdsAsync(principal);
                if (accessible.Contains(dto.AssignedTo))
                    assignedTo = dto.AssignedTo;
            }

            var nextNum = (await _context.Leads.MaxAsync(l => (int?)l.LeadId) ?? 0) + 1;
            var leadCode = $"LEAD-{nextNum:D4}";

            var lead = new Lead
            {
                LeadCode = leadCode,
                LeadName = dto.LeadName.Trim(),
                Email = dto.Email.Trim().ToLowerInvariant(),
                Phone = dto.Phone.Trim(),
                CompanyName = dto.CompanyName?.Trim(),
                Source = dto.Source,
                Status = dto.Status,
                Priority = dto.Priority,
                ExpectedValue = dto.ExpectedValue,
                Notes = dto.Notes?.Trim(),
                AssignedTo = assignedTo,
                CreatedDate = DateTime.UtcNow
            };

            _context.Leads.Add(lead);
            await _context.SaveChangesAsync();

            await _auditLogService.LogAsync(
                currentUser.Id,
                currentUser.Email,
                AuditActions.Create,
                nameof(Lead),
                lead.LeadId.ToString(),
                null,
                new { lead.LeadCode, lead.LeadName, lead.Email, lead.Status, lead.Priority, lead.ExpectedValue, lead.AssignedTo },
                "Success",
                $"Lead {lead.LeadCode} created.",
                ipAddress);

            return lead;
        }

        public async Task<Lead> UpdateLeadAsync(ClaimsPrincipal principal, int id, UpdateLeadDto dto, string? ipAddress)
        {
            var currentUser = await _userScopeService.GetCurrentUserAsync(principal);
            if (currentUser == null || !await _userScopeService.CanAccessLeadAsync(principal, id))
                throw new UnauthorizedAccessException("You do not have permission to modify this lead.");

            var lead = await _context.Leads.FindAsync(id);
            if (lead == null)
                throw new KeyNotFoundException($"Lead with ID {id} not found.");

            if (lead.Status == LeadStatuses.Converted && dto.Status != LeadStatuses.Converted)
                throw new InvalidOperationException("Converted leads cannot be re-opened or moved away from Converted status.");

            if (!LeadStatuses.CanTransition(lead.Status, dto.Status))
                throw new InvalidOperationException($"Invalid lead status transition from '{lead.Status}' to '{dto.Status}'.");

            if (dto.ExpectedValue < 0)
                throw new InvalidOperationException("Expected value cannot be negative.");

            var role = await _userScopeService.GetPrimaryRoleAsync(currentUser);
            var oldState = new { lead.LeadName, lead.Email, lead.Phone, lead.Status, lead.Priority, lead.ExpectedValue, lead.AssignedTo };

            lead.LeadName = dto.LeadName.Trim();
            lead.Email = dto.Email.Trim().ToLowerInvariant();
            lead.Phone = dto.Phone.Trim();
            lead.CompanyName = dto.CompanyName?.Trim();
            lead.Source = dto.Source;
            lead.Status = dto.Status;
            lead.Priority = dto.Priority;
            lead.ExpectedValue = dto.ExpectedValue;
            lead.Notes = dto.Notes?.Trim();

            if (role != RoleConstants.SalesExecutive && !string.IsNullOrWhiteSpace(dto.AssignedTo))
            {
                var accessible = await _userScopeService.GetAccessibleUserIdsAsync(principal);
                if (accessible.Contains(dto.AssignedTo))
                    lead.AssignedTo = dto.AssignedTo;
            }

            await _context.SaveChangesAsync();

            await _auditLogService.LogAsync(
                currentUser.Id,
                currentUser.Email,
                AuditActions.Update,
                nameof(Lead),
                lead.LeadId.ToString(),
                oldState,
                new { lead.LeadName, lead.Email, lead.Phone, lead.Status, lead.Priority, lead.ExpectedValue, lead.AssignedTo },
                "Success",
                $"Lead {lead.LeadCode} updated.",
                ipAddress);

            return lead;
        }

        public async Task<bool> DeleteLeadAsync(ClaimsPrincipal principal, int id, string? ipAddress)
        {
            var currentUser = await _userScopeService.GetCurrentUserAsync(principal);
            if (currentUser == null || !await _userScopeService.CanAccessLeadAsync(principal, id))
                throw new UnauthorizedAccessException("You do not have permission to delete this lead.");

            var lead = await _context.Leads.FindAsync(id);
            if (lead == null) return false;

            if (lead.Status == LeadStatuses.Converted)
                throw new InvalidOperationException("Converted leads cannot be deleted because they are linked to customer records.");

            var oldState = new { lead.LeadCode, lead.LeadName, lead.Email, lead.Status };

            _context.Leads.Remove(lead);
            await _context.SaveChangesAsync();

            await _auditLogService.LogAsync(
                currentUser.Id,
                currentUser.Email,
                AuditActions.Delete,
                nameof(Lead),
                id.ToString(),
                oldState,
                null,
                "Success",
                $"Lead {lead.LeadCode} deleted.",
                ipAddress);

            return true;
        }

        public async Task<(Customer Customer, Opportunity? Opportunity)> ConvertToCustomerAsync(
            ClaimsPrincipal principal,
            ConvertLeadDto dto,
            string? ipAddress)
        {
            var currentUser = await _userScopeService.GetCurrentUserAsync(principal);
            if (currentUser == null || !await _userScopeService.CanAccessLeadAsync(principal, dto.LeadId))
                throw new UnauthorizedAccessException("You do not have permission to convert this lead.");

            // Use atomic database transaction
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var lead = await _context.Leads.FirstOrDefaultAsync(l => l.LeadId == dto.LeadId);
                if (lead == null)
                    throw new KeyNotFoundException($"Lead with ID {dto.LeadId} not found.");

                if (lead.Status == LeadStatuses.Converted)
                    throw new InvalidOperationException($"Lead {lead.LeadCode} is already converted. Duplicate conversion is prevented.");

                // Check for existing customer by email or phone
                var normEmail = lead.Email.Trim().ToLowerInvariant();
                var normPhone = lead.Phone.Trim();

                var customer = await _context.Customers
                    .FirstOrDefaultAsync(c => c.Email.ToLower() == normEmail || c.Phone == normPhone);

                bool customerCreated = false;
                if (customer == null)
                {
                    // Create new customer
                    var nextCustomerNum = (await _context.Customers.MaxAsync(c => (int?)c.CustomerId) ?? 0) + 1;
                    var custCode = $"CUST-{nextCustomerNum:D4}";

                    customer = new Customer
                    {
                        CustomerCode = custCode,
                        CustomerName = lead.LeadName,
                        Email = normEmail,
                        Phone = normPhone,
                        CompanyName = lead.CompanyName,
                        Status = "Active",
                        OwnerId = lead.AssignedTo,
                        CreatedBy = currentUser.FullName,
                        CreatedDate = DateTime.UtcNow,
                        IsActive = true
                    };

                    _context.Customers.Add(customer);
                    await _context.SaveChangesAsync();
                    customerCreated = true;
                }

                Opportunity? opportunity = null;
                if (dto.CreateOpportunity)
                {
                    var oppName = !string.IsNullOrWhiteSpace(dto.OpportunityName)
                        ? dto.OpportunityName.Trim()
                        : $"{lead.LeadName} - Opportunity";

                    var oppAmount = dto.OpportunityAmount ?? (lead.ExpectedValue > 0 ? lead.ExpectedValue : 1000m);
                    if (oppAmount <= 0)
                        throw new InvalidOperationException("Opportunity Amount must be greater than 0.");

                    var closeDate = dto.ExpectedCloseDate ?? DateTime.Today.AddDays(30);
                    if (closeDate.Date < DateTime.Today)
                        throw new InvalidOperationException("Expected close date cannot be in the past.");

                    opportunity = new Opportunity
                    {
                        OpportunityName = oppName,
                        CustomerId = customer.CustomerId,
                        LeadId = lead.LeadId,
                        Amount = oppAmount,
                        Stage = OpportunityStages.Qualification,
                        Probability = OpportunityStages.DefaultProbability(OpportunityStages.Qualification),
                        ExpectedCloseDate = closeDate,
                        Status = "Open",
                        AssignedTo = lead.AssignedTo,
                        CreatedDate = DateTime.UtcNow,
                        Notes = $"Converted from Lead {lead.LeadCode}. {lead.Notes}"
                    };

                    _context.Opportunities.Add(opportunity);
                    await _context.SaveChangesAsync();
                }

                // Update lead conversion state
                lead.Status = LeadStatuses.Converted;
                lead.ConvertedCustomerId = customer.CustomerId;
                lead.ConvertedOpportunityId = opportunity?.OpportunityId;
                lead.ConversionDate = DateTime.UtcNow;

                // Re-link existing lead follow-ups and activities to new customer
                var leadFollowUps = await _context.FollowUps.Where(f => f.LeadId == lead.LeadId && f.CustomerId == null).ToListAsync();
                foreach (var f in leadFollowUps)
                {
                    f.CustomerId = customer.CustomerId;
                    if (opportunity != null) f.OpportunityId = opportunity.OpportunityId;
                }

                var leadActivities = await _context.Activities.Where(a => a.LeadId == lead.LeadId && a.CustomerId == null).ToListAsync();
                foreach (var a in leadActivities)
                {
                    a.CustomerId = customer.CustomerId;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Audit logging
                await _auditLogService.LogAsync(
                    currentUser.Id,
                    currentUser.Email,
                    AuditActions.LeadConversion,
                    nameof(Lead),
                    lead.LeadId.ToString(),
                    new { LeadCode = lead.LeadCode, Status = LeadStatuses.Qualified },
                    new
                    {
                        LeadCode = lead.LeadCode,
                        Status = LeadStatuses.Converted,
                        CustomerId = customer.CustomerId,
                        CustomerCreated = customerCreated,
                        OpportunityId = opportunity?.OpportunityId
                    },
                    "Success",
                    $"Lead {lead.LeadCode} converted to Customer {customer.CustomerCode}" +
                    (opportunity != null ? $" and Opportunity '{opportunity.OpportunityName}'" : ""),
                    ipAddress);

                return (customer, opportunity);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}
