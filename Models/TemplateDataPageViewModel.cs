namespace A246F_IQC_Monitoring.Models
{
    public class TemplateDataPageViewModel
    {
        public List<TemplateDataViewModel> Data { get; set; } = new();

        public string? TemplateId { get; set; }

        public string? Part { get; set; }

        public string? Vendor { get; set; }

        public string? Category { get; set; }

        public List<string> Parts { get; set; } = new();

        public List<string> Vendors { get; set; } = new();

        public List<string> Categories { get; set; } = new();

        public int CurrentPage { get; set; } = 1;

        public int PageSize { get; set; } = 10;

        public int TotalRecords { get; set; }

        public int TotalPages =>
            PageSize > 0
                ? (int)Math.Ceiling((double)TotalRecords / PageSize)
                : 0;
    }
}