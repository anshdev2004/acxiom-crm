using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AcxiomCRM.Models
{
    public class Lead
    {
        [Key]
        public int LeadId { get; set; }

        [Required]
        [MaxLength(20)]
        public string LeadCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string LeadName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [MaxLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Phone { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? CompanyName { get; set; }

        [Required]
        [MaxLength(50)]
        public string Source { get; set; } = "Website";

        [Required]
        [MaxLength(30)]
        public string Status { get; set; } = "New";

        [Required]
        [MaxLength(20)]
        public string Priority { get; set; } = "Medium";

        [Column(TypeName = "decimal(18,2)")]
        public decimal ExpectedValue { get; set; } = 0.00m;

        [MaxLength(1000)]
        public string? Notes { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [Required]
        public string AssignedTo { get; set; } = string.Empty;

        [ForeignKey("AssignedTo")]
        public virtual ApplicationUser? AssignedUser { get; set; }

        // Conversion tracking
        public int? ConvertedCustomerId { get; set; }

        [ForeignKey("ConvertedCustomerId")]
        public virtual Customer? ConvertedCustomer { get; set; }

        public int? ConvertedOpportunityId { get; set; }

        [ForeignKey("ConvertedOpportunityId")]
        public virtual Opportunity? ConvertedOpportunity { get; set; }

        public DateTime? ConversionDate { get; set; }

        // Navigation
        public virtual ICollection<FollowUp> FollowUps { get; set; } = new List<FollowUp>();
        public virtual ICollection<Activity> Activities { get; set; } = new List<Activity>();
    }
}
