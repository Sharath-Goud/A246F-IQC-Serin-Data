namespace A246F_IQC_Monitoring.Models
{
    public class TemplateDataViewModel
    {
        public int TemplateId { get; set; }

        public string? Part { get; set; }

        public string? Vendor { get; set; }

        public string? Parameter { get; set; }

        public string? Category { get; set; }

        public string? Unit { get; set; }

        public decimal? Nominal { get; set; }

        public decimal? USL { get; set; }

        public decimal? LSL { get; set; }
    }
}