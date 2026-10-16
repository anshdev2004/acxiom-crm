namespace AcxiomCRM.DTOs
{
    public class StageSummaryDto
    {
        public string Stage { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal WeightedAmount { get; set; }
    }

    public class OwnerSummaryDto
    {
        public string OwnerId { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public int Count { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal WeightedAmount { get; set; }
    }

    public class PipelineReportResponseDto
    {
        public decimal TotalPipelineAmount { get; set; }
        public decimal TotalWeightedAmount { get; set; }
        public int TotalOpportunities { get; set; }
        public List<StageSummaryDto> Stages { get; set; } = new();
        public List<OwnerSummaryDto> Owners { get; set; } = new();
    }
}
