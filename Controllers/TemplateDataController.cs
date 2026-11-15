using A246F_IQC_Monitoring.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;

namespace A246F_IQC_Monitoring.Controllers
{
    public class TemplateDataController : Controller
    {
        private readonly IConfiguration _configuration;

        public TemplateDataController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<IActionResult> Index(
            string? templateId,
            string? part,
            string? vendor,
            string? category,
            int page = 1)
        {
            const int pageSize = 10;

            if (page < 1)
                page = 1;

            var model = new TemplateDataPageViewModel
            {
                TemplateId = templateId,
                Part = part,
                Vendor = vendor,
                Category = category,
                CurrentPage = page,
                PageSize = pageSize
            };

            var connectionString =
                _configuration.GetConnectionString("DefaultConnection");

            try
            {
                using var connection = new SqlConnection(connectionString);

                await connection.OpenAsync();

                // --------------------------------------------------
                // LOAD FILTER VALUES
                // --------------------------------------------------

                const string filterSql = @"
                    SELECT DISTINCT
                        PART,
                        VENDOR,
                        CATEGORY
                    FROM master.dbo.IPQC_TEMPLATE_A246F
                    WHERE PART IS NOT NULL
                       OR VENDOR IS NOT NULL
                       OR CATEGORY IS NOT NULL
                    ORDER BY PART, VENDOR, CATEGORY;
                ";

                using (var filterCommand =
                       new SqlCommand(filterSql, connection))
                {
                    using var reader =
                        await filterCommand.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        var partValue =
                            reader["PART"] == DBNull.Value
                                ? null
                                : reader["PART"].ToString();

                        var vendorValue =
                            reader["VENDOR"] == DBNull.Value
                                ? null
                                : reader["VENDOR"].ToString();

                        var categoryValue =
                            reader["CATEGORY"] == DBNull.Value
                                ? null
                                : reader["CATEGORY"].ToString();

                        if (!string.IsNullOrWhiteSpace(partValue) &&
                            !model.Parts.Contains(partValue))
                        {
                            model.Parts.Add(partValue);
                        }

                        if (!string.IsNullOrWhiteSpace(vendorValue) &&
                            !model.Vendors.Contains(vendorValue))
                        {
                            model.Vendors.Add(vendorValue);
                        }

                        if (!string.IsNullOrWhiteSpace(categoryValue) &&
                            !model.Categories.Contains(categoryValue))
                        {
                            model.Categories.Add(categoryValue);
                        }
                    }
                }

                model.Parts = model.Parts
                    .OrderBy(x => x)
                    .ToList();

                model.Vendors = model.Vendors
                    .OrderBy(x => x)
                    .ToList();

                model.Categories = model.Categories
                    .OrderBy(x => x)
                    .ToList();

                // --------------------------------------------------
                // BUILD MAIN QUERY
                // --------------------------------------------------

                var whereConditions = new List<string>();

                if (!string.IsNullOrWhiteSpace(templateId))
                {
                    whereConditions.Add(
                        "CAST(TEMPLATE_ID AS NVARCHAR(50)) = @TemplateId");
                }

                if (!string.IsNullOrWhiteSpace(part))
                {
                    whereConditions.Add("PART = @Part");
                }

                if (!string.IsNullOrWhiteSpace(vendor))
                {
                    whereConditions.Add("VENDOR = @Vendor");
                }

                if (!string.IsNullOrWhiteSpace(category))
                {
                    whereConditions.Add("CATEGORY = @Category");
                }

                var whereClause =
                    whereConditions.Count > 0
                        ? "WHERE " + string.Join(" AND ", whereConditions)
                        : "";

                // --------------------------------------------------
                // COUNT
                // --------------------------------------------------

                var countSql = $@"
                    SELECT COUNT(*)
                    FROM master.dbo.IPQC_TEMPLATE_A246F
                    {whereClause};
                ";

                using (var countCommand =
                       new SqlCommand(countSql, connection))
                {
                    AddParameters(
                        countCommand,
                        templateId,
                        part,
                        vendor,
                        category);

                    model.TotalRecords =
                        Convert.ToInt32(
                            await countCommand.ExecuteScalarAsync());
                }

                // --------------------------------------------------
                // CORRECT PAGE NUMBER
                // --------------------------------------------------

                if (model.TotalRecords > 0)
                {
                    var totalPages =
                        (int)Math.Ceiling(
                            (double)model.TotalRecords / pageSize);

                    if (page > totalPages)
                        page = totalPages;

                    model.CurrentPage = page;
                }
                else
                {
                    model.CurrentPage = 1;
                }

                var offset =
                    (model.CurrentPage - 1) * pageSize;

                // --------------------------------------------------
                // MAIN DATA
                // --------------------------------------------------

                var dataSql = $@"
                    SELECT
                        TEMPLATE_ID,
                        PART,
                        VENDOR,
                        PARAMETER,
                        CATEGORY,
                        UNIT,
                        NOMINAL,
                        USL,
                        LSL
                    FROM master.dbo.IPQC_TEMPLATE_A246F
                    {whereClause}
                    ORDER BY TEMPLATE_ID ASC
                    OFFSET @Offset ROWS
                    FETCH NEXT @PageSize ROWS ONLY;
                ";

                using (var dataCommand =
                       new SqlCommand(dataSql, connection))
                {
                    AddParameters(
                        dataCommand,
                        templateId,
                        part,
                        vendor,
                        category);

                    dataCommand.Parameters.Add(
                        "@Offset",
                        SqlDbType.Int).Value = offset;

                    dataCommand.Parameters.Add(
                        "@PageSize",
                        SqlDbType.Int).Value = pageSize;

                    using var reader =
                        await dataCommand.ExecuteReaderAsync();

                    while (await reader.ReadAsync())
                    {
                        model.Data.Add(
                            new TemplateDataViewModel
                            {
                                TemplateId =
                                    reader["TEMPLATE_ID"] == DBNull.Value
                                        ? 0
                                        : Convert.ToInt32(
                                            reader["TEMPLATE_ID"]),

                                Part =
                                    reader["PART"] == DBNull.Value
                                        ? null
                                        : reader["PART"].ToString(),

                                Vendor =
                                    reader["VENDOR"] == DBNull.Value
                                        ? null
                                        : reader["VENDOR"].ToString(),

                                Parameter =
                                    reader["PARAMETER"] == DBNull.Value
                                        ? null
                                        : reader["PARAMETER"].ToString(),

                                Category =
                                    reader["CATEGORY"] == DBNull.Value
                                        ? null
                                        : reader["CATEGORY"].ToString(),

                                Unit =
                                    reader["UNIT"] == DBNull.Value
                                        ? null
                                        : reader["UNIT"].ToString(),

                                Nominal =
                                    reader["NOMINAL"] == DBNull.Value
                                        ? null
                                        : Convert.ToDecimal(
                                            reader["NOMINAL"]),

                                USL =
                                    reader["USL"] == DBNull.Value
                                        ? null
                                        : Convert.ToDecimal(
                                            reader["USL"]),

                                LSL =
                                    reader["LSL"] == DBNull.Value
                                        ? null
                                        : Convert.ToDecimal(
                                            reader["LSL"])
                            });
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] =
                    "Unable to load Template Data. " + ex.Message;
            }

            return View(model);
        }

        private static void AddParameters(
            SqlCommand command,
            string? templateId,
            string? part,
            string? vendor,
            string? category)
        {
            command.Parameters.Add(
                "@TemplateId",
                SqlDbType.NVarChar,
                50).Value =
                    string.IsNullOrWhiteSpace(templateId)
                        ? DBNull.Value
                        : templateId;

            command.Parameters.Add(
                "@Part",
                SqlDbType.NVarChar,
                255).Value =
                    string.IsNullOrWhiteSpace(part)
                        ? DBNull.Value
                        : part;

            command.Parameters.Add(
                "@Vendor",
                SqlDbType.NVarChar,
                255).Value =
                    string.IsNullOrWhiteSpace(vendor)
                        ? DBNull.Value
                        : vendor;

            command.Parameters.Add(
                "@Category",
                SqlDbType.NVarChar,
                255).Value =
                    string.IsNullOrWhiteSpace(category)
                        ? DBNull.Value
                        : category;
        }
    }
}