using A246F_IQC_Monitoring.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using ClosedXML.Excel;

namespace A246F_IQC_Monitoring.Controllers
{
    public class ReportController : Controller
    {
        private readonly IConfiguration _configuration;

        public ReportController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<IActionResult> Index(
            string? shift,
            DateTime? fromDate,
            DateTime? toDate,
            string? vendor,
            string? cpkLevel,
            string? part,
            string? parameter,
            int page = 1,
            int pageSize = 10)
        {
            var data = new List<IQCDataViewModel>();

            var connectionString =
                _configuration.GetConnectionString("DefaultConnection");

            await using var connection =
                new SqlConnection(connectionString);

            await connection.OpenAsync();

            var sql = @"
                SELECT
                    ID,
                    WOM,
                    Project,
                    Site,
                    Build,
                    Inspection_Step,
                    Station_ID,
                    Part,
                    Vendor,
                    Lot_ID,
                    Line_ID,
                    Process,
                    Category,
                    Parameter,
                    [Unique Identifier],
                    Unit,
                    Sampling_Plan,
                    Nominal,
                    USL,
                    LSL,
                    Time,
                    [1],[2],[3],[4],[5],[6],[7],[8],
                    [9],[10],[11],[12],[13],[14],[15],[16],
                    [17],[18],[19],[20],[21],[22],[23],[24],
                    [25],[26],[27],[28],[29],[30],[31],[32]
                FROM master.dbo.IPQC_DIMENSION_REPORT_A246F
                WHERE 1 = 1
            ";

            var parameters = new List<SqlParameter>();

            // DATE RANGE
            if (fromDate.HasValue && toDate.HasValue)
            {
                // Make sure From Date is not greater than To Date
                if (fromDate.Value.Date > toDate.Value.Date)
                {
                    var temp = fromDate;
                    fromDate = toDate;
                    toDate = temp;
                }

                sql += @"
                    AND Time >= @FromDate
                    AND Time < DATEADD(DAY, 1, @ToDate)
                ";

                parameters.Add(
                    new SqlParameter("@FromDate", SqlDbType.DateTime)
                    {
                        Value = fromDate.Value.Date
                    });

                parameters.Add(
                    new SqlParameter("@ToDate", SqlDbType.DateTime)
                    {
                        Value = toDate.Value.Date
                    });
            }
            else if (fromDate.HasValue)
            {
                // Only From Date selected
                sql += @"
                    AND Time >= @FromDate
                ";

                parameters.Add(
                    new SqlParameter("@FromDate", SqlDbType.DateTime)
                    {
                        Value = fromDate.Value.Date
                    });
            }
            else if (toDate.HasValue)
            {
                // Only To Date selected
                sql += @"
                    AND Time < DATEADD(DAY, 1, @ToDate)
                ";

                parameters.Add(
                    new SqlParameter("@ToDate", SqlDbType.DateTime)
                    {
                        Value = toDate.Value.Date
                    });
            }

            // SHIFT
            if (!string.IsNullOrWhiteSpace(shift))
            {
                if (shift == "A")
                {
                    sql += @"
                        AND CAST(Time AS time) >= '07:00:00'
                        AND CAST(Time AS time) < '19:00:00'
                    ";
                }
                else if (shift == "B")
                {
                    sql += @"
                        AND (
                            CAST(Time AS time) >= '19:00:00'
                            OR CAST(Time AS time) < '07:00:00'
                        )
                    ";
                }
            }

            // VENDOR
            if (!string.IsNullOrWhiteSpace(vendor))
            {
                sql += " AND Vendor = @Vendor ";

                parameters.Add(
                    new SqlParameter("@Vendor", SqlDbType.NVarChar, 200)
                    {
                        Value = vendor
                    });
            }

            // PART
            if (!string.IsNullOrWhiteSpace(part))
            {
                sql += " AND Part = @Part ";

                parameters.Add(
                    new SqlParameter("@Part", SqlDbType.NVarChar, 200)
                    {
                        Value = part
                    });
            }

            // PARAMETER
            if (!string.IsNullOrWhiteSpace(parameter))
            {
                sql += " AND Parameter = @Parameter ";

                parameters.Add(
                    new SqlParameter("@Parameter", SqlDbType.NVarChar, 500)
                    {
                        Value = parameter
                    });
            }

            sql += " ORDER BY Time DESC, ID DESC";

            await using var command =
                new SqlCommand(sql, connection);

            command.CommandType = CommandType.Text;

            foreach (var parameterItem in parameters)
            {
                command.Parameters.Add(parameterItem);
            }

            await using var reader =
                await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var item = new IQCDataViewModel
                {
                    ID = GetInt(reader, "ID"),

                    WOM = GetString(reader, "WOM"),
                    Project = GetString(reader, "Project"),
                    Site = GetString(reader, "Site"),
                    Build = GetString(reader, "Build"),
                    InspectionStep = GetString(reader, "Inspection_Step"),
                    StationId = GetString(reader, "Station_ID"),
                    Part = GetString(reader, "Part"),
                    Vendor = GetString(reader, "Vendor"),
                    LotId = GetString(reader, "Lot_ID"),
                    LineId = GetString(reader, "Line_ID"),
                    Process = GetString(reader, "Process"),
                    Category = GetString(reader, "Category"),
                    Parameter = GetString(reader, "Parameter"),
                    UniqueIdentifier = GetString(reader, "Unique Identifier"),
                    Unit = GetString(reader, "Unit"),
                    SamplingPlan = GetString(reader, "Sampling_Plan"),

                    Nominal = GetDecimal(reader, "Nominal"),
                    USL = GetDecimal(reader, "USL"),
                    LSL = GetDecimal(reader, "LSL"),

                    Time = GetDateTime(reader, "Time")
                };

                // Read 1-32 measurement values
                for (int i = 1; i <= 32; i++)
                {
                    string column = i.ToString();

                    int ordinal = reader.GetOrdinal(column);

                    if (!reader.IsDBNull(ordinal))
                    {
                        string value =
                            reader[ordinal]?.ToString()?.Trim() ?? "";

                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            item.Values.Add(value);
                        }
                    }
                }

