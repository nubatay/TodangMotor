using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using PdfDocument = QuestPDF.Fluent.Document;

namespace TodangMotor.Services
{
    /// <summary>
    /// A simple table of data plus metadata for a report.
    /// Built by the calling form, then handed to ReportService.
    /// </summary>
    public class ReportTable
    {
        public string Title { get; set; } = string.Empty;
        public string Subtitle { get; set; } = string.Empty;
        public string FooterNote { get; set; } = string.Empty;

        public List<string> Headers { get; set; } = new();
        public List<List<string>> Rows { get; set; } = new();
    }

    /// <summary>
    /// Turns a ReportTable into a CSV, Excel, or PDF file on disk.
    /// PDF uses QuestPDF's bundled "Lato" font — Segoe UI is not available
    /// by default in QuestPDF 2026.9+ and must not be referenced.
    /// </summary>
    public static class ReportService
    {
        /// <summary>Font family used by PDF output. Bundled with QuestPDF.</summary>
        private const string PdfFontFamily = "Lato";

        static ReportService()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        // ============================================================
        // CSV
        // ============================================================

        public static void ExportCsv(ReportTable table, string path)
        {
            EnsureValid(table, path);
            path = EnsureExtension(path, ".csv");

            var sb = new StringBuilder();

            if (!string.IsNullOrEmpty(table.Title))
                sb.AppendLine(EscapeCsv(table.Title));

            if (!string.IsNullOrEmpty(table.Subtitle))
                sb.AppendLine(EscapeCsv(table.Subtitle));

            if (!string.IsNullOrEmpty(table.Title) || !string.IsNullOrEmpty(table.Subtitle))
                sb.AppendLine();

            sb.AppendLine(string.Join(",", EscapeCsvAll(table.Headers)));

            foreach (var row in table.Rows)
                sb.AppendLine(string.Join(",", EscapeCsvAll(row)));

            if (!string.IsNullOrEmpty(table.FooterNote))
            {
                sb.AppendLine();
                sb.AppendLine(EscapeCsv(table.FooterNote));
            }

            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        }

        // ============================================================
        // EXCEL
        // ============================================================

        public static void ExportExcel(ReportTable table, string path)
        {
            EnsureValid(table, path);
            path = EnsureExtension(path, ".xlsx");

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Report");

            int colCount = Math.Max(1, table.Headers.Count);

            ws.Cell(1, 1).Value = table.Title ?? string.Empty;
            if (colCount > 1)
                ws.Range(1, 1, 1, colCount).Merge();
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 16;
            ws.Cell(1, 1).Style.Font.FontColor = XLColor.FromHtml("#1A1A1A");

            ws.Cell(2, 1).Value = table.Subtitle ?? string.Empty;
            if (colCount > 1)
                ws.Range(2, 1, 2, colCount).Merge();
            ws.Cell(2, 1).Style.Font.FontSize = 10;
            ws.Cell(2, 1).Style.Font.FontColor = XLColor.FromHtml("#6B7280");

            const int headerRow = 4;
            for (int c = 0; c < table.Headers.Count; c++)
            {
                var cell = ws.Cell(headerRow, c + 1);
                cell.Value = table.Headers[c] ?? string.Empty;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#4D6BFE");
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Font.Bold = true;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                cell.Style.Border.BottomBorderColor = XLColor.FromHtml("#3B55D9");
            }

            for (int r = 0; r < table.Rows.Count; r++)
            {
                var row = table.Rows[r];
                for (int c = 0; c < row.Count; c++)
                {
                    var cell = ws.Cell(headerRow + 1 + r, c + 1);
                    cell.Value = row[c] ?? string.Empty;
                    cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                }
            }

            if (!string.IsNullOrEmpty(table.FooterNote))
            {
                int footRow = headerRow + 1 + table.Rows.Count + 1;
                ws.Cell(footRow, 1).Value = table.FooterNote;
                ws.Cell(footRow, 1).Style.Font.FontColor = XLColor.FromHtml("#6B7280");
                ws.Cell(footRow, 1).Style.Font.FontSize = 9;
                ws.Cell(footRow, 1).Style.Font.Italic = true;
            }

            ws.Columns().AdjustToContents();

            for (int c = 1; c <= colCount; c++)
            {
                if (ws.Column(c).Width < 12)
                    ws.Column(c).Width = 12;
            }

            wb.SaveAs(path);
        }

