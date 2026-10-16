using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.DTOs
{
    public class FollowUpDto
    {
        public int FollowUpId { get; set; }
        public int? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public int? LeadId { get; set; }
        public string? LeadName { get; set; }
        public int? OpportunityId { get; set; }
        public string? OpportunityName { get; set; }
        public string Subject { get; set; } = string.Empty;
        public DateTime FollowUpDate { get; set; }
        public string FollowUpType { get; set; } = "Call";
        public string? Remarks { get; set; }
        public string Status { get; set; } = "Planned";
        public string AssignedTo { get; set; } = string.Empty;
        public string? AssignedUserName { get; set; }
        public DateTime? CompletedDate { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class CreateFollowUpDto
    {
        public int? CustomerId { get; set; }
        public int? LeadId { get; set; }
        public int? OpportunityId { get; set; }

        [Required(ErrorMessage = "Subject is required.")]
        [StringLength(150, ErrorMessage = "Subject cannot exceed 150 characters.")]
        public string Subject { get; set; } = string.Empty;

        [Required(ErrorMessage = "Follow-up Date is required.")]
        public DateTime FollowUpDate { get; set; } = DateTime.Today.AddDays(1);

        [Required(ErrorMessage = "Follow-up Type is required.")]
        [StringLength(30)]
        public string FollowUpType { get; set; } = "Call";

        [StringLength(1000, ErrorMessage = "Remarks cannot exceed 1000 characters.")]
        public string? Remarks { get; set; }

        public string? AssignedTo { get; set; }
    }

    public class UpdateFollowUpDto
    {
        [Required(ErrorMessage = "Subject is required.")]
        [StringLength(150, ErrorMessage = "Subject cannot exceed 150 characters.")]
        public string Subject { get; set; } = string.Empty;

        [Required(ErrorMessage = "Follow-up Date is required.")]
        public DateTime FollowUpDate { get; set; }

        [Required(ErrorMessage = "Follow-up Type is required.")]
        [StringLength(30)]
        public string FollowUpType { get; set; } = "Call";

        [StringLength(1000, ErrorMessage = "Remarks cannot exceed 1000 characters.")]
        public string? Remarks { get; set; }

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = "Planned";

        public string? AssignedTo { get; set; }
    }
}
