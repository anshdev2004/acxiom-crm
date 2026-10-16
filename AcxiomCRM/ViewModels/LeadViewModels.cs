using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using AcxiomCRM.Models;

namespace AcxiomCRM.ViewModels
{
    public class LeadListViewModel
    {
        public List<Lead> Leads { get; set; } = new();
        public string? SearchTerm { get; set; }
        public string? StatusFilter { get; set; }
        public string? PriorityFilter { get; set; }
        public string? SourceFilter { get; set; }
        public string? AssignedToFilter { get; set; }
        public string? SortBy { get; set; } = "DateDesc";
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public List<SelectListItem> AvailableUsers { get; set; } = new();
    }

    public class LeadEditViewModel
    {
        public int LeadId { get; set; }
        public string? LeadCode { get; set; }

        [Required(ErrorMessage = "Lead Name is required.")]
        [StringLength(100, ErrorMessage = "Lead Name cannot exceed 100 characters.")]
        [Display(Name = "Lead Name")]
        public string LeadName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        [StringLength(100, ErrorMessage = "Email cannot exceed 100 characters.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone is required.")]
        [RegularExpression(@"^[0-9+\-\s()]{7,20}$", ErrorMessage = "Enter a valid phone number.")]
        [StringLength(20, ErrorMessage = "Phone cannot exceed 20 characters.")]
        public string Phone { get; set; } = string.Empty;

        [StringLength(100, ErrorMessage = "Company Name cannot exceed 100 characters.")]
        [Display(Name = "Company Name")]
        public string? CompanyName { get; set; }

        [Required(ErrorMessage = "Lead Source is required.")]
        [StringLength(50)]
        public string Source { get; set; } = "Website";

        [Required(ErrorMessage = "Lead Status is required.")]
        [StringLength(30)]
        public string Status { get; set; } = "New";

        [Required(ErrorMessage = "Priority is required.")]
        [StringLength(20)]
        public string Priority { get; set; } = "Medium";

        [Range(0, 999999999.99, ErrorMessage = "Expected value must be a positive number or zero.")]
        [Display(Name = "Expected Value")]
        public decimal ExpectedValue { get; set; } = 0.00m;

        [StringLength(1000, ErrorMessage = "Notes cannot exceed 1000 characters.")]
        public string? Notes { get; set; }

        [Display(Name = "Assigned Sales Executive")]
        public string? AssignedTo { get; set; }

        public List<SelectListItem> AvailableUsers { get; set; } = new();
    }

    public class LeadDetailsViewModel
    {
        public Lead Lead { get; set; } = null!;
        public List<FollowUp> FollowUps { get; set; } = new();
        public List<Activity> Activities { get; set; } = new();
    }

    public class LeadConvertViewModel
    {
        public int LeadId { get; set; }

        public string LeadCode { get; set; } = string.Empty;
        public string LeadName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string? CompanyName { get; set; }

        public bool ExistingCustomerFound { get; set; }
        public int? ExistingCustomerId { get; set; }
        public string? ExistingCustomerName { get; set; }

        [Display(Name = "Create an Opportunity for this customer?")]
        public bool CreateOpportunity { get; set; } = true;

        [Display(Name = "Opportunity Name")]
        [StringLength(150, ErrorMessage = "Opportunity Name cannot exceed 150 characters.")]
        public string? OpportunityName { get; set; }

        [Range(0.01, 999999999.99, ErrorMessage = "Opportunity Amount must be greater than 0.")]
        [Display(Name = "Opportunity Amount")]
        public decimal? OpportunityAmount { get; set; }

        [Display(Name = "Expected Close Date")]
        public DateTime? ExpectedCloseDate { get; set; } = DateTime.Today.AddDays(30);
    }
}
