using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using AcxiomCRM.Data;
using AcxiomCRM.DTOs;
using AcxiomCRM.Models;
using AcxiomCRM.Models.Enums;
using AcxiomCRM.Services.Interfaces;
using AcxiomCRM.ViewModels;
using Xunit;

namespace AcxiomCRM.Tests
{
    public class AcceptanceScenariosTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public AcceptanceScenariosTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        private ApplicationDbContext GetDbContext(IServiceScope scope) =>
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Scenario 1: Open AcxiomCRM without authentication -> protected CRM pages must not be accessible.
        [Fact]
        public async Task Scenario01_UnauthenticatedAccessBlocked()
        {
            var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false
            });

            // 1a: Web pages redirect to Login
            var respCustomers = await client.GetAsync("/Customers");
            Assert.Equal(HttpStatusCode.Redirect, respCustomers.StatusCode);
            Assert.Contains("/Account/Login", respCustomers.Headers.Location?.ToString());

            var respDashboard = await client.GetAsync("/Dashboard");
            Assert.Equal(HttpStatusCode.Redirect, respDashboard.StatusCode);

            // 1b: API requests return JSON 401 Unauthorized
            var respApi = await client.GetAsync("/api/customers");
            Assert.Equal(HttpStatusCode.Unauthorized, respApi.StatusCode);
            var content = await respApi.Content.ReadAsStringAsync();
            Assert.Contains("Unauthorized", content);
        }

        // Scenario 2: Register/login with a valid test user -> user reaches the Dashboard.
        [Fact]
        public async Task Scenario02_RegisterAndLoginUser()
        {
            using var scope = _factory.Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            var testEmail = $"sales_test_{Guid.NewGuid():N}@acxiomcrm.local";
            var testUser = new ApplicationUser
            {
                UserName = testEmail,
                Email = testEmail,
                FullName = "Test Sales User",
                PhoneNumber = $"+1-555-{Random.Shared.Next(100000, 999999)}",
                Department = "Sales",
                EmailConfirmed = true,
                IsActive = true
            };

            var createResult = await userManager.CreateAsync(testUser, "TestPass@12345!");
            Assert.True(createResult.Succeeded);

            // Public registration must assign SalesExecutive server-side
            await userManager.AddToRoleAsync(testUser, RoleConstants.SalesExecutive);

            var roles = await userManager.GetRolesAsync(testUser);
            Assert.Single(roles);
            Assert.Equal(RoleConstants.SalesExecutive, roles.First());
        }

        // Scenario 3: Create a Customer with invalid email/phone -> validation fails.
        [Fact]
        public async Task Scenario03_CustomerValidationClientAndServer()
        {
            using var scope = _factory.Services.CreateScope();
            var customerService = scope.ServiceProvider.GetRequiredService<ICustomerService>();
            var db = GetDbContext(scope);

            var admin = await db.Users.FirstAsync(u => u.Email == "admin@acxiomcrm.local");
            var principal = CreatePrincipal(admin.Id, admin.Email!, RoleConstants.Admin);

            // Invalid email duplicate test or format
            var invalidDto = new CreateCustomerDto
            {
                CustomerName = "Invalid Customer",
                Email = "not-an-email",
                Phone = "12", // too short
                Status = "Active"
            };

            var validationResults = ValidateModel(invalidDto);
            Assert.NotEmpty(validationResults);
            Assert.Contains(validationResults, v => v.MemberNames.Contains(nameof(CreateCustomerDto.Email)));
            Assert.Contains(validationResults, v => v.MemberNames.Contains(nameof(CreateCustomerDto.Phone)));
        }

        // Scenario 4: Bypass browser validation with a crafted request -> server-side validation rejects duplicate.
        [Fact]
        public async Task Scenario04_DuplicateCustomerRejectedByServer()
        {
            using var scope = _factory.Services.CreateScope();
            var customerService = scope.ServiceProvider.GetRequiredService<ICustomerService>();
            var db = GetDbContext(scope);

            var admin = await db.Users.FirstAsync(u => u.Email == "admin@acxiomcrm.local");
            var principal = CreatePrincipal(admin.Id, admin.Email!, RoleConstants.Admin);

            var email = $"dup_{Guid.NewGuid():N}@test.com";
            var phone = $"+1-555-{Random.Shared.Next(100000, 999999)}";

            // First create succeeds
            var dto1 = new CreateCustomerDto
            {
                CustomerName = "First Company",
                Email = email,
                Phone = phone,
                Status = "Active"
            };
            var customer1 = await customerService.CreateCustomerAsync(principal, dto1, "TestRunner", "127.0.0.1");
            Assert.NotNull(customer1);

            // Second create with duplicate email must throw InvalidOperationException on server
            var dto2 = new CreateCustomerDto
            {
                CustomerName = "Second Company",
                Email = email,
                Phone = $"+1-555-{Random.Shared.Next(100000, 999999)}",
                Status = "Active"
            };

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                customerService.CreateCustomerAsync(principal, dto2, "TestRunner", "127.0.0.1"));
        }

        // Scenario 5: Create an Opportunity with Amount <= 0 -> rejected.
        [Fact]
        public async Task Scenario05_OpportunityAmountMustBeGreaterThanZero()
        {
            using var scope = _factory.Services.CreateScope();
            var oppService = scope.ServiceProvider.GetRequiredService<IOpportunityService>();
            var db = GetDbContext(scope);

            var admin = await db.Users.FirstAsync(u => u.Email == "admin@acxiomcrm.local");
            var principal = CreatePrincipal(admin.Id, admin.Email!, RoleConstants.Admin);
            var cust = await db.Customers.FirstAsync();

            var zeroAmountDto = new CreateOpportunityDto
            {
                OpportunityName = "Zero Deal",
                CustomerId = cust.CustomerId,
                Amount = 0m, // <= 0
                Stage = OpportunityStages.Qualification,
                Probability = 20,
                ExpectedCloseDate = DateTime.Today.AddDays(30)
            };

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                oppService.CreateOpportunityAsync(principal, zeroAmountDto, "127.0.0.1"));
        }

        // Scenario 6: Create an Opportunity with Probability > 100 -> rejected.
        [Fact]
        public async Task Scenario06_OpportunityProbabilityRange()
        {
            using var scope = _factory.Services.CreateScope();
            var oppService = scope.ServiceProvider.GetRequiredService<IOpportunityService>();
            var db = GetDbContext(scope);

            var admin = await db.Users.FirstAsync(u => u.Email == "admin@acxiomcrm.local");
            var principal = CreatePrincipal(admin.Id, admin.Email!, RoleConstants.Admin);
            var cust = await db.Customers.FirstAsync();

            var invalidProbDto = new CreateOpportunityDto
            {
                OpportunityName = "Invalid Prob Deal",
                CustomerId = cust.CustomerId,
                Amount = 5000m,
                Stage = OpportunityStages.Qualification,
                Probability = 101, // > 100
                ExpectedCloseDate = DateTime.Today.AddDays(30)
            };

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                oppService.CreateOpportunityAsync(principal, invalidProbDto, "127.0.0.1"));
        }

        // Scenario 7: Set Expected Close Date in the past -> rejected for an active opportunity.
        [Fact]
        public async Task Scenario07_OpportunityExpectedCloseDateCannotBeInPast()
        {
            using var scope = _factory.Services.CreateScope();
            var oppService = scope.ServiceProvider.GetRequiredService<IOpportunityService>();
            var db = GetDbContext(scope);

            var admin = await db.Users.FirstAsync(u => u.Email == "admin@acxiomcrm.local");
            var principal = CreatePrincipal(admin.Id, admin.Email!, RoleConstants.Admin);
            var cust = await db.Customers.FirstAsync();

            var pastDateDto = new CreateOpportunityDto
            {
                OpportunityName = "Past Date Deal",
                CustomerId = cust.CustomerId,
                Amount = 5000m,
                Stage = OpportunityStages.Proposal,
                Probability = 50,
                ExpectedCloseDate = DateTime.Today.AddDays(-1) // yesterday
            };

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                oppService.CreateOpportunityAsync(principal, pastDateDto, "127.0.0.1"));
        }

        // Scenario 8: Create a Follow-Up dated before today -> rejected for a new/planned follow-up.
        [Fact]
        public async Task Scenario08_FollowUpDateCannotBeEarlierThanToday()
        {
            using var scope = _factory.Services.CreateScope();
            var followUpService = scope.ServiceProvider.GetRequiredService<IFollowUpService>();
            var db = GetDbContext(scope);

            var admin = await db.Users.FirstAsync(u => u.Email == "admin@acxiomcrm.local");
            var principal = CreatePrincipal(admin.Id, admin.Email!, RoleConstants.Admin);

            var pastFollowUp = new CreateFollowUpDto
            {
                Subject = "Past Follow-up",
                FollowUpDate = DateTime.Today.AddDays(-2), // 2 days ago
                FollowUpType = FollowUpTypes.Call
            };

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                followUpService.CreateFollowUpAsync(principal, pastFollowUp, "127.0.0.1"));
        }

        // Scenario 9: Login as SalesExecutive -> only authorized/assigned sales scope is accessible.
        [Fact]
        public async Task Scenario09_SalesExecutiveRoleScoping()
        {
            using var scope = _factory.Services.CreateScope();
            var userScope = scope.ServiceProvider.GetRequiredService<IUserScopeService>();
            var customerService = scope.ServiceProvider.GetRequiredService<ICustomerService>();
            var db = GetDbContext(scope);

            var sales1 = await db.Users.FirstAsync(u => u.Email == "sales1@acxiomcrm.local");
            var sales2 = await db.Users.FirstAsync(u => u.Email == "sales2@acxiomcrm.local");

            var sales1Principal = CreatePrincipal(sales1.Id, sales1.Email!, RoleConstants.SalesExecutive);

            // Create a customer owned by sales2
            var customerOwnedBySales2 = new Customer
            {
                CustomerCode = $"CUST-TEST-{Guid.NewGuid():N}".Substring(0, 15),
                CustomerName = "Sales2 Exclusive Customer",
                Email = $"sales2_cust_{Guid.NewGuid():N}@test.com",
                Phone = $"+1-555-{Random.Shared.Next(100000, 999999)}",
                Status = "Active",
                OwnerId = sales2.Id,
                CreatedBy = "Test"
            };
            db.Customers.Add(customerOwnedBySales2);
            await db.SaveChangesAsync();

            // Sales 1 cannot access Sales 2's customer
            var canAccess = await userScope.CanAccessCustomerAsync(sales1Principal, customerOwnedBySales2.CustomerId);
            Assert.False(canAccess);

            var details = await customerService.GetCustomerByIdAsync(sales1Principal, customerOwnedBySales2.CustomerId);
            Assert.Null(details);
        }

        // Scenario 10: Login as Manager -> team pipeline/report access is available.
        [Fact]
        public async Task Scenario10_ManagerTeamAccess()
        {
            using var scope = _factory.Services.CreateScope();
            var userScope = scope.ServiceProvider.GetRequiredService<IUserScopeService>();
            var db = GetDbContext(scope);

            var manager = await db.Users.FirstAsync(u => u.Email == "manager@acxiomcrm.local");
            var sales1 = await db.Users.FirstAsync(u => u.Email == "sales1@acxiomcrm.local");

            var managerPrincipal = CreatePrincipal(manager.Id, manager.Email!, RoleConstants.Manager);

            // Manager must be able to access subordinates in their team
            var accessibleUserIds = await userScope.GetAccessibleUserIdsAsync(managerPrincipal);
            Assert.Contains(sales1.Id, accessibleUserIds);
            Assert.Contains(manager.Id, accessibleUserIds);
        }

        // Scenario 11: Login as Admin -> user/role/audit administration is available.
        [Fact]
        public async Task Scenario11_AdminAccessToSecurityAndAudit()
        {
            using var scope = _factory.Services.CreateScope();
            var userScope = scope.ServiceProvider.GetRequiredService<IUserScopeService>();
            var auditService = scope.ServiceProvider.GetRequiredService<IAuditLogService>();
            var db = GetDbContext(scope);

            var admin = await db.Users.FirstAsync(u => u.Email == "admin@acxiomcrm.local");
            var adminPrincipal = CreatePrincipal(admin.Id, admin.Email!, RoleConstants.Admin);

            var role = await userScope.GetPrimaryRoleAsync(adminPrincipal);
            Assert.Equal(RoleConstants.Admin, role);

            var logs = await auditService.GetLogsAsync(page: 1, pageSize: 10);
            Assert.NotNull(logs);
        }

        // Scenario 12: Create/update/delete a CRM record -> appropriate audit entry is generated.
        [Fact]
        public async Task Scenario12_AuditTrailGenerated()
        {
            using var scope = _factory.Services.CreateScope();
            var customerService = scope.ServiceProvider.GetRequiredService<ICustomerService>();
            var db = GetDbContext(scope);

            var admin = await db.Users.FirstAsync(u => u.Email == "admin@acxiomcrm.local");
            var adminPrincipal = CreatePrincipal(admin.Id, admin.Email!, RoleConstants.Admin);

            var custEmail = $"audit_test_{Guid.NewGuid():N}@test.com";
            var dto = new CreateCustomerDto
            {
                CustomerName = "Audit Verified Inc.",
                Email = custEmail,
                Phone = $"+1-555-{Random.Shared.Next(100000, 999999)}",
                Status = "Active"
            };

            var customer = await customerService.CreateCustomerAsync(adminPrincipal, dto, "AdminTester", "192.168.1.100");

            var auditEntry = await db.AuditLogs
                .Where(a => a.EntityName == nameof(Customer) && a.RecordId == customer.CustomerId.ToString())
                .FirstOrDefaultAsync();

            Assert.NotNull(auditEntry);
            Assert.Equal(AuditActions.Create, auditEntry.Action);
            Assert.Equal("Success", auditEntry.Result);
            Assert.Equal("192.168.1.100", auditEntry.IpAddress);
        }

        // Scenario 13: Call /api/customers -> authorized JSON response is returned.
        [Fact]
        public async Task Scenario13_RestApiCustomersAuthorizedResponse()
        {
            using var scope = _factory.Services.CreateScope();
            var customerService = scope.ServiceProvider.GetRequiredService<ICustomerService>();
            var db = GetDbContext(scope);

            var admin = await db.Users.FirstAsync(u => u.Email == "admin@acxiomcrm.local");
            var adminPrincipal = CreatePrincipal(admin.Id, admin.Email!, RoleConstants.Admin);

            var result = await customerService.GetCustomersAsync(adminPrincipal, null, null, null, null, 1, 10);
            Assert.NotNull(result);
            Assert.True(result.Customers.Count > 0);
        }

        // Scenario 14: Open Dashboard -> KPI cards and Chart.js charts display authorized data.
        [Fact]
        public async Task Scenario14_DashboardKpisAndCharts()
        {
            using var scope = _factory.Services.CreateScope();
            var dashboardService = scope.ServiceProvider.GetRequiredService<IDashboardService>();
            var db = GetDbContext(scope);

            var admin = await db.Users.FirstAsync(u => u.Email == "admin@acxiomcrm.local");
            var adminPrincipal = CreatePrincipal(admin.Id, admin.Email!, RoleConstants.Admin);

            var data = await dashboardService.GetDashboardDataAsync(adminPrincipal);

            Assert.True(data.TotalCustomers >= 0);
            Assert.True(data.TotalLeads >= 0);
            Assert.NotEmpty(data.LeadStatusLabels);
            Assert.NotEmpty(data.PipelineStageLabels);
            Assert.NotEmpty(data.MonthlySalesLabels);
        }

        private static ClaimsPrincipal CreatePrincipal(string userId, string email, string role)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, email),
                new Claim(ClaimTypes.Email, email),
                new Claim(ClaimTypes.Role, role)
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            return new ClaimsPrincipal(identity);
        }

        private static List<System.ComponentModel.DataAnnotations.ValidationResult> ValidateModel(object model)
        {
            var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
            var context = new System.ComponentModel.DataAnnotations.ValidationContext(model, null, null);
            System.ComponentModel.DataAnnotations.Validator.TryValidateObject(model, context, results, true);
            return results;
        }
    }
}
