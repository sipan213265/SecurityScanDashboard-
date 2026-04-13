using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SecurityScanDashboard.Models;

namespace SecurityScanDashboard.Services
{
    public interface IPdfReportService
    {
        byte[] GenerateScanReport(Scan scan);
    }

    public class PdfReportService : IPdfReportService
    {
        public PdfReportService()
        {
            // Set QuestPDF license
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public byte[] GenerateScanReport(Scan scan)
        {
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                    // Header
                    page.Header()
                        .Height(80)
                        .Background(Colors.Blue.Darken3)
                        .Padding(10)
                        .AlignCenter()
                        .AlignMiddle()
                        .Text(text =>
                        {
                            text.Span("Security Scan Report").FontSize(24).FontColor(Colors.White).Bold();
                        });

                    // Content
                    page.Content()
                        .PaddingVertical(10)
                        .Column(column =>
                        {
                            column.Spacing(10);

                            // Scan Information Section
                            column.Item().Element(container => ScanInfoSection(container, scan));

                            // Vulnerabilities Summary
                            column.Item().Element(container => VulnerabilitySummary(container, scan));

                            // Detailed Vulnerabilities
                            if (scan.Vulnerabilities.Any())
                            {
                                column.Item().Element(container => DetailedVulnerabilities(container, scan));
                            }
                        });

                    // Footer
                    page.Footer()
                        .Height(30)
                        .AlignCenter()
                        .Text(text =>
                        {
                            text.Span("Generated on: ").FontSize(9).FontColor(Colors.Grey.Darken1);
                            text.Span(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC")).FontSize(9).FontColor(Colors.Grey.Darken1);
                            text.Span(" | Page ").FontSize(9).FontColor(Colors.Grey.Darken1);
                            text.CurrentPageNumber().FontSize(9).FontColor(Colors.Grey.Darken1);
                            text.Span(" of ").FontSize(9).FontColor(Colors.Grey.Darken1);
                            text.TotalPages().FontSize(9).FontColor(Colors.Grey.Darken1);
                        });
                });
            });

