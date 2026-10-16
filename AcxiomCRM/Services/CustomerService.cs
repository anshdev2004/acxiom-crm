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
    public class CustomerService : ICustomerService
    {
        private readonly ApplicationDbContext _context;
        private readonly IUserScopeService _userScopeService;
        private readonly IAuditLogService _auditLogService;
        private readonly UserManager<ApplicationUser> _userManager;

        public CustomerService(
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

        public async Task<CustomerListViewModel> GetCustomersAsync(
            ClaimsPrincipal principal,
            string? searchTerm,
            string? status,
            string? ownerId,
            string? sortBy,
            int page,
            int pageSize)
        {
            var accessibleUserIds = await _userScopeService.GetAccessibleUserIdsAsync(principal);
            var query = _context.Customers
                .Include(c => c.Owner)
                .Where(c => accessibleUserIds.Contains(c.OwnerId));

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim().ToLower();
                query = query.Where(c =>
                    c.CustomerName.ToLower().Contains(term) ||
                    c.Email.ToLower().Contains(term) ||
                    c.Phone.ToLower().Contains(term) ||
                    (c.CompanyName != null && c.CompanyName.ToLower().Contains(term)) ||
                    c.CustomerCode.ToLower().Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(c => c.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(ownerId) && accessibleUserIds.Contains(ownerId))
            {
                query = query.Where(c => c.OwnerId == ownerId);
            }

            query = sortBy switch
            {
                "NameDesc" => query.OrderByDescending(c => c.CustomerName),
                "DateAsc" => query.OrderBy(c => c.CreatedDate),
                "DateDesc" => query.OrderByDescending(c => c.CreatedDate),
                "CompanyAsc" => query.OrderBy(c => c.CompanyName),
                _ => query.OrderBy(c => c.CustomerName)
            };

            var totalCount = await query.CountAsync();
            var customers = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var currentRole = await _userScopeService.GetPrimaryRoleAsync(principal);
            var availableOwners = new List<SelectListItem>();
            if (currentRole != RoleConstants.SalesExecutive)
            {
                var users = await _userManager.Users
                    .Where(u => accessibleUserIds.Contains(u.Id))
                    .OrderBy(u => u.FullName)
                    .Select(u => new SelectListItem { Value = u.Id, Text = $"{u.FullName} ({u.Email})" })
                    .ToListAsync();
                availableOwners = users;
            }

            return new CustomerListViewModel
            {
                Customers = customers,
                SearchTerm = searchTerm,
                StatusFilter = status,
                OwnerFilter = ownerId,
                SortBy = sortBy,
                CurrentPage = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                AvailableOwners = availableOwners
            };
        }

        public async Task<Customer?> GetCustomerByIdAsync(ClaimsPrincipal principal, int id)
        {
            if (!await _userScopeService.CanAccessCustomerAsync(principal, id))
                return null;

            return await _context.Customers
                .Include(c => c.Owner)
                .FirstOrDefaultAsync(c => c.CustomerId == id);
        }

        public async Task<CustomerDetailsViewModel?> GetCustomerDetailsAsync(ClaimsPrincipal principal, int id)
        {
            if (!await _userScopeService.CanAccessCustomerAsync(principal, id))
                return null;

            var customer = await _context.Customers
                .Include(c => c.Owner)
                .FirstOrDefaultAsync(c => c.CustomerId == id);

            if (customer == null) return null;

            var opportunities = await _context.Opportunities
                .Include(o => o.AssignedUser)
                .Where(o => o.CustomerId == id)
                .OrderByDescending(o => o.CreatedDate)
                .ToListAsync();

            var convertedLeads = await _context.Leads
                .Include(l => l.AssignedUser)
                .Where(l => l.ConvertedCustomerId == id)
                .OrderByDescending(l => l.CreatedDate)
                .ToListAsync();

            var followUps = await _context.FollowUps
                .Include(f => f.AssignedUser)
                .Where(f => f.CustomerId == id)
                .OrderByDescending(f => f.FollowUpDate)
                .ToListAsync();

            var activities = await _context.Activities
                .Include(a => a.AssignedUser)
                .Where(a => a.CustomerId == id)
                .OrderByDescending(a => a.ActivityDate)
                .ToListAsync();

            return new CustomerDetailsViewModel
            {
                Customer = customer,
                Opportunities = opportunities,
                ConvertedLeads = convertedLeads,
                FollowUps = followUps,
                Activities = activities
            };
        }

        public async Task<Customer> CreateCustomerAsync(ClaimsPrincipal principal, CreateCustomerDto dto, string createdBy, string? ipAddress)
        {
            var currentUser = await _userScopeService.GetCurrentUserAsync(principal);
            if (currentUser == null)
                throw new UnauthorizedAccessException("User is not authenticated.");

            var role = await _userScopeService.GetPrimaryRoleAsync(currentUser);

            // Determine owner: SalesExecutive can only assign to themselves
            var ownerId = currentUser.Id;
            if (role != RoleConstants.SalesExecutive && !string.IsNullOrWhiteSpace(dto.OwnerId))
            {
                var accessible = await _userScopeService.GetAccessibleUserIdsAsync(principal);
                if (accessible.Contains(dto.OwnerId))
                {
                    ownerId = dto.OwnerId;
                }
            }

            // Uniqueness validation on normalized values
            var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
            var normalizedPhone = dto.Phone.Trim();

            if (!await IsEmailUniqueAsync(normalizedEmail))
                throw new InvalidOperationException($"A customer with email '{dto.Email}' already exists.");

            if (!await IsPhoneUniqueAsync(normalizedPhone))
                throw new InvalidOperationException($"A customer with phone '{dto.Phone}' already exists.");

            // Generate unique customer code
            var nextNum = (await _context.Customers.MaxAsync(c => (int?)c.CustomerId) ?? 0) + 1;
            var customerCode = $"CUST-{nextNum:D4}";

            var customer = new Customer
            {
                CustomerCode = customerCode,
                CustomerName = dto.CustomerName.Trim(),
                Email = normalizedEmail,
                Phone = normalizedPhone,
                CompanyName = dto.CompanyName?.Trim(),
                Address = dto.Address?.Trim(),
                City = dto.City?.Trim(),
                State = dto.State?.Trim(),
                Status = string.IsNullOrWhiteSpace(dto.Status) ? "Active" : dto.Status,
                OwnerId = ownerId,
                CreatedBy = createdBy,
                CreatedDate = DateTime.UtcNow,
                IsActive = true
            };

            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();

            await _auditLogService.LogAsync(
                currentUser.Id,
                currentUser.Email,
                AuditActions.Create,
                nameof(Customer),
                customer.CustomerId.ToString(),
                null,
                new { customer.CustomerCode, customer.CustomerName, customer.Email, customer.Phone, customer.OwnerId },
                "Success",
                $"Customer {customer.CustomerCode} created.",
                ipAddress);

            return customer;
        }

        public async Task<Customer> UpdateCustomerAsync(ClaimsPrincipal principal, int id, UpdateCustomerDto dto, string? ipAddress)
        {
            var currentUser = await _userScopeService.GetCurrentUserAsync(principal);
            if (currentUser == null)
                throw new UnauthorizedAccessException("User is not authenticated.");

            if (!await _userScopeService.CanAccessCustomerAsync(principal, id))
                throw new UnauthorizedAccessException("You do not have permission to modify this customer.");

            var customer = await _context.Customers.FindAsync(id);
            if (customer == null)
                throw new KeyNotFoundException($"Customer with ID {id} not found.");

            var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
            var normalizedPhone = dto.Phone.Trim();

            if (!await IsEmailUniqueAsync(normalizedEmail, id))
                throw new InvalidOperationException($"Another customer with email '{dto.Email}' already exists.");

            if (!await IsPhoneUniqueAsync(normalizedPhone, id))
                throw new InvalidOperationException($"Another customer with phone '{dto.Phone}' already exists.");

            var role = await _userScopeService.GetPrimaryRoleAsync(currentUser);
            var oldState = new { customer.CustomerName, customer.Email, customer.Phone, customer.CompanyName, customer.Status, customer.OwnerId, customer.IsActive };

            customer.CustomerName = dto.CustomerName.Trim();
            customer.Email = normalizedEmail;
            customer.Phone = normalizedPhone;
            customer.CompanyName = dto.CompanyName?.Trim();
            customer.Address = dto.Address?.Trim();
            customer.City = dto.City?.Trim();
            customer.State = dto.State?.Trim();
            customer.Status = dto.Status;
            customer.IsActive = dto.IsActive;
            customer.ModifiedDate = DateTime.UtcNow;

            // Privilege check for changing owner
            if (role != RoleConstants.SalesExecutive && !string.IsNullOrWhiteSpace(dto.OwnerId))
            {
                var accessible = await _userScopeService.GetAccessibleUserIdsAsync(principal);
                if (accessible.Contains(dto.OwnerId))
                {
                    customer.OwnerId = dto.OwnerId;
                }
            }

            await _context.SaveChangesAsync();

            await _auditLogService.LogAsync(
                currentUser.Id,
                currentUser.Email,
                AuditActions.Update,
                nameof(Customer),
                customer.CustomerId.ToString(),
                oldState,
                new { customer.CustomerName, customer.Email, customer.Phone, customer.CompanyName, customer.Status, customer.OwnerId, customer.IsActive },
                "Success",
                $"Customer {customer.CustomerCode} updated.",
                ipAddress);

            return customer;
        }

        public async Task<bool> DeleteOrDeactivateCustomerAsync(ClaimsPrincipal principal, int id, string? ipAddress)
        {
            var currentUser = await _userScopeService.GetCurrentUserAsync(principal);
            if (currentUser == null || !await _userScopeService.CanAccessCustomerAsync(principal, id))
                throw new UnauthorizedAccessException("You do not have permission to deactivate or delete this customer.");

            var customer = await _context.Customers.FindAsync(id);
            if (customer == null) return false;

            // Check if customer has opportunities or activities. If so, soft-deactivate to maintain CRM history
            var hasRelated = await _context.Opportunities.AnyAsync(o => o.CustomerId == id)
                             || await _context.FollowUps.AnyAsync(f => f.CustomerId == id);

            var oldState = new { customer.CustomerCode, customer.CustomerName, customer.Status, customer.IsActive };

            if (hasRelated)
            {
                customer.IsActive = false;
                customer.Status = "Inactive";
                customer.ModifiedDate = DateTime.UtcNow;
            }
            else
            {
                _context.Customers.Remove(customer);
            }

            await _context.SaveChangesAsync();

            await _auditLogService.LogAsync(
                currentUser.Id,
                currentUser.Email,
                AuditActions.Delete,
                nameof(Customer),
                id.ToString(),
                oldState,
                hasRelated ? new { IsActive = false, Status = "Inactive" } : null,
                "Success",
                hasRelated ? $"Customer {customer.CustomerCode} deactivated due to existing CRM records." : $"Customer {customer.CustomerCode} deleted.",
                ipAddress);

            return true;
        }

        public async Task<bool> IsEmailUniqueAsync(string email, int? excludeCustomerId = null)
        {
            var query = _context.Customers.Where(c => c.Email.ToLower() == email.Trim().ToLower());
            if (excludeCustomerId.HasValue)
                query = query.Where(c => c.CustomerId != excludeCustomerId.Value);

            return !await query.AnyAsync();
        }

        public async Task<bool> IsPhoneUniqueAsync(string phone, int? excludeCustomerId = null)
        {
            var query = _context.Customers.Where(c => c.Phone == phone.Trim());
            if (excludeCustomerId.HasValue)
                query = query.Where(c => c.CustomerId != excludeCustomerId.Value);

            return !await query.AnyAsync();
        }
    }
}