                // Calculate CPK
                item.CPK = CalculateCpk(
                    item.Values,
                    item.Nominal,
                    item.USL,
                    item.LSL);

                // CPK filter
                if (PassesCpkFilter(
                    item.CPK,
                    cpkLevel))
                {
                    data.Add(item);
                }
            }

            await LoadFilterLists(connection);

            ViewBag.SelectedShift = shift;
            ViewBag.SelectedFromDate =
                fromDate?.ToString("yyyy-MM-dd");

            ViewBag.SelectedToDate =
                toDate?.ToString("yyyy-MM-dd");

            ViewBag.SelectedVendor = vendor;
            ViewBag.SelectedCpkLevel = cpkLevel;
            ViewBag.SelectedPart = part;
            ViewBag.SelectedParameter = parameter;

            // Pagination
            if (pageSize != 10 &&
                pageSize != 25 &&
                pageSize != 50 &&
                pageSize != 100)
            {
                pageSize = 10;
            }

            if (page < 1)
                page = 1;

            int totalRecords = data.Count;

            int totalPages =
                (int)Math.Ceiling(
                    totalRecords / (double)pageSize);

            if (totalPages > 0 && page > totalPages)
                page = totalPages;

            var pagedData = data
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalRecords = totalRecords;
            ViewBag.TotalPages = totalPages;

