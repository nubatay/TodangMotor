using System;
using System.Collections.Generic;
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

        /// <summary>
        /// Optional summary pairs to display in a small box above the table
        /// (PDF only). Example: ("Total products", "5"), ("Total units", "39").
        /// </summary>
        public List<(string Label, string Value)> SummaryItems { get; set; } = new();
    }

    /// <summary>
    /// Turns a ReportTable into a CSV, Excel, or PDF file on disk.
    /// PDF uses QuestPDF's bundled "Lato" font and includes a branded
    /// header, summary box, zebra-striped rows, and page numbers.
    /// </summary>
    public static class ReportService
    {
        private const string PdfFontFamily = "Lato";

        // Brand colors (matching the WinForms theme).
        private const string BrandBlue = "#4D6BFE";
        private const string BrandBlueDk = "#3B55D9";
        private const string TextPrimary = "#1A1A1A";
        private const string TextMuted = "#6B7280";
        private const string Divider = "#E5E7EF";
        private const string SummaryBg = "#F4F6FD";
        private const string ZebraBg = "#FAFBFE";

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

            // Summary (optional) — one line per pair.
            if (table.SummaryItems != null && table.SummaryItems.Count > 0)
            {
                foreach (var item in table.SummaryItems)
                    sb.AppendLine(EscapeCsv($"{item.Label},{item.Value}"));
                sb.AppendLine();
            }

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

            // Row 1: title
            ws.Cell(1, 1).Value = table.Title ?? string.Empty;
            if (colCount > 1)
                ws.Range(1, 1, 1, colCount).Merge();
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 16;
            ws.Cell(1, 1).Style.Font.FontColor = XLColor.FromHtml(TextPrimary);

            // Row 2: subtitle
            ws.Cell(2, 1).Value = table.Subtitle ?? string.Empty;
            if (colCount > 1)
                ws.Range(2, 1, 2, colCount).Merge();
            ws.Cell(2, 1).Style.Font.FontSize = 10;
            ws.Cell(2, 1).Style.Font.FontColor = XLColor.FromHtml(TextMuted);

            int nextRow = 3;

            // Summary block (optional) — 2 rows: labels then values.
            if (table.SummaryItems != null && table.SummaryItems.Count > 0)
            {
                nextRow++;
                int sc = table.SummaryItems.Count;
                for (int i = 0; i < sc; i++)
                {
                    var cellL = ws.Cell(nextRow, i + 1);
                    cellL.Value = table.SummaryItems[i].Label;
                    cellL.Style.Font.FontSize = 9;
                    cellL.Style.Font.FontColor = XLColor.FromHtml(TextMuted);
                    cellL.Style.Fill.BackgroundColor = XLColor.FromHtml(SummaryBg);

                    var cellV = ws.Cell(nextRow + 1, i + 1);
                    cellV.Value = table.SummaryItems[i].Value;
                    cellV.Style.Font.Bold = true;
                    cellV.Style.Font.FontSize = 12;
                    cellV.Style.Fill.BackgroundColor = XLColor.FromHtml(SummaryBg);
                }
                nextRow += 2;
            }

            // Blank row before table.
            nextRow++;

            // Header row
            int headerRow = nextRow;
            for (int c = 0; c < table.Headers.Count; c++)
            {
                var cell = ws.Cell(headerRow, c + 1);
                cell.Value = table.Headers[c] ?? string.Empty;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml(BrandBlue);
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Font.Bold = true;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                cell.Style.Border.BottomBorderColor = XLColor.FromHtml(BrandBlueDk);
            }

            // Data rows
            for (int r = 0; r < table.Rows.Count; r++)
            {
                var row = table.Rows[r];
                for (int c = 0; c < row.Count; c++)
                {
                    var cell = ws.Cell(headerRow + 1 + r, c + 1);
                    cell.Value = row[c] ?? string.Empty;
                    cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                    // Auto-detect numeric column alignment.
                    bool right = c < table.Headers.Count && IsNumericHeader(table.Headers[c]);
                    cell.Style.Alignment.Horizontal = right
                        ? XLAlignmentHorizontalValues.Right
                        : XLAlignmentHorizontalValues.Left;

                    // Zebra striping.
                    if (r % 2 == 1)
                        cell.Style.Fill.BackgroundColor = XLColor.FromHtml(ZebraBg);
                }
            }

            // Footer note
            if (!string.IsNullOrEmpty(table.FooterNote))
            {
                int footRow = headerRow + 1 + table.Rows.Count + 1;
                ws.Cell(footRow, 1).Value = table.FooterNote;
                ws.Cell(footRow, 1).Style.Font.FontColor = XLColor.FromHtml(TextMuted);
                ws.Cell(footRow, 1).Style.Font.FontSize = 9;
                ws.Cell(footRow, 1).Style.Font.Italic = true;
            }

            ws.Columns().AdjustToContents();

            for (int c = 1; c <= colCount; c++)
            {
                if (ws.Column(c).Width < 14)
                    ws.Column(c).Width = 14;
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

            byte[]? logoBytes = TryLoadLogoBytes();

            PdfDocument.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.DefaultTextStyle(x => x
                        .FontSize(10)
                        .FontFamily(PdfFontFamily)
                        .FontColor(TextPrimary));

                    // ================= HEADER =================
                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            // Logo tile (image if present, otherwise styled "T").
                            row.ConstantItem(52).Height(52).Element(logoBox =>
                            {
                                if (logoBytes != null)
                                {
                                    logoBox.Background(BrandBlue)
                                           .Padding(6)
                                           .Image(logoBytes).FitArea();
                                }
                                else
                                {
                                    logoBox.Background(BrandBlue)
                                           .AlignCenter()
                                           .AlignMiddle()
                                           .Text("T")
                                           .FontColor("#FFFFFF")
                                           .FontSize(26)
                                           .Bold();
                                }
                            });

                            row.ConstantItem(14);

                            // Company block.
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("TODANG MOTOR PARTS & ACCESSORIES")
                                    .FontSize(13).Bold().FontColor(TextPrimary);
                                c.Item().Text("Lower Tamugan, Marilog District, Davao City")
                                    .FontSize(9).FontColor(TextMuted);
                                c.Item().Text("Inventory & Sales Management System")
                                    .FontSize(8).FontColor(TextMuted);
                            });
                        });

                        col.Item().PaddingTop(10)
                            .LineHorizontal(1).LineColor(Divider);
                    });

                    // ================= CONTENT =================
                    page.Content().PaddingTop(14).Column(col =>
                    {
                        // Report title + subtitle
                        col.Item().Text(table.Title ?? string.Empty)
                            .FontSize(20).Bold().FontColor(TextPrimary);

                        if (!string.IsNullOrEmpty(table.Subtitle))
                        {
                            col.Item().PaddingTop(2)
                                .Text(table.Subtitle)
                                .FontSize(10).FontColor(TextMuted);
                        }

                        // Summary box
                        if (table.SummaryItems != null && table.SummaryItems.Count > 0)
                        {
                            col.Item().PaddingTop(16).Element(box =>
                            {
                                box.Background(SummaryBg)
                                   .Border(1).BorderColor(Divider)
                                   .Padding(12)
                                   .Row(srow =>
                                   {
                                       int n = table.SummaryItems.Count;
                                       for (int i = 0; i < n; i++)
                                       {
                                           var (label, value) = table.SummaryItems[i];

                                           srow.RelativeItem().Column(sc =>
                                           {
                                               sc.Item().Text(label)
                                                   .FontSize(8).FontColor(TextMuted);
                                               sc.Item().PaddingTop(2)
                                                   .Text(value)
                                                   .FontSize(14).Bold().FontColor(BrandBlue);
                                           });

                                           if (i < n - 1)
                                               srow.ConstantItem(1).Background(Divider);
                                       }
                                   });
                            });
                        }

                        // Data table
                        col.Item().PaddingTop(18).Table(t =>
                        {
                            int nCols = Math.Max(1, table.Headers.Count);

                            t.ColumnsDefinition(c =>
                            {
                                // First column gets more weight (product name),
                                // last column gets a bit more (reason).
                                for (int i = 0; i < nCols; i++)
                                {
                                    if (i == 0) c.RelativeColumn(2.2f);
                                    else if (i == nCols - 1) c.RelativeColumn(1.4f);
                                    else c.RelativeColumn();
                                }
                            });

                            // Header row
                            t.Header(h =>
                            {
                                foreach (var header in table.Headers)
                                {
                                    h.Cell()
                                        .Background(BrandBlue)
                                        .PaddingVertical(10).PaddingHorizontal(10)
                                        .AlignMiddle()
                                        .Text(header ?? string.Empty)
                                        .FontColor("#FFFFFF").Bold().FontSize(10);
                                }
                            });

                            // Body rows
                            for (int r = 0; r < table.Rows.Count; r++)
                            {
                                var row = table.Rows[r];
                                bool zebra = (r % 2 == 1);

                                for (int i = 0; i < table.Headers.Count; i++)
                                {
                                    string cellText = i < row.Count ? (row[i] ?? string.Empty) : string.Empty;
                                    bool rightAlign = IsNumericHeader(table.Headers[i]);

                                    var cell = t.Cell()
                                        .BorderBottom(0.5f).BorderColor(Divider)
                                        .PaddingVertical(9).PaddingHorizontal(10)
                                        .AlignMiddle();

                                    if (zebra)
                                        cell = cell.Background(ZebraBg);

                                    var txt = cell.Text(cellText).FontSize(9);
                                    if (rightAlign)
                                        txt.AlignRight();
                                }
                            }
                        });

                        // Footer note
                        if (!string.IsNullOrEmpty(table.FooterNote))
                        {
                            col.Item().PaddingTop(14)
                                .Text(table.FooterNote)
                                .FontSize(9).Italic().FontColor(TextMuted);
                        }
                    });

                    // ================= FOOTER =================
                    page.Footer().Column(col =>
                    {
                        col.Item().PaddingTop(8)
                            .LineHorizontal(0.5f).LineColor(Divider);

                        col.Item().PaddingTop(6).Row(row =>
                        {
                            row.RelativeItem().Text(x =>
                            {
                                x.DefaultTextStyle(s => s.FontSize(8).FontColor(TextMuted));
                                x.Span("Generated by Todang Motor POS  ·  ");
                                x.Span(DateTime.Now.ToString("MMM d, yyyy h:mm tt"));
                            });

                            row.ConstantItem(140).AlignRight().Text(x =>
                            {
                                x.DefaultTextStyle(s => s.FontSize(8).FontColor(TextMuted));
                                x.Span("Page ");
                                x.CurrentPageNumber();
                                x.Span(" of ");
                                x.TotalPages();
                            });
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

        /// <summary>
        /// Heuristic: is this column header a numeric column?
        /// Used to decide left vs right alignment.
        /// </summary>
        private static bool IsNumericHeader(string? header)
        {
            if (string.IsNullOrEmpty(header)) return false;

            string h = header.ToLowerInvariant();

            string[] numericKeywords =
            {
                "qty", "quantity", "total", "amount", "net", "change",
                "price", "cost", "on hand", "reorder", "starting",
                "ending", "count", "number", "#", "php", "₱",
                "subtotal", "tendered", "units"
            };

            foreach (var kw in numericKeywords)
            {
                if (h.Contains(kw)) return true;
            }

            return false;
        }

        /// <summary>
        /// Tries to load a logo PNG from the app's Assets folder.
        /// Returns null if not present (caller falls back to a text tile).
        /// </summary>
        private static byte[]? TryLoadLogoBytes()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string path = Path.Combine(baseDir, "Assets", "Logo.png");
                if (File.Exists(path))
                    return File.ReadAllBytes(path);
            }
            catch
            {
                // Fall through and return null.
            }
            return null;
        }
    }
}