
namespace A246F_IQC_Monitoring.Models
{
    public class IQCDataViewModel
    {
        public int ID { get; set; }

        public string? WOM { get; set; }
        public string? Project { get; set; }
        public string? Site { get; set; }
        public string? Build { get; set; }
        public string? InspectionStep { get; set; }
        public string? StationId { get; set; }
        public string? Part { get; set; }
        public string? Vendor { get; set; }
        public string? LotId { get; set; }
        public string? LineId { get; set; }
        public string? Process { get; set; }
        public string? Category { get; set; }
        public string? Parameter { get; set; }
        public string? UniqueIdentifier { get; set; }
        public string? Unit { get; set; }
        public string? SamplingPlan { get; set; }

        public decimal? Nominal { get; set; }
        public decimal? USL { get; set; }
        public decimal? LSL { get; set; }

        public DateTime? Time { get; set; }

        public List<string> Values { get; set; } = new List<string>();

        public decimal? CPK { get; set; }

        public string CpkClass
        {
            get
            {
                if (!CPK.HasValue)
                    return "neutral";

                if (CPK.Value >= 1.67m)
                    return "excellent";

                if (CPK.Value >= 1.33m)
                    return "good";

                return "fail";
            }
        }
    }
}
