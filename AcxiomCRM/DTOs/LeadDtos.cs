using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.DTOs
{
    public class LeadDto
    {
        public int LeadId { get; set; }
        public string LeadCode { get; set; } = string.Empty;
        public string LeadName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string? CompanyName { get; set; }
        public string Source { get; set; } = "Website";
        public string Status { get; set; } = "New";
        public string Priority { get; set; } = "Medium";
        public decimal ExpectedValue { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedDate { get; set; }
        public string AssignedTo { get; set; } = string.Empty;
        public string? AssignedUserName { get; set; }
        public int? ConvertedCustomerId { get; set; }
        public int? ConvertedOpportunityId { get; set; }
        public DateTime? ConversionDate { get; set; }
    }

    public class CreateLeadDto
    {
        [Required(ErrorMessage = "Lead Name is required.")]
        [StringLength(100, ErrorMessage = "Lead Name cannot exceed 100 characters.")]
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
        public string? CompanyName { get; set; }

        [Required(ErrorMessage = "Source is required.")]
        [StringLength(50)]
        public string Source { get; set; } = "Website";

        [Required(ErrorMessage = "Status is required.")]
        [StringLength(30)]
        public string Status { get; set; } = "New";

        [Required(ErrorMessage = "Priority is required.")]
        [StringLength(20)]
        public string Priority { get; set; } = "Medium";

        [Range(0, 999999999.99, ErrorMessage = "Expected value must be a positive number or zero.")]
        public decimal ExpectedValue { get; set; } = 0.00m;

        [StringLength(1000, ErrorMessage = "Notes cannot exceed 1000 characters.")]
        public string? Notes { get; set; }

        public string? AssignedTo { get; set; }
    }

    public class UpdateLeadDto : CreateLeadDto
    {
    }

    public class ConvertLeadDto
    {
        public int LeadId { get; set; }

        public bool CreateOpportunity { get; set; } = true;

        [StringLength(150, ErrorMessage = "Opportunity Name cannot exceed 150 characters.")]
        public string? OpportunityName { get; set; }

        [Range(0.01, 999999999.99, ErrorMessage = "Opportunity Amount must be greater than 0.")]
        public decimal? OpportunityAmount { get; set; }

        public DateTime? ExpectedCloseDate { get; set; }
    }
}
