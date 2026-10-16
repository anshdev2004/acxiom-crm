using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using AcxiomCRM.Models;

namespace AcxiomCRM.ViewModels
{
    public class OpportunityListViewModel
    {
        public List<Opportunity> Opportunities { get; set; } = new();
        public string? SearchTerm { get; set; }
        public string? StageFilter { get; set; }
        public string? StatusFilter { get; set; }
        public string? OwnerFilter { get; set; }
        public string? SortBy { get; set; } = "DateDesc";
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

        public decimal TotalPipelineValue { get; set; }
        public decimal TotalWeightedPipelineValue { get; set; }

        public List<SelectListItem> AvailableStages { get; set; } = new();
        public List<SelectListItem> AvailableUsers { get; set; } = new();
    }

    public class OpportunityEditViewModel
    {
        public int OpportunityId { get; set; }

        [Required(ErrorMessage = "Opportunity Name is required.")]
        [StringLength(150, ErrorMessage = "Opportunity Name cannot exceed 150 characters.")]
        [Display(Name = "Opportunity Name")]
        public string OpportunityName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Customer is required.")]
        [Display(Name = "Customer")]
        public int CustomerId { get; set; }

        [Display(Name = "Source Lead (Optional)")]
        public int? LeadId { get; set; }

        [Required(ErrorMessage = "Opportunity Amount is required.")]
        [Range(0.01, 999999999.99, ErrorMessage = "Opportunity Amount must be greater than 0.")]
        [Display(Name = "Amount ($)")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Stage is required.")]
        [StringLength(30)]
        public string Stage { get; set; } = "Qualification";

        [Range(0, 100, ErrorMessage = "Probability must be between 0 and 100.")]
        [Display(Name = "Probability (%)")]
        public int Probability { get; set; } = 20;

        [Required(ErrorMessage = "Expected Close Date is required.")]
        [DataType(DataType.Date)]
        [Display(Name = "Expected Close Date")]
        public DateTime ExpectedCloseDate { get; set; } = DateTime.Today.AddDays(30);

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = "Open";

        [Display(Name = "Assigned Sales Executive")]
        public string? AssignedTo { get; set; }

        [StringLength(1000, ErrorMessage = "Notes cannot exceed 1000 characters.")]
        public string? Notes { get; set; }

        public List<SelectListItem> AvailableCustomers { get; set; } = new();
        public List<SelectListItem> AvailableLeads { get; set; } = new();
        public List<SelectListItem> AvailableUsers { get; set; } = new();
        public List<SelectListItem> AvailableStages { get; set; } = new();
    }

    public class OpportunityDetailsViewModel
    {
        public Opportunity Opportunity { get; set; } = null!;
        public List<FollowUp> FollowUps { get; set; } = new();
    }
}
