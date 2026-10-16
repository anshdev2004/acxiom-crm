using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using AcxiomCRM.Models;

namespace AcxiomCRM.ViewModels
{
    public class FollowUpListViewModel
    {
        public List<FollowUp> FollowUps { get; set; } = new();
        public string? FilterScope { get; set; } = "all"; // all, pending, upcoming, overdue
        public string? StatusFilter { get; set; }
        public string? TypeFilter { get; set; }
        public string? AssignedToFilter { get; set; }
        public string? SearchTerm { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

        public int OverdueCount { get; set; }
        public int UpcomingCount { get; set; }
        public int PendingCount { get; set; }

        public List<SelectListItem> AvailableUsers { get; set; } = new();
    }

    public class FollowUpEditViewModel
    {
        public int FollowUpId { get; set; }

        [Display(Name = "Related Customer (Optional)")]
        public int? CustomerId { get; set; }

        [Display(Name = "Related Lead (Optional)")]
        public int? LeadId { get; set; }

        [Display(Name = "Related Opportunity (Optional)")]
        public int? OpportunityId { get; set; }

        [Required(ErrorMessage = "Subject is required.")]
        [StringLength(150, ErrorMessage = "Subject cannot exceed 150 characters.")]
        public string Subject { get; set; } = string.Empty;

        [Required(ErrorMessage = "Follow-up Date is required.")]
        [DataType(DataType.DateTime)]
        [Display(Name = "Follow-up Date & Time")]
        public DateTime FollowUpDate { get; set; } = DateTime.Today.AddDays(1).AddHours(10);

        [Required(ErrorMessage = "Follow-up Type is required.")]
        [StringLength(30)]
        [Display(Name = "Type")]
        public string FollowUpType { get; set; } = "Call";

        [StringLength(1000, ErrorMessage = "Remarks cannot exceed 1000 characters.")]
        public string? Remarks { get; set; }

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = "Planned";

        [Display(Name = "Assigned To")]
        public string? AssignedTo { get; set; }

        public List<SelectListItem> AvailableCustomers { get; set; } = new();
        public List<SelectListItem> AvailableLeads { get; set; } = new();
        public List<SelectListItem> AvailableOpportunities { get; set; } = new();
        public List<SelectListItem> AvailableUsers { get; set; } = new();
        public List<SelectListItem> AvailableTypes { get; set; } = new();
        public List<SelectListItem> AvailableStatuses { get; set; } = new();
    }
}
