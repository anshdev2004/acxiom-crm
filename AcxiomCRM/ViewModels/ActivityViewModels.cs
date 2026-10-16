using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using AcxiomCRM.Models;

namespace AcxiomCRM.ViewModels
{
    public class ActivityListViewModel
    {
        public List<Activity> Activities { get; set; } = new();
        public string? TypeFilter { get; set; }
        public string? StatusFilter { get; set; }
        public string? AssignedToFilter { get; set; }
        public string? SearchTerm { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public List<SelectListItem> AvailableUsers { get; set; } = new();
    }

    public class ActivityEditViewModel
    {
        public int ActivityId { get; set; }

        [Required(ErrorMessage = "Activity Type is required.")]
        [StringLength(30)]
        [Display(Name = "Activity Type")]
        public string ActivityType { get; set; } = "Call"; // Call, Meeting, Email, Task

        [Required(ErrorMessage = "Subject is required.")]
        [StringLength(150, ErrorMessage = "Subject cannot exceed 150 characters.")]
        public string Subject { get; set; } = string.Empty;

        [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters.")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Activity Date is required.")]
        [Display(Name = "Date & Time")]
        public DateTime ActivityDate { get; set; } = DateTime.Now;

        [Display(Name = "Customer (Optional)")]
        public int? CustomerId { get; set; }

        [Display(Name = "Lead (Optional)")]
        public int? LeadId { get; set; }

        [Display(Name = "Assigned To")]
        public string? AssignedTo { get; set; }

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = "Completed";

        public List<SelectListItem> AvailableCustomers { get; set; } = new();
        public List<SelectListItem> AvailableLeads { get; set; } = new();
        public List<SelectListItem> AvailableUsers { get; set; } = new();
    }
}