        // ============================================================
        // PDF
        // ============================================================

        public static void ExportPdf(ReportTable table, string path)
        {
            EnsureValid(table, path);
            path = EnsureExtension(path, ".pdf");

            PdfDocument.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(36);
                    page.DefaultTextStyle(x => x
                        .FontSize(10)
                        .FontFamily(PdfFontFamily)
                        .FontColor("#1A1A1A"));

                    // ---- Header ----
                    page.Header().Column(col =>
                    {
                        col.Item().Text(table.Title ?? string.Empty)
                            .FontSize(18).SemiBold().FontColor("#1A1A1A");

                        if (!string.IsNullOrEmpty(table.Subtitle))
                        {
                            col.Item().PaddingTop(2)
                                .Text(table.Subtitle).FontSize(10).FontColor("#6B7280");
                        }

                        col.Item().PaddingTop(8)
                            .LineHorizontal(1).LineColor("#D6DAE5");
                    });

                    // ---- Table ----
                    page.Content().PaddingTop(12).Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            int n = Math.Max(1, table.Headers.Count);
                            for (int i = 0; i < n; i++)
                                c.RelativeColumn();
                        });

                        t.Header(h =>
                        {
                            foreach (var header in table.Headers)
                            {
                                h.Cell()
                                    .Background("#4D6BFE")
                                    .PaddingVertical(6).PaddingHorizontal(8)
                                    .Text(header ?? string.Empty)
                                    .FontColor("#FFFFFF").SemiBold().FontSize(10);
                            }
                        });

                        foreach (var row in table.Rows)
                        {
                            for (int i = 0; i < table.Headers.Count; i++)
                            {
                                string cellText = i < row.Count ? (row[i] ?? string.Empty) : string.Empty;
                                t.Cell()
                                    .BorderBottom(0.5f).BorderColor("#E5E7EF")
                                    .PaddingVertical(6).PaddingHorizontal(8)
                                    .Text(cellText).FontSize(9);
                            }
                        }
                    });

                    // ---- Footer ----
                    page.Footer().Column(col =>
                    {
                        if (!string.IsNullOrEmpty(table.FooterNote))
                        {
                            col.Item().PaddingBottom(4)
                                .Text(table.FooterNote)
                                .FontSize(8).Italic().FontColor("#6B7280");
                        }

                        col.Item().AlignCenter().Text(x =>
                        {
                            x.DefaultTextStyle(s => s.FontSize(8).FontColor("#6B7280"));
                            x.Span("Page ");
                            x.CurrentPageNumber();
                            x.Span(" of ");
                            x.TotalPages();
                        });
                    });
                });
            }).GeneratePdf(path);
        }

        // ============================================================
        // HELPERS
        // ============================================================

        private static void EnsureValid(ReportTable table, string path)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A file path is required.", nameof(path));
        }

        private static string EnsureExtension(string path, string ext)
        {
            if (string.IsNullOrEmpty(path)) return path;
            if (path.EndsWith(ext, StringComparison.OrdinalIgnoreCase)) return path;
            return path + ext;
        }

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            bool needsQuotes =
                value.Contains(',') ||
                value.Contains('"') ||
                value.Contains('\n') ||
                value.Contains('\r');

            if (!needsQuotes) return value;

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static IEnumerable<string> EscapeCsvAll(IEnumerable<string> values)
        {
            foreach (var v in values)
                yield return EscapeCsv(v ?? string.Empty);
        }
    }
}