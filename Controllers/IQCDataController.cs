using A246F_IQC_Monitoring.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace A246F_IQC_Monitoring.Controllers
{
    public class IQCDataController : Controller
    {
        private readonly IConfiguration _configuration;

        public IQCDataController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<IActionResult> Index(
    string? shift,
    DateTime? date,
    string? vendor,
    string? cpkLevel,
    string? part,
    string? parameter,
    int page = 1)
        {
            var data = new List<IQCDataViewModel>();

            // If no date is selected, automatically use TODAY
            DateTime selectedDate = date?.Date ?? DateTime.Today;

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
        WHERE Time >= @DateStart
          AND Time < DATEADD(DAY, 1, @DateStart)
    ";

            var parameters = new List<SqlParameter>();

            parameters.Add(
                new SqlParameter("@DateStart", SqlDbType.DateTime)
                {
                    Value = selectedDate
                });

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

                item.CPK = CalculateCpk(
                    item.Values,
                    item.Nominal,
                    item.USL,
                    item.LSL);

                if (PassesCpkFilter(
                    item.CPK,
                    cpkLevel))
                {
                    data.Add(item);
                }
            }

            await LoadFilterLists(connection);

            ViewBag.SelectedShift = shift;

            // Always show the actual date being displayed
            ViewBag.SelectedDate =
                selectedDate.ToString("yyyy-MM-dd");

            ViewBag.SelectedVendor = vendor;
            ViewBag.SelectedCpkLevel = cpkLevel;
            ViewBag.SelectedPart = part;
            ViewBag.SelectedParameter = parameter;

            // Show only the latest single IQC record
            var latestData = data
                .OrderByDescending(x => x.Time)
                .ThenByDescending(x => x.ID)
                .Take(1)
                .ToList();

            ViewBag.CurrentPage = 1;
            ViewBag.PageSize = 1;
            ViewBag.TotalRecords = latestData.Count;
            ViewBag.TotalPages = latestData.Count > 0 ? 1 : 0;

            return View(latestData);
        }

        [HttpGet]
        public async Task<IActionResult> GetForEdit(int id)
        {
            var connectionString =
                _configuration.GetConnectionString("DefaultConnection");

            await using var connection =
                new SqlConnection(connectionString);

            await connection.OpenAsync();

            const string sql = @"
        SELECT
            ID,
            Parameter,
            Lot_ID,
            Time,
            Nominal,
            USL,
            LSL,
            [1], [2], [3], [4], [5], [6], [7], [8],
            [9], [10], [11], [12], [13], [14], [15], [16],
            [17], [18], [19], [20], [21], [22], [23], [24],
            [25], [26], [27], [28], [29], [30], [31], [32]
        FROM [master].[dbo].[IPQC_DIMENSION_REPORT_A246F]
        WHERE ID = @ID;
    ";

            await using var command = new SqlCommand(sql, connection);

            command.Parameters.Add("@ID", SqlDbType.Int).Value = id;

            await using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                return NotFound(new
                {
                    success = false,
                    message = "IQC record not found."
                });
            }

            var values = new List<string?>();

            for (int i = 1; i <= 32; i++)
            {
                int ordinal = reader.GetOrdinal(i.ToString());

                if (reader.IsDBNull(ordinal))
                {
                    values.Add(null);
                }
                else
                {
                    values.Add(reader[ordinal].ToString());
                }
            }

            return Json(new
            {
                id = reader["ID"],

                parameter = reader["Parameter"] == DBNull.Value
                    ? null
                    : reader["Parameter"].ToString(),

                lotId = reader["Lot_ID"] == DBNull.Value
                    ? null
                    : reader["Lot_ID"].ToString(),

                time = reader["Time"] == DBNull.Value
                    ? null
                    : Convert.ToDateTime(reader["Time"])
                        .ToString("yyyy-MM-dd HH:mm:ss"),
                Nominal = GetDecimal(reader, "Nominal"),
                USL = GetDecimal(reader, "USL"),
                LSL = GetDecimal(reader, "LSL"),

                values = values
            });
        }


        [HttpPost]
        public async Task<IActionResult> Update()
        {
            try
            {
                var form = Request.Form;

                if (!int.TryParse(form["ID"], out int id))
                {
                    return Json(new
                    {
                        success = false,
                        message = "Invalid IQC record ID."
                    });
                }

                decimal? nominal = ParseDecimal(form["Nominal"]);
                decimal? usl = ParseDecimal(form["USL"]);
                decimal? lsl = ParseDecimal(form["LSL"]);

                var connectionString =
                    _configuration.GetConnectionString("DefaultConnection");

                await using var connection =
                    new SqlConnection(connectionString);

                await connection.OpenAsync();

                const string sql = @"
            UPDATE [master].[dbo].[IPQC_DIMENSION_REPORT_A246F]
            SET
                Nominal = @Nominal,
                USL = @USL,
                LSL = @LSL,

                [1] = @V1,
                [2] = @V2,
                [3] = @V3,
                [4] = @V4,
                [5] = @V5,
                [6] = @V6,
                [7] = @V7,
                [8] = @V8,
                [9] = @V9,
                [10] = @V10,
                [11] = @V11,
                [12] = @V12,
                [13] = @V13,
                [14] = @V14,
                [15] = @V15,
                [16] = @V16,
                [17] = @V17,
                [18] = @V18,
                [19] = @V19,
                [20] = @V20,
                [21] = @V21,
                [22] = @V22,
                [23] = @V23,
                [24] = @V24,
                [25] = @V25,
                [26] = @V26,
                [27] = @V27,
                [28] = @V28,
                [29] = @V29,
                [30] = @V30,
                [31] = @V31,
                [32] = @V32

            WHERE ID = @ID;
        ";

                await using var command =
                    new SqlCommand(sql, connection);

                command.Parameters.Add("@ID", SqlDbType.Int).Value = id;

                AddDecimalParameter(command, "@Nominal", nominal);
                AddDecimalParameter(command, "@USL", usl);
                AddDecimalParameter(command, "@LSL", lsl);

                for (int i = 1; i <= 32; i++)
                {
                    string fieldName = $"Value{i}";
                    string parameterName = $"@V{i}";

                    decimal? value =
                        ParseDecimal(form[fieldName]);

                    AddDecimalParameter(
                        command,
                        parameterName,
                        value);
                }

                int rowsAffected =
                    await command.ExecuteNonQueryAsync();

                if (rowsAffected == 0)
                {
                    return Json(new
                    {
                        success = false,
                        message = "IQC record was not found."
                    });
                }

                return Json(new
                {
                    success = true,
                    message = "IQC data updated successfully."
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = ex.Message
                });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var connectionString =
                _configuration.GetConnectionString("DefaultConnection");

            await using var connection =
                new SqlConnection(connectionString);

            await connection.OpenAsync();

            const string sql = @"
                DELETE FROM dbo.IPQC_DIMENSION_REPORT_A246F
                WHERE ID = @ID";

            await using var command =
                new SqlCommand(sql, connection);

            command.Parameters.AddWithValue("@ID", id);

            var affectedRows = await command.ExecuteNonQueryAsync();

            if (affectedRows == 0)
            {
                TempData["ErrorMessage"] = "Record not found or already deleted.";
            }
            else
            {
                TempData["SuccessMessage"] = "IQC record deleted successfully.";
            }

            return RedirectToAction(nameof(Index));
        }

        private static decimal? ParseDecimal(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            if (decimal.TryParse(
                value,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out decimal result))
            {
                return result;
            }

            return null;
        }

        private static void AddDecimalParameter(
    SqlCommand command,
    string parameterName,
    decimal? value)
        {
            var parameter =
                command.Parameters.Add(
                    parameterName,
                    SqlDbType.Decimal);

            parameter.Precision = 18;
            parameter.Scale = 3;

            parameter.Value =
                value.HasValue
                    ? value.Value
                    : DBNull.Value;
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
                actualUSL = nominal.Value + usl.Value;
                actualLSL = nominal.Value + lsl.Value;
            }

            if (actualUSL <= actualLSL)
                return null;

            double mean = (double)numbers.Average();

            double variance = numbers
                .Select(x => Math.Pow((double)x - mean, 2))
                .Sum()
                / (numbers.Count - 1);

            double sigma = Math.Sqrt(variance);

            if (sigma <= 0)
                return null;

            double cpu =
                ((double)actualUSL - mean)
                / (3.0 * sigma);

            double cpl =
                (mean - (double)actualLSL)
                / (3.0 * sigma);

            double cpk = Math.Min(cpu, cpl);

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
                "Excellent" => cpk >= 1.67m,

                "Good" => cpk >= 1.33m && cpk < 1.67m,

                "Acceptable" => cpk >= 1.00m && cpk < 1.33m,

                "Poor" => cpk < 1.00m,

                _ => true
            };
        }

    }

}