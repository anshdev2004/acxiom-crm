using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Models;
using AcxiomCRM.Models.Enums;

namespace AcxiomCRM.Data
{
    public static class ApplicationDbContextSeed
    {
        public static async Task SeedAsync(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager,
            IConfiguration configuration)
        {
            // Apply any pending migrations automatically
            if (context.Database.IsRelational())
            {
                await context.Database.MigrateAsync();
            }
            else
            {
                await context.Database.EnsureCreatedAsync();
            }

            // 1. Seed Roles
            foreach (var roleName in RoleConstants.AllRoles)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    var desc = roleName switch
                    {
                        RoleConstants.Admin => "Full application administrator with security, audit, and CRM control.",
                        RoleConstants.Manager => "Team sales manager monitoring team pipeline and reporting.",
                        _ => "Frontline sales executive managing assigned customers and leads."
                    };
                    await roleManager.CreateAsync(new ApplicationRole(roleName, desc));
                }
            }

            // Read seed credentials from config (safe defaults for local demonstration)
            var adminEmail = configuration["SeedData:AdminEmail"] ?? "admin@acxiomcrm.local";
            var adminPassword = configuration["SeedData:AdminPassword"] ?? "Admin@12345!";

            var managerEmail = configuration["SeedData:ManagerEmail"] ?? "manager@acxiomcrm.local";
            var managerPassword = configuration["SeedData:ManagerPassword"] ?? "Manager@12345!";

            var sales1Email = configuration["SeedData:Sales1Email"] ?? "sales1@acxiomcrm.local";
            var sales1Password = configuration["SeedData:Sales1Password"] ?? "Sales@12345!";

            var sales2Email = configuration["SeedData:Sales2Email"] ?? "sales2@acxiomcrm.local";
            var sales2Password = configuration["SeedData:Sales2Password"] ?? "Sales@12345!";

