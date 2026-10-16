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
    public class OpportunityService : IOpportunityService
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserScopeService _userScopeService;
        private readonly IAuditLogService _auditLogService;
        private readonly UserManager<ApplicationUser> _userManager;

        public OpportunityService(
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

        public async Task<OpportunityListViewModel> GetOpportunitiesAsync(
            ClaimsPrincipal principal,
            string? searchTerm,
            string? stage,
            string? status,
            string? ownerId,
            string? sortBy,
            int page,
            int pageSize)
        {
            var accessibleUserIds = await _userScopeService.GetAccessibleUserIdsAsync(principal);
            var query = _context.Opportunities
                .Include(o => o.Customer)
                .Include(o => o.Lead)
                .Include(o => o.AssignedUser)
                .Where(o => accessibleUserIds.Contains(o.AssignedTo));

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                query = query.Where(o =>
                    o.OpportunityName.ToLower().Contains(term) ||
                    (o.Customer != null && o.Customer.CustomerName.ToLower().Contains(term)) ||
                    (o.Lead != null && o.Lead.LeadName.ToLower().Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(stage))
                query = query.Where(o => o.Stage == stage);

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(o => o.Status == status);

            if (!string.IsNullOrWhiteSpace(ownerId) && accessibleUserIds.Contains(ownerId))
                query = query.Where(o => o.AssignedTo == ownerId);

            var totalCount = await query.CountAsync();
            var openOpps = await query.Where(o => o.Status == "Open").Select(o => new { o.Amount, o.Probability }).ToListAsync();
            var totalPipeline = openOpps.Sum(o => o.Amount);
            var totalWeighted = openOpps.Sum(o => Math.Round(o.Amount * o.Probability / 100m, 2));

            query = sortBy switch
            {
                "AmountDesc" => query.OrderByDescending(o => o.Amount),
                "AmountAsc" => query.OrderBy(o => o.Amount),
                "CloseDateAsc" => query.OrderBy(o => o.ExpectedCloseDate),
                "CloseDateDesc" => query.OrderByDescending(o => o.ExpectedCloseDate),
                "StageAsc" => query.OrderBy(o => o.Stage),
                _ => query.OrderByDescending(o => o.CreatedDate)
            };

            var opportunities = await query
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

            var availableStages = OpportunityStages.AllStages
                .Select(s => new SelectListItem { Value = s, Text = s })
                .ToList();

            return new OpportunityListViewModel
            {
                Opportunities = opportunities,
                SearchTerm = searchTerm,
                StageFilter = stage,
                StatusFilter = status,
                OwnerFilter = ownerId,
                SortBy = sortBy,
                CurrentPage = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPipelineValue = totalPipeline,
                TotalWeightedPipelineValue = totalWeighted,
                AvailableStages = availableStages,
                AvailableUsers = availableUsers
            };
        }

        public async Task<Opportunity?> GetOpportunityByIdAsync(ClaimsPrincipal principal, int id)
        {
            if (!await _userScopeService.CanAccessOpportunityAsync(principal, id))
                return null;

            return await _context.Opportunities
                .Include(o => o.Customer)
                .Include(o => o.Lead)
                .Include(o => o.AssignedUser)
                .FirstOrDefaultAsync(o => o.OpportunityId == id);
        }

        public async Task<OpportunityDetailsViewModel?> GetOpportunityDetailsAsync(ClaimsPrincipal principal, int id)
        {
            if (!await _userScopeService.CanAccessOpportunityAsync(principal, id))
                return null;

            var opp = await _context.Opportunities
                .Include(o => o.Customer)
                .Include(o => o.Lead)
                .Include(o => o.AssignedUser)
                .FirstOrDefaultAsync(o => o.OpportunityId == id);

            if (opp == null) return null;

            var followUps = await _context.FollowUps
                .Include(f => f.AssignedUser)
                .Where(f => f.OpportunityId == id)
                .OrderByDescending(f => f.FollowUpDate)
                .ToListAsync();

            return new OpportunityDetailsViewModel
            {
                Opportunity = opp,
                FollowUps = followUps
            };
        }

        public async Task<Opportunity> CreateOpportunityAsync(ClaimsPrincipal principal, CreateOpportunityDto dto, string? ipAddress)
        {
            var currentUser = await _userScopeService.GetCurrentUserAsync(principal);
            if (currentUser == null)
                throw new UnauthorizedAccessException("User is not authenticated.");

            // Business rule: Amount > 0 for active sales opportunities
            if (dto.Amount <= 0)
                throw new InvalidOperationException("Opportunity Amount must be greater than 0.");

            // Business rule: Probability 0 to 100
            if (dto.Probability < 0 || dto.Probability > 100)
                throw new InvalidOperationException("Probability must be between 0 and 100.");

            // Business rule: ExpectedCloseDate cannot be in the past for active opportunities
            var isClosed = OpportunityStages.IsClosed(dto.Stage);
            if (!isClosed && dto.ExpectedCloseDate.Date < DateTime.Today)
                throw new InvalidOperationException("Expected Close Date cannot be in the past for active opportunities.");

            // Verify customer authorization
            if (!await _userScopeService.CanAccessCustomerAsync(principal, dto.CustomerId))
                throw new UnauthorizedAccessException("Customer not found or not accessible within your role scope.");

            // Verify lead if provided
            if (dto.LeadId.HasValue && !await _userScopeService.CanAccessLeadAsync(principal, dto.LeadId.Value))
                throw new UnauthorizedAccessException("Lead not found or not accessible within your role scope.");

            var role = await _userScopeService.GetPrimaryRoleAsync(currentUser);
            var assignedTo = currentUser.Id;
            if (role != RoleConstants.SalesExecutive && !string.IsNullOrWhiteSpace(dto.AssignedTo))
            {
                var accessible = await _userScopeService.GetAccessibleUserIdsAsync(principal);
                if (accessible.Contains(dto.AssignedTo))
                    assignedTo = dto.AssignedTo;
            }

            var status = dto.Stage == OpportunityStages.Won ? "Won"
                       : dto.Stage == OpportunityStages.Lost ? "Lost"
                       : "Open";

            var opp = new Opportunity
            {
                OpportunityName = dto.OpportunityName.Trim(),
                CustomerId = dto.CustomerId,
                LeadId = dto.LeadId,
                Amount = dto.Amount,
                Stage = dto.Stage,
                Probability = dto.Probability,
                ExpectedCloseDate = dto.ExpectedCloseDate,
                Status = status,
                AssignedTo = assignedTo,
                Notes = dto.Notes?.Trim(),
                CreatedDate = DateTime.UtcNow,
                OutcomeDate = (dto.Stage is OpportunityStages.Won or OpportunityStages.Lost) ? DateTime.UtcNow : null
            };

            _context.Opportunities.Add(opp);
            await _context.SaveChangesAsync();

            await _auditLogService.LogAsync(
                currentUser.Id,
                currentUser.Email,
                AuditActions.Create,
                nameof(Opportunity),
                opp.OpportunityId.ToString(),
                null,
                new { opp.OpportunityName, opp.CustomerId, opp.Amount, opp.Stage, opp.Probability, opp.ExpectedCloseDate, opp.AssignedTo },
                "Success",
                $"Opportunity '{opp.OpportunityName}' created.",
                ipAddress);

            return opp;
        }

        public async Task<Opportunity> UpdateOpportunityAsync(ClaimsPrincipal principal, int id, UpdateOpportunityDto dto, string? ipAddress)
        {
            var currentUser = await _userScopeService.GetCurrentUserAsync(principal);
            if (currentUser == null || !await _userScopeService.CanAccessOpportunityAsync(principal, id))
                throw new UnauthorizedAccessException("You do not have permission to modify this opportunity.");

            var opp = await _context.Opportunities.FindAsync(id);
            if (opp == null)
                throw new KeyNotFoundException($"Opportunity with ID {id} not found.");

            // Business rule: Amount > 0 for active sales opportunities
            if (dto.Amount <= 0)
                throw new InvalidOperationException("Opportunity Amount must be greater than 0.");

            // Business rule: Probability 0 to 100
            if (dto.Probability < 0 || dto.Probability > 100)
                throw new InvalidOperationException("Probability must be between 0 and 100.");

            var isClosed = OpportunityStages.IsClosed(dto.Stage);
            if (!isClosed && dto.ExpectedCloseDate.Date < DateTime.Today)
                throw new InvalidOperationException("Expected Close Date cannot be in the past for active opportunities.");

            if (!await _userScopeService.CanAccessCustomerAsync(principal, dto.CustomerId))
                throw new UnauthorizedAccessException("Invalid customer selection.");

            var role = await _userScopeService.GetPrimaryRoleAsync(currentUser);
            var oldState = new { opp.OpportunityName, opp.Amount, opp.Stage, opp.Probability, opp.Status, opp.ExpectedCloseDate, opp.AssignedTo };

            opp.OpportunityName = dto.OpportunityName.Trim();
            opp.CustomerId = dto.CustomerId;
            opp.LeadId = dto.LeadId;
            opp.Amount = dto.Amount;
            opp.Stage = dto.Stage;
            opp.Probability = dto.Probability;
            opp.ExpectedCloseDate = dto.ExpectedCloseDate;
            opp.Notes = dto.Notes?.Trim();

            // Set outcome date when Won or Lost
            if (dto.Stage == OpportunityStages.Won)
            {
                opp.Status = "Won";
                if (!opp.OutcomeDate.HasValue) opp.OutcomeDate = DateTime.UtcNow;
            }
            else if (dto.Stage == OpportunityStages.Lost)
            {
                opp.Status = "Lost";
                if (!opp.OutcomeDate.HasValue) opp.OutcomeDate = DateTime.UtcNow;
            }
            else
            {
                opp.Status = "Open";
                opp.OutcomeDate = null;
            }

            if (role != RoleConstants.SalesExecutive && !string.IsNullOrWhiteSpace(dto.AssignedTo))
            {
                var accessible = await _userScopeService.GetAccessibleUserIdsAsync(principal);
                if (accessible.Contains(dto.AssignedTo))
                    opp.AssignedTo = dto.AssignedTo;
            }

            await _context.SaveChangesAsync();

            await _auditLogService.LogAsync(
                currentUser.Id,
                currentUser.Email,
                AuditActions.Update,
                nameof(Opportunity),
                opp.OpportunityId.ToString(),
                oldState,
                new { opp.OpportunityName, opp.Amount, opp.Stage, opp.Probability, opp.Status, opp.ExpectedCloseDate, opp.AssignedTo },
                "Success",
                $"Opportunity '{opp.OpportunityName}' updated to Stage: {opp.Stage}.",
                ipAddress);

            return opp;
        }

        public async Task<bool> DeleteOpportunityAsync(ClaimsPrincipal principal, int id, string? ipAddress)
        {
            var currentUser = await _userScopeService.GetCurrentUserAsync(principal);
            if (currentUser == null || !await _userScopeService.CanAccessOpportunityAsync(principal, id))
                throw new UnauthorizedAccessException("You do not have permission to delete this opportunity.");

            var opp = await _context.Opportunities.FindAsync(id);
            if (opp == null) return false;

            var oldState = new { opp.OpportunityName, opp.Amount, opp.Stage, opp.AssignedTo };

            _context.Opportunities.Remove(opp);
            await _context.SaveChangesAsync();

            await _auditLogService.LogAsync(
                currentUser.Id,
                currentUser.Email,
                AuditActions.Delete,
                nameof(Opportunity),
                id.ToString(),
                oldState,
                null,
                "Success",
                $"Opportunity '{opp.OpportunityName}' deleted.",
                ipAddress);

            return true;
        }

        public async Task<PipelineReportResponseDto> GetPipelineReportAsync(ClaimsPrincipal principal)
        {
            var accessibleUserIds = await _userScopeService.GetAccessibleUserIdsAsync(principal);
            var openOpps = await _context.Opportunities
                .Include(o => o.AssignedUser)
                .Where(o => accessibleUserIds.Contains(o.AssignedTo) && o.Status == "Open")
                .ToListAsync();

            var totalAmount = openOpps.Sum(o => o.Amount);
            var totalWeighted = openOpps.Sum(o => o.WeightedAmount);

            var stageGroups = openOpps
                .GroupBy(o => o.Stage)
                .Select(g => new StageSummaryDto
                {
                    Stage = g.Key,
                    Count = g.Count(),
                    TotalAmount = g.Sum(x => x.Amount),
                    WeightedAmount = g.Sum(x => x.WeightedAmount)
                })
                .ToList();

            var ownerGroups = openOpps
                .GroupBy(o => new { o.AssignedTo, Name = o.AssignedUser != null ? o.AssignedUser.FullName : "Unassigned" })
                .Select(g => new OwnerSummaryDto
                {
                    OwnerId = g.Key.AssignedTo,
                    OwnerName = g.Key.Name,
                    Count = g.Count(),
                    TotalAmount = g.Sum(x => x.Amount),
                    WeightedAmount = g.Sum(x => x.WeightedAmount)
                })
                .ToList();

            return new PipelineReportResponseDto
            {
                TotalPipelineAmount = totalAmount,
                TotalWeightedAmount = totalWeighted,
                TotalOpportunities = openOpps.Count,
                Stages = stageGroups,
                Owners = ownerGroups
            };
        }
    }
}