            return View(pagedData);
        }

        [HttpGet]
        public async Task<IActionResult> ExportData(
            string? shift,
            DateTime? fromDate,
            DateTime? toDate,
            string? vendor,
            string? cpkLevel,
            string? part,
            string? parameter)
        {
            var data = new List<IQCDataViewModel>();

            var connectionString =
                _configuration.GetConnectionString("DefaultConnection");

            await using var connection =
                new SqlConnection(connectionString);

            await connection.OpenAsync();

            var sql = @"
                SELECT
                    ID,
                    WOM,
                    Project,
                    Site,
                    Build,
                    Inspection_Step,
                    Station_ID,
                    Part,
                    Vendor,
                    Lot_ID,
                    Line_ID,
                    Process,
                    Category,
                    Parameter,
                    [Unique Identifier],
                    Unit,
                    Sampling_Plan,
                    Nominal,
                    USL,
                    LSL,
                    Time,
                    [1],[2],[3],[4],[5],[6],[7],[8],
                    [9],[10],[11],[12],[13],[14],[15],[16],
                    [17],[18],[19],[20],[21],[22],[23],[24],
                    [25],[26],[27],[28],[29],[30],[31],[32]
                FROM master.dbo.IPQC_DIMENSION_REPORT_A246F
                WHERE 1 = 1
            ";

            var parameters = new List<SqlParameter>();

            if (fromDate.HasValue && toDate.HasValue)
            {
                if (fromDate.Value.Date > toDate.Value.Date)
                {
                    var temp = fromDate;
                    fromDate = toDate;
                    toDate = temp;
                }

                sql += @"
            AND Time >= @FromDate
            AND Time < DATEADD(DAY, 1, @ToDate)
        ";

                parameters.Add(
                    new SqlParameter("@FromDate", SqlDbType.DateTime)
                    {
                        Value = fromDate.Value.Date
                    });

                parameters.Add(
                    new SqlParameter("@ToDate", SqlDbType.DateTime)
                    {
                        Value = toDate.Value.Date
                    });
            }
            else if (fromDate.HasValue)
            {
                sql += @"
            AND Time >= @FromDate
        ";

                parameters.Add(
                    new SqlParameter("@FromDate", SqlDbType.DateTime)
                    {
                        Value = fromDate.Value.Date
                    });
            }
            else if (toDate.HasValue)
            {
                sql += @"
            AND Time < DATEADD(DAY, 1, @ToDate)
        ";

                parameters.Add(
                    new SqlParameter("@ToDate", SqlDbType.DateTime)
                    {
                        Value = toDate.Value.Date
                    });
            }


            if (!string.IsNullOrWhiteSpace(shift))
            {
                if (shift == "A")
                {
                    sql += @"
                AND CAST(Time AS time) >= '07:00:00'
                AND CAST(Time AS time) < '19:00:00'
            ";
                }
                else if (shift == "B")
                {
                    sql += @"
                AND (
                    CAST(Time AS time) >= '19:00:00'
                    OR CAST(Time AS time) < '07:00:00'
                )
            ";
                }
            }

            if (!string.IsNullOrWhiteSpace(vendor))
            {
                sql += " AND Vendor = @Vendor ";

                parameters.Add(
                    new SqlParameter("@Vendor", SqlDbType.NVarChar, 200)
                    {
                        Value = vendor
                    });
            }

            if (!string.IsNullOrWhiteSpace(part))
            {
                sql += " AND Part = @Part ";

                parameters.Add(
                    new SqlParameter("@Part", SqlDbType.NVarChar, 200)
                    {
                        Value = part
                    });
            }

            if (!string.IsNullOrWhiteSpace(parameter))
            {
                sql += " AND Parameter = @Parameter ";

                parameters.Add(
                    new SqlParameter("@Parameter", SqlDbType.NVarChar, 500)
                    {
                        Value = parameter
                    });
            }


            sql += " ORDER BY Time DESC, ID DESC";


            await using var command =
                new SqlCommand(sql, connection);

            command.CommandType = CommandType.Text;

            foreach (var parameterItem in parameters)
            {
                command.Parameters.Add(parameterItem);
            }


            await using var reader =
                await command.ExecuteReaderAsync();


            while (await reader.ReadAsync())
            {
                var item = new IQCDataViewModel
                {
                    ID = GetInt(reader, "ID"),

                    WOM = GetString(reader, "WOM"),
                    Project = GetString(reader, "Project"),
                    Site = GetString(reader, "Site"),
                    Build = GetString(reader, "Build"),
                    InspectionStep = GetString(reader, "Inspection_Step"),
                    StationId = GetString(reader, "Station_ID"),
                    Part = GetString(reader, "Part"),
                    Vendor = GetString(reader, "Vendor"),
                    LotId = GetString(reader, "Lot_ID"),
                    LineId = GetString(reader, "Line_ID"),
                    Process = GetString(reader, "Process"),
                    Category = GetString(reader, "Category"),
                    Parameter = GetString(reader, "Parameter"),
                    UniqueIdentifier = GetString(reader, "Unique Identifier"),
                    Unit = GetString(reader, "Unit"),
                    SamplingPlan = GetString(reader, "Sampling_Plan"),

                    Nominal = GetDecimal(reader, "Nominal"),
                    USL = GetDecimal(reader, "USL"),
                    LSL = GetDecimal(reader, "LSL"),

                    Time = GetDateTime(reader, "Time")
                };

                for (int i = 1; i <= 32; i++)
                {
                    string columnName = i.ToString();

                    int ordinal =
                        reader.GetOrdinal(columnName);

                    if (!reader.IsDBNull(ordinal))
                    {
                        string value =
                            reader[ordinal]?.ToString()?.Trim() ?? "";

                        item.Values.Add(value);
                    }
                    else
                    {
                        // Keep the exact position for missing value
                        item.Values.Add("");
                    }
                }

                data.Add(item);
            }
            using var workbook =
                new XLWorkbook();

            var worksheet =
                workbook.Worksheets.Add("IQC Report");

            int column = 1;

            worksheet.Cell(1, column++).Value = "WOM";
            worksheet.Cell(1, column++).Value = "Project";
            worksheet.Cell(1, column++).Value = "Site";
            worksheet.Cell(1, column++).Value = "Build";
            worksheet.Cell(1, column++).Value = "Inspection Step";
            worksheet.Cell(1, column++).Value = "Station ID";
            worksheet.Cell(1, column++).Value = "Part";
            worksheet.Cell(1, column++).Value = "Vendor";
            worksheet.Cell(1, column++).Value = "Lot ID";
            worksheet.Cell(1, column++).Value = "Line ID";
            worksheet.Cell(1, column++).Value = "Process";
            worksheet.Cell(1, column++).Value = "Category";
            worksheet.Cell(1, column++).Value = "Parameter";
            worksheet.Cell(1, column++).Value = "Unique Identifier";
            worksheet.Cell(1, column++).Value = "Unit";
            worksheet.Cell(1, column++).Value = "Sampling Plan";
            worksheet.Cell(1, column++).Value = "Nominal";
            worksheet.Cell(1, column++).Value = "USL";
            worksheet.Cell(1, column++).Value = "LSL";
            worksheet.Cell(1, column++).Value = "Time";

            for (int i = 1; i <= 32; i++)
            {
                worksheet.Cell(1, column++).Value =
                    $"Value {i}";
            }
            var headerRange =
                worksheet.Range(
                    1,
                    1,
                    1,
                    column);

            headerRange.Style.Font.Bold = true;

            headerRange.Style.Alignment.Horizontal =
                XLAlignmentHorizontalValues.Center;

            headerRange.Style.Alignment.Vertical =
                XLAlignmentVerticalValues.Center;

            headerRange.Style.Fill.BackgroundColor =
                XLColor.FromHtml("#0B5A8F");

            headerRange.Style.Font.FontColor =
                XLColor.White;

            headerRange.Style.Border.BottomBorder =
                XLBorderStyleValues.Thin;

            int row = 2;

            foreach (var item in data)
            {
                column = 1;

                worksheet.Cell(row, column++).Value =
                    item.WOM ?? "";

                worksheet.Cell(row, column++).Value =
                    item.Project ?? "";

                worksheet.Cell(row, column++).Value =
                    item.Site ?? "";

                worksheet.Cell(row, column++).Value =
                    item.Build ?? "";

                worksheet.Cell(row, column++).Value =
                    item.InspectionStep ?? "";

                worksheet.Cell(row, column++).Value =
                    item.StationId ?? "";

                worksheet.Cell(row, column++).Value =
                    item.Part ?? "";

                worksheet.Cell(row, column++).Value =
                    item.Vendor ?? "";

                worksheet.Cell(row, column++).Value =
                    item.LotId ?? "";

                worksheet.Cell(row, column++).Value =
                    item.LineId ?? "";

                worksheet.Cell(row, column++).Value =
                    item.Process ?? "";

                worksheet.Cell(row, column++).Value =
                    item.Category ?? "";

                worksheet.Cell(row, column++).Value =
                    item.Parameter ?? "";

                worksheet.Cell(row, column++).Value =
                    item.UniqueIdentifier ?? "";

                worksheet.Cell(row, column++).Value =
                    item.Unit ?? "";

                worksheet.Cell(row, column++).Value =
                    item.SamplingPlan ?? "";


                if (item.Nominal.HasValue)
                    worksheet.Cell(row, column).Value =
                        item.Nominal.Value;

                column++;

                if (item.USL.HasValue)
                    worksheet.Cell(row, column).Value =
                        item.USL.Value;

                column++;

                if (item.LSL.HasValue)
                    worksheet.Cell(row, column).Value =
                        item.LSL.Value;

                column++;

                if (item.Time.HasValue)
                {
                    worksheet.Cell(row, column).Value =
                        item.Time.Value;

                    worksheet.Cell(row, column)
                        .Style.DateFormat.Format =
                        "yyyy-MM-dd HH:mm:ss";
                }

                column++;

                for (int i = 0; i < 32; i++)
                {
                    string value =
                        i < item.Values.Count
                            ? item.Values[i]
                            : "";

                    if (decimal.TryParse(
                        value,
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out decimal numericValue))
                    {
                        worksheet.Cell(row, column).Value =
                            numericValue;
                    }
                    else
                    {
                        worksheet.Cell(row, column).Value =
                            value;
                    }

                    column++;
                }
               
                row++;
            }

            var usedRange =
                worksheet.RangeUsed();

            if (usedRange != null)
            {
                usedRange.Style.Alignment.Vertical =
                    XLAlignmentVerticalValues.Center;

                usedRange.Style.Border.OutsideBorder =
                    XLBorderStyleValues.Thin;

                usedRange.Style.Border.InsideBorder =
                    XLBorderStyleValues.Thin;
            }

            worksheet.SheetView.FreezeRows(1);

            worksheet.Range(
                1,
                1,
                Math.Max(1, row - 1),
                column)
                .SetAutoFilter();


            worksheet.Row(1).Height = 25;

            worksheet.Column(1).Width = 15;   // WOM
            worksheet.Column(2).Width = 15;   // Project
            worksheet.Column(3).Width = 12;   // Site
            worksheet.Column(4).Width = 15;   // Build
            worksheet.Column(5).Width = 20;   // Inspection
            worksheet.Column(6).Width = 15;   // Station
            worksheet.Column(7).Width = 18;   // Part
            worksheet.Column(8).Width = 18;   // Vendor
            worksheet.Column(9).Width = 18;   // Lot
            worksheet.Column(10).Width = 12;  // Line
            worksheet.Column(11).Width = 15;  // Process
            worksheet.Column(12).Width = 15;  // Category
            worksheet.Column(13).Width = 30;  // Parameter
            worksheet.Column(14).Width = 25;  // Unique Identifier
            worksheet.Column(15).Width = 12;  // Unit
            worksheet.Column(16).Width = 18;  // Sampling Plan
            worksheet.Column(17).Width = 12;  // Nominal
            worksheet.Column(18).Width = 12;  // USL
            worksheet.Column(19).Width = 12;  // LSL
            worksheet.Column(20).Width = 22;  // Time

            // Value 1 - Value 32
            for (int i = 21; i <= 52; i++)
            {
                worksheet.Column(i).Width = 12;
            }

            using var stream =
                new MemoryStream();

            workbook.SaveAs(stream);

            stream.Position = 0;

            string fileName =
                $"A246F_IQC_Report_{DateTime.Now:yyyyMMdd}.xlsx";

            return File(
                stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        private async Task LoadFilterLists(
            SqlConnection connection)
        {
            var vendors = new List<string>();
            var parts = new List<string>();
            var parameters = new List<string>();

            var sql = @"
                SELECT DISTINCT
                    Vendor,
                    Part,
                    Parameter
                FROM master.dbo.IPQC_DIMENSION_REPORT_A246F
                WHERE Vendor IS NOT NULL
                   OR Part IS NOT NULL
                   OR Parameter IS NOT NULL
                ORDER BY Vendor, Part, Parameter
            ";

            await using var command =
                new SqlCommand(sql, connection);

            await using var reader =
                await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                if (!reader.IsDBNull(
                    reader.GetOrdinal("Vendor")))
                {
                    var value =
                        reader["Vendor"]?.ToString();

                    if (!string.IsNullOrWhiteSpace(value))
                        vendors.Add(value);
                }

                if (!reader.IsDBNull(
                    reader.GetOrdinal("Part")))
                {
                    var value =
                        reader["Part"]?.ToString();

                    if (!string.IsNullOrWhiteSpace(value))
                        parts.Add(value);
                }

                if (!reader.IsDBNull(
                    reader.GetOrdinal("Parameter")))
                {
                    var value =
                        reader["Parameter"]?.ToString();

                    if (!string.IsNullOrWhiteSpace(value))
                        parameters.Add(value);
                }
            }

            ViewBag.Vendors = vendors
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            ViewBag.Parts = parts
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            ViewBag.Parameters = parameters
                .Distinct()
                .OrderBy(x => x)
                .ToList();
        }


        private static int GetInt(
            SqlDataReader reader,
            string column)
        {
            int ordinal = reader.GetOrdinal(column);

            if (reader.IsDBNull(ordinal))
                return 0;

            return Convert.ToInt32(reader[ordinal]);
        }


        private static string? GetString(
            SqlDataReader reader,
            string column)
        {
            int ordinal = reader.GetOrdinal(column);

            if (reader.IsDBNull(ordinal))
                return null;

            return reader[ordinal]?.ToString();
        }


        private static decimal? GetDecimal(
            SqlDataReader reader,
            string column)
        {
            int ordinal = reader.GetOrdinal(column);

            if (reader.IsDBNull(ordinal))
                return null;

            return Convert.ToDecimal(reader[ordinal]);
        }


        private static DateTime? GetDateTime(
            SqlDataReader reader,
            string column)
        {
            int ordinal = reader.GetOrdinal(column);

            if (reader.IsDBNull(ordinal))
                return null;

            return Convert.ToDateTime(reader[ordinal]);
        }


        private static decimal? CalculateCpk(
            List<string> values,
            decimal? nominal,
            decimal? usl,
            decimal? lsl)
        {
            if (!nominal.HasValue ||
                !usl.HasValue ||
                !lsl.HasValue)
            {
                return null;
            }

            var numbers = values
                .Select(x =>
                {
                    if (decimal.TryParse(
                        x,
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out decimal value))
                    {
                        return (decimal?)value;
                    }

                    return null;
                })
                .Where(x => x.HasValue)
                .Select(x => x!.Value)
                .ToList();

            if (numbers.Count < 2)
                return null;

            decimal actualUSL;
            decimal actualLSL;

            if (usl.Value > nominal.Value &&
                lsl.Value < nominal.Value)
            {
                actualUSL = usl.Value;
                actualLSL = lsl.Value;
            }
            else
            {
                actualUSL =
                    nominal.Value + usl.Value;

                actualLSL =
                    nominal.Value + lsl.Value;
            }

            if (actualUSL <= actualLSL)
                return null;

            double mean =
                (double)numbers.Average();

            double variance =
                numbers
                    .Select(x =>
                        Math.Pow(
                            (double)x - mean,
                            2))
                    .Sum()
                / (numbers.Count - 1);

            double sigma =
                Math.Sqrt(variance);

            if (sigma <= 0)
                return null;

            double cpu =
                ((double)actualUSL - mean)
                / (3.0 * sigma);

            double cpl =
                (mean - (double)actualLSL)
                / (3.0 * sigma);

            double cpk =
                Math.Min(cpu, cpl);

            return (decimal)cpk;
        }


        private static bool PassesCpkFilter(
            decimal? cpk,
            string? level)
        {
            if (string.IsNullOrWhiteSpace(level) ||
                level == "All")
            {
                return true;
            }

            if (!cpk.HasValue)
                return false;

            return level switch
            {
                "Excellent" =>
                    cpk >= 1.67m,

                "Good" =>
                    cpk >= 1.33m &&
                    cpk < 1.67m,

                "Acceptable" =>
                    cpk >= 1.00m &&
                    cpk < 1.33m,

                "Poor" =>
                    cpk < 1.00m,

                _ => true
            };
        }
    }
}