            // 2. Seed Admin User
            var admin = await userManager.FindByEmailAsync(adminEmail);
            if (admin == null)
            {
                admin = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FullName = "System Administrator",
                    Department = "Executive & IT",
                    PhoneNumber = "+1-555-0100",
                    EmailConfirmed = true,
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow
                };
                var result = await userManager.CreateAsync(admin, adminPassword);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(admin, RoleConstants.Admin);
                }
            }

            // 3. Seed Manager User
            var manager = await userManager.FindByEmailAsync(managerEmail);
            if (manager == null)
            {
                manager = new ApplicationUser
                {
                    UserName = managerEmail,
                    Email = managerEmail,
                    FullName = "Sarah Connor (Sales Manager)",
                    Department = "Enterprise Sales",
                    PhoneNumber = "+1-555-0101",
                    EmailConfirmed = true,
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow
                };
                var result = await userManager.CreateAsync(manager, managerPassword);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(manager, RoleConstants.Manager);
                }
            }

            // 4. Seed Sales Executive 1
            var sales1 = await userManager.FindByEmailAsync(sales1Email);
            if (sales1 == null)
            {
                sales1 = new ApplicationUser
                {
                    UserName = sales1Email,
                    Email = sales1Email,
                    FullName = "Alex Morgan",
                    Department = "Regional Sales",
                    PhoneNumber = "+1-555-0102",
                    ManagerId = manager?.Id,
                    EmailConfirmed = true,
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow
                };
                var result = await userManager.CreateAsync(sales1, sales1Password);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(sales1, RoleConstants.SalesExecutive);
                }
            }

            // 5. Seed Sales Executive 2
            var sales2 = await userManager.FindByEmailAsync(sales2Email);
            if (sales2 == null)
            {
                sales2 = new ApplicationUser
                {
                    UserName = sales2Email,
                    Email = sales2Email,
                    FullName = "Jordan Lee",
                    Department = "Regional Sales",
                    PhoneNumber = "+1-555-0103",
                    ManagerId = manager?.Id,
                    EmailConfirmed = true,
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow
                };
                var result = await userManager.CreateAsync(sales2, sales2Password);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(sales2, RoleConstants.SalesExecutive);
                }
            }

            // 6. Seed CRM Sample Data if empty
            if (!await context.Customers.AnyAsync())
            {
                var cust1 = new Customer
                {
                    CustomerCode = "CUST-0001",
                    CustomerName = "Acme Global Solutions",
                    Email = "contact@acmeglobal.com",
                    Phone = "+1-555-1111",
                    CompanyName = "Acme Global Solutions",
                    Address = "100 Tech Boulevard",
                    City = "Seattle",
                    State = "WA",
                    Status = "Active",
                    OwnerId = sales1?.Id ?? admin.Id,
                    CreatedBy = "System Seed",
                    CreatedDate = DateTime.UtcNow.AddMonths(-3),
                    IsActive = true
                };

                var cust2 = new Customer
                {
                    CustomerCode = "CUST-0002",
                    CustomerName = "Nexus Cloud Technologies",
                    Email = "hello@nexuscloud.io",
                    Phone = "+1-555-2222",
                    CompanyName = "Nexus Cloud Technologies",
                    Address = "450 Silicon Way",
                    City = "San Jose",
                    State = "CA",
                    Status = "Active",
                    OwnerId = sales1?.Id ?? admin.Id,
                    CreatedBy = "System Seed",
                    CreatedDate = DateTime.UtcNow.AddMonths(-2),
                    IsActive = true
                };

                var cust3 = new Customer
                {
                    CustomerCode = "CUST-0003",
                    CustomerName = "Beacon Logistics Corp",
                    Email = "ops@beaconlogistics.com",
                    Phone = "+1-555-3333",
                    CompanyName = "Beacon Logistics Corp",
                    Address = "78 Industrial Parkway",
                    City = "Chicago",
                    State = "IL",
                    Status = "Active",
                    OwnerId = sales2?.Id ?? admin.Id,
                    CreatedBy = "System Seed",
                    CreatedDate = DateTime.UtcNow.AddMonths(-1),
                    IsActive = true
                };

                context.Customers.AddRange(cust1, cust2, cust3);
                await context.SaveChangesAsync();

                // Leads
                var lead1 = new Lead
                {
                    LeadCode = "LEAD-0001",
                    LeadName = "Finovate Capital",
                    Email = "inquiries@finovate.com",
                    Phone = "+1-555-4444",
                    CompanyName = "Finovate Capital",
                    Source = LeadSources.Website,
                    Status = LeadStatuses.Qualified,
                    Priority = LeadPriorities.High,
                    ExpectedValue = 45000.00m,
                    AssignedTo = sales1?.Id ?? admin.Id,
                    Notes = "High interest in enterprise CRM and analytics integration.",
                    CreatedDate = DateTime.UtcNow.AddDays(-20)
                };

                var lead2 = new Lead
                {
                    LeadCode = "LEAD-0002",
                    LeadName = "OmniCare Health",
                    Email = "partnerships@omnicare.org",
                    Phone = "+1-555-5555",
                    CompanyName = "OmniCare Health",
                    Source = LeadSources.Referral,
                    Status = LeadStatuses.Contacted,
                    Priority = LeadPriorities.Medium,
                    ExpectedValue = 28000.00m,
                    AssignedTo = sales1?.Id ?? admin.Id,
                    Notes = "Initial introductory call completed. Follow-up demo requested.",
                    CreatedDate = DateTime.UtcNow.AddDays(-10)
                };

                var lead3 = new Lead
                {
                    LeadCode = "LEAD-0003",
                    LeadName = "Starlight Media Group",
                    Email = "growth@starlightmedia.com",
                    Phone = "+1-555-6666",
                    CompanyName = "Starlight Media Group",
                    Source = LeadSources.LinkedIn,
                    Status = LeadStatuses.New,
                    Priority = LeadPriorities.Low,
                    ExpectedValue = 12000.00m,
                    AssignedTo = sales2?.Id ?? admin.Id,
                    Notes = "Inbound lead from sponsored content campaign.",
                    CreatedDate = DateTime.UtcNow.AddDays(-3)
                };

                context.Leads.AddRange(lead1, lead2, lead3);
                await context.SaveChangesAsync();

                // Opportunities
                var opp1 = new Opportunity
                {
                    OpportunityName = "Acme - Enterprise Annual License",
                    CustomerId = cust1.CustomerId,
                    Amount = 75000.00m,
                    Stage = OpportunityStages.Negotiation,
                    Probability = 80,
                    ExpectedCloseDate = DateTime.Today.AddDays(25),
                    Status = "Open",
                    AssignedTo = sales1?.Id ?? admin.Id,
                    Notes = "Contract redline stage with executive stakeholders.",
                    CreatedDate = DateTime.UtcNow.AddMonths(-1)
                };

                var opp2 = new Opportunity
                {
                    OpportunityName = "Nexus Cloud - Migration Consulting",
                    CustomerId = cust2.CustomerId,
                    Amount = 35000.00m,
                    Stage = OpportunityStages.Proposal,
                    Probability = 50,
                    ExpectedCloseDate = DateTime.Today.AddDays(40),
                    Status = "Open",
                    AssignedTo = sales1?.Id ?? admin.Id,
                    Notes = "Proposal delivered; waiting on procurement review.",
                    CreatedDate = DateTime.UtcNow.AddDays(-15)
                };

                var opp3 = new Opportunity
                {
                    OpportunityName = "Beacon Logistics - Pilot Implementation",
                    CustomerId = cust3.CustomerId,
                    Amount = 50000.00m,
                    Stage = OpportunityStages.Won,
                    Probability = 100,
                    ExpectedCloseDate = DateTime.Today.AddDays(-10),
                    Status = "Won",
                    OutcomeDate = DateTime.UtcNow.AddDays(-10),
                    AssignedTo = sales2?.Id ?? admin.Id,
                    Notes = "Master Services Agreement executed.",
                    CreatedDate = DateTime.UtcNow.AddMonths(-2)
                };

                context.Opportunities.AddRange(opp1, opp2, opp3);
                await context.SaveChangesAsync();

                // Follow-ups
                var fu1 = new FollowUp
                {
                    CustomerId = cust1.CustomerId,
                    OpportunityId = opp1.OpportunityId,
                    Subject = "Final terms review and legal signoff call",
                    FollowUpDate = DateTime.Today.AddDays(2).AddHours(14),
                    FollowUpType = FollowUpTypes.Meeting,
                    Remarks = "Confirm pricing schedule and SLA appendices.",
                    Status = FollowUpStatuses.Planned,
                    AssignedTo = sales1?.Id ?? admin.Id,
                    CreatedDate = DateTime.UtcNow
                };

                var fu2 = new FollowUp
                {
                    LeadId = lead2.LeadId,
                    Subject = "Send product architecture overview and pricing sheet",
                    FollowUpDate = DateTime.Today.AddDays(1).AddHours(11),
                    FollowUpType = FollowUpTypes.Email,
                    Remarks = "Address compliance questions raised during initial call.",
                    Status = FollowUpStatuses.Planned,
                    AssignedTo = sales1?.Id ?? admin.Id,
                    CreatedDate = DateTime.UtcNow
                };

                var fu3 = new FollowUp
                {
                    CustomerId = cust3.CustomerId,
                    OpportunityId = opp3.OpportunityId,
                    Subject = "Kickoff call and project handover",
                    FollowUpDate = DateTime.Today.AddDays(-5).AddHours(15),
                    FollowUpType = FollowUpTypes.Call,
                    Remarks = "Completed successfully. Engineering team looped in.",
                    Status = FollowUpStatuses.Completed,
                    CompletedDate = DateTime.UtcNow.AddDays(-5),
                    AssignedTo = sales2?.Id ?? admin.Id,
                    CreatedDate = DateTime.UtcNow.AddDays(-8)
                };

                context.FollowUps.AddRange(fu1, fu2, fu3);
                await context.SaveChangesAsync();

                // Activities
                var act1 = new Activity
                {
                    ActivityType = ActivityTypes.Call,
                    Subject = "Introductory discovery discussion",
                    Description = "Discussed business requirements, existing CRM pain points, and target go-live date.",
                    ActivityDate = DateTime.UtcNow.AddDays(-10),
                    LeadId = lead2.LeadId,
                    AssignedTo = sales1?.Id ?? admin.Id,
                    Status = "Completed",
                    CreatedDate = DateTime.UtcNow.AddDays(-10)
                };

                var act2 = new Activity
                {
                    ActivityType = ActivityTypes.Meeting,
                    Subject = "Contract review meeting with Acme legal counsel",
                    Description = "Reviewed confidentiality and indemnification clauses.",
                    ActivityDate = DateTime.UtcNow.AddDays(-4),
                    CustomerId = cust1.CustomerId,
                    AssignedTo = sales1?.Id ?? admin.Id,
                    Status = "Completed",
                    CreatedDate = DateTime.UtcNow.AddDays(-4)
                };

                context.Activities.AddRange(act1, act2);

                // Initial audit log
                context.AuditLogs.Add(new AuditLog
                {
                    UserId = admin.Id,
                    UserEmail = admin.Email,
                    Action = AuditActions.Security,
                    EntityName = "System",
                    RecordId = "0",
                    Result = "Success",
                    Details = "Initial database seed completed with sample roles, accounts, and CRM records.",
                    CreatedDate = DateTime.UtcNow
                });

                await context.SaveChangesAsync();
            }
        }
    }
}