            return document.GeneratePdf();
        }

        private void ScanInfoSection(IContainer container, Scan scan)
        {
            container
                .Border(1)
                .BorderColor(Colors.Grey.Lighten2)
                .Background(Colors.Grey.Lighten4)
                .Padding(15)
                .Column(column =>
                {
                    column.Spacing(8);

                    column.Item().Text("Scan Information").FontSize(16).Bold().FontColor(Colors.Blue.Darken2);

                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Text(text =>
                        {
                            text.Span("Repository: ").Bold();
                            text.Span(scan.Repository.Name);
                        });

                        row.RelativeItem().Text(text =>
                        {
                            text.Span("Owner: ").Bold();
                            text.Span(scan.Repository.Owner);
                        });
                    });

                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Text(text =>
                        {
                            text.Span("Scan Type: ").Bold();
                            text.Span(scan.Type.ToString());
                        });

                        row.RelativeItem().Text(text =>
                        {
                            text.Span("Tool: ").Bold();
                            text.Span(scan.ToolName);
                        });
                    });

                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Text(text =>
                        {
                            text.Span("Status: ").Bold();
                            text.Span(scan.Status.ToString());
                        });

                        row.RelativeItem().Text(text =>
                        {
                            text.Span("Started: ").Bold();
                            text.Span(scan.StartedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                        });
                    });

                    if (scan.CompletedAt.HasValue)
                    {
                        var duration = (scan.CompletedAt.Value - scan.StartedAt).TotalMinutes;
                        column.Item().Text(text =>
                        {
                            text.Span("Completed: ").Bold();
                            text.Span($"{scan.CompletedAt.Value:yyyy-MM-dd HH:mm:ss} (Duration: {duration:F1} minutes)");
                        });
                    }

                    column.Item().Text(text =>
                    {
                        text.Span("Repository URL: ").Bold();
                        text.Span(scan.Repository.Url).FontSize(9);
                    });
                });
        }

        private void VulnerabilitySummary(IContainer container, Scan scan)
        {
            var criticalCount = scan.Vulnerabilities.Count(v => v.Severity == SeverityLevel.Critical);
            var highCount = scan.Vulnerabilities.Count(v => v.Severity == SeverityLevel.High);
            var mediumCount = scan.Vulnerabilities.Count(v => v.Severity == SeverityLevel.Medium);
            var lowCount = scan.Vulnerabilities.Count(v => v.Severity == SeverityLevel.Low);

            container
                .Border(1)
                .BorderColor(Colors.Grey.Lighten2)
                .Padding(15)
                .Column(column =>
                {
                    column.Spacing(8);

                    column.Item().Text("Vulnerability Summary").FontSize(16).Bold().FontColor(Colors.Blue.Darken2);

                    column.Item().Text(text =>
                    {
                        text.Span("Total Vulnerabilities: ").Bold();
                        text.Span(scan.Vulnerabilities.Count.ToString());
                    });

                    // Severity breakdown table
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(1);
                        });

                        // Critical
                        table.Cell().Border(0.5f).BorderColor(Colors.Red.Lighten3)
                            .Background(Colors.Red.Lighten4).Padding(8)
                            .Text("Critical").Bold().FontColor(Colors.Red.Darken2);
                        table.Cell().Border(0.5f).BorderColor(Colors.Red.Lighten3)
                            .Background(Colors.Red.Lighten4).Padding(8)
                            .AlignRight().Text(criticalCount.ToString()).Bold();

                        // High
                        table.Cell().Border(0.5f).BorderColor(Colors.Orange.Lighten3)
                            .Background(Colors.Orange.Lighten4).Padding(8)
                            .Text("High").Bold().FontColor(Colors.Orange.Darken2);
                        table.Cell().Border(0.5f).BorderColor(Colors.Orange.Lighten3)
                            .Background(Colors.Orange.Lighten4).Padding(8)
                            .AlignRight().Text(highCount.ToString()).Bold();

                        // Medium
                        table.Cell().Border(0.5f).BorderColor(Colors.Blue.Lighten3)
                            .Background(Colors.Blue.Lighten4).Padding(8)
                            .Text("Medium").Bold().FontColor(Colors.Blue.Darken2);
                        table.Cell().Border(0.5f).BorderColor(Colors.Blue.Lighten3)
                            .Background(Colors.Blue.Lighten4).Padding(8)
                            .AlignRight().Text(mediumCount.ToString()).Bold();

                        // Low
                        table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2)
                            .Background(Colors.Grey.Lighten3).Padding(8)
                            .Text("Low").Bold().FontColor(Colors.Grey.Darken2);
                        table.Cell().Border(0.5f).BorderColor(Colors.Grey.Lighten2)
                            .Background(Colors.Grey.Lighten3).Padding(8)
                            .AlignRight().Text(lowCount.ToString()).Bold();
                    });
                });
        }

        private void DetailedVulnerabilities(IContainer container, Scan scan)
        {
            container
                .Border(1)
                .BorderColor(Colors.Grey.Lighten2)
                .Padding(15)
                .Column(column =>
                {
                    column.Spacing(8);

                    column.Item().Text("Detailed Vulnerabilities").FontSize(16).Bold().FontColor(Colors.Blue.Darken2);

                    var groupedVulnerabilities = scan.Vulnerabilities
                        .OrderByDescending(v => v.Severity)
                        .GroupBy(v => v.Severity);

                    foreach (var group in groupedVulnerabilities)
                    {
                        column.Item().PageBreak();
                        
                        column.Item().Text($"{group.Key} Severity ({group.Count()})")
                            .FontSize(14).Bold().FontColor(GetSeverityColor(group.Key));

                        foreach (var vuln in group)
                        {
                            column.Item().PaddingTop(10).Column(vulnColumn =>
                            {
                                vulnColumn.Item()
                                    .Border(1)
                                    .BorderColor(GetSeverityColor(vuln.Severity))
                                    .Background(Colors.Grey.Lighten5)
                                    .Padding(10)
                                    .Column(c =>
                                    {
                                        c.Item().Text(vuln.Title).FontSize(12).Bold();

                                        if (!string.IsNullOrEmpty(vuln.Description))
                                        {
                                            c.Item().PaddingTop(5).Text(vuln.Description).FontSize(10);
                                        }

                                        if (!string.IsNullOrEmpty(vuln.FilePath))
                                        {
                                            c.Item().PaddingTop(5).Text(text =>
                                            {
                                                text.Span("File: ").Bold().FontSize(9);
                                                text.Span(vuln.FilePath).FontSize(9).FontColor(Colors.Grey.Darken1);
                                                if (vuln.LineNumber.HasValue)
                                                {
                                                    text.Span($" (Line: {vuln.LineNumber})").FontSize(9).FontColor(Colors.Grey.Darken1);
                                                }
                                            });
                                        }

                                        if (!string.IsNullOrEmpty(vuln.CweId))
                                        {
                                            c.Item().PaddingTop(3).Text(text =>
                                            {
                                                text.Span("CWE: ").Bold().FontSize(9);
                                                text.Span(vuln.CweId).FontSize(9);
                                            });
                                        }

                                        if (!string.IsNullOrEmpty(vuln.CveId))
                                        {
                                            c.Item().PaddingTop(3).Text(text =>
                                            {
                                                text.Span("CVE: ").Bold().FontSize(9);
                                                text.Span(vuln.CveId).FontSize(9);
                                            });
                                        }

                                        c.Item().PaddingTop(3).Text(text =>
                                        {
                                            text.Span("Detected: ").Bold().FontSize(9);
                                            text.Span(vuln.DetectedAt.ToString("yyyy-MM-dd HH:mm:ss")).FontSize(9);
                                        });
                                    });
                            });
                        }
                    }
                });
        }

        private string GetSeverityColor(SeverityLevel severity)
        {
            return severity switch
            {
                SeverityLevel.Critical => Colors.Red.Darken2,
                SeverityLevel.High => Colors.Orange.Darken2,
                SeverityLevel.Medium => Colors.Blue.Darken2,
                SeverityLevel.Low => Colors.Grey.Darken2,
                _ => Colors.Grey.Darken1
            };
        }
    }
}
