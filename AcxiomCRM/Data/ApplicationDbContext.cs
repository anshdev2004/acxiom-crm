using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using AcxiomCRM.Models;

namespace AcxiomCRM.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, string>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<Lead> Leads => Set<Lead>();
        public DbSet<Opportunity> Opportunities => Set<Opportunity>();
        public DbSet<FollowUp> FollowUps => Set<FollowUp>();
        public DbSet<Activity> Activities => Set<Activity>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // User Self-referencing Manager relationship
            builder.Entity<ApplicationUser>(b =>
            {
                b.HasOne(u => u.Manager)
                 .WithMany(m => m.Subordinates)
                 .HasForeignKey(u => u.ManagerId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // Customer
            builder.Entity<Customer>(b =>
            {
                b.HasIndex(c => c.CustomerCode).IsUnique();
                b.HasIndex(c => c.Email).IsUnique();
                b.HasIndex(c => c.Phone).IsUnique();

                b.HasOne(c => c.Owner)
                 .WithMany(u => u.OwnedCustomers)
                 .HasForeignKey(c => c.OwnerId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // Lead
            builder.Entity<Lead>(b =>
            {
                b.HasIndex(l => l.LeadCode).IsUnique();
                b.Property(l => l.ExpectedValue).HasPrecision(18, 2);

                b.HasOne(l => l.AssignedUser)
                 .WithMany(u => u.AssignedLeads)
                 .HasForeignKey(l => l.AssignedTo)
                 .OnDelete(DeleteBehavior.Restrict);

                b.HasOne(l => l.ConvertedCustomer)
                 .WithMany(c => c.ConvertedLeads)
                 .HasForeignKey(l => l.ConvertedCustomerId)
                 .OnDelete(DeleteBehavior.SetNull);

                b.HasOne(l => l.ConvertedOpportunity)
                 .WithMany()
                 .HasForeignKey(l => l.ConvertedOpportunityId)
                 .OnDelete(DeleteBehavior.SetNull);
            });

            // Opportunity
            builder.Entity<Opportunity>(b =>
            {
                b.Property(o => o.Amount).HasPrecision(18, 2);

                b.HasOne(o => o.Customer)
                 .WithMany(c => c.Opportunities)
                 .HasForeignKey(o => o.CustomerId)
                 .OnDelete(DeleteBehavior.Restrict);

                b.HasOne(o => o.Lead)
                 .WithMany()
                 .HasForeignKey(o => o.LeadId)
                 .OnDelete(DeleteBehavior.SetNull);

                b.HasOne(o => o.AssignedUser)
                 .WithMany(u => u.AssignedOpportunities)
                 .HasForeignKey(o => o.AssignedTo)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // FollowUp
            builder.Entity<FollowUp>(b =>
            {
                b.HasOne(f => f.Customer)
                 .WithMany(c => c.FollowUps)
                 .HasForeignKey(f => f.CustomerId)
                 .OnDelete(DeleteBehavior.SetNull);

                b.HasOne(f => f.Lead)
                 .WithMany(l => l.FollowUps)
                 .HasForeignKey(f => f.LeadId)
                 .OnDelete(DeleteBehavior.SetNull);

                b.HasOne(f => f.Opportunity)
                 .WithMany(o => o.FollowUps)
                 .HasForeignKey(f => f.OpportunityId)
                 .OnDelete(DeleteBehavior.SetNull);

                b.HasOne(f => f.AssignedUser)
                 .WithMany(u => u.AssignedFollowUps)
                 .HasForeignKey(f => f.AssignedTo)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // Activity
            builder.Entity<Activity>(b =>
            {
                b.HasOne(a => a.Customer)
                 .WithMany(c => c.Activities)
                 .HasForeignKey(a => a.CustomerId)
                 .OnDelete(DeleteBehavior.SetNull);

                b.HasOne(a => a.Lead)
                 .WithMany(l => l.Activities)
                 .HasForeignKey(a => a.LeadId)
                 .OnDelete(DeleteBehavior.SetNull);

                b.HasOne(a => a.AssignedUser)
                 .WithMany(u => u.AssignedActivities)
                 .HasForeignKey(a => a.AssignedTo)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // AuditLog
            builder.Entity<AuditLog>(b =>
            {
                b.HasIndex(a => a.CreatedDate);
                b.HasIndex(a => a.EntityName);
                b.HasIndex(a => a.Action);
            });
        }
    }
}
