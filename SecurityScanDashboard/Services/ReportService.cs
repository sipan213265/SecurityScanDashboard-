using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SecurityScanDashboard.Models;
using System.Text;

namespace SecurityScanDashboard.Services
{
    public interface IReportService
    {
        byte[] GeneratePdfReport(Scan scan);
        byte[] GenerateCsvReport(Scan scan);
    }

    public class ReportService : IReportService
    {
        private static readonly Dictionary<string, string> OwaspMapping = new()
        {
            { "CWE-79", "A03:2021 - Injection" },
            { "CWE-89", "A03:2021 - Injection" },
            { "CWE-95", "A03:2021 - Injection" },
            { "CWE-134", "A03:2021 - Injection" },
            { "CWE-798", "A07:2021 - Authentication Failures" },
            { "CWE-321", "A02:2021 - Cryptographic Failures" },
            { "CWE-353", "A02:2021 - Cryptographic Failures" },
            { "CWE-601", "A01:2021 - Broken Access Control" },
            { "CWE-73", "A01:2021 - Broken Access Control" },
            { "CWE-611", "A05:2021 - Security Misconfiguration" },
            { "CWE-548", "A05:2021 - Security Misconfiguration" }
        };

        public ReportService()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public byte[] GeneratePdfReport(Scan scan)
        {
            var criticalCount = scan.Vulnerabilities.Count(v => v.Severity == SeverityLevel.Critical);
            var highCount = scan.Vulnerabilities.Count(v => v.Severity == SeverityLevel.High);
            var mediumCount = scan.Vulnerabilities.Count(v => v.Severity == SeverityLevel.Medium);
            var lowCount = scan.Vulnerabilities.Count(v => v.Severity == SeverityLevel.Low);
            
            var riskScore = CalculateRiskScore(criticalCount, highCount, mediumCount, lowCount);

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Grey.Darken3));

                    // --- DÜZELTME 1: Header ---
                    // .Height(100) kaldırıldı. Sabit yükseklik yerine içeriğin yüksekliği belirlemesine izin veriyoruz.
                    // Böylece metinler taşarsa hata vermez.
                    page.Header().BorderBottom(3).BorderColor(Colors.Blue.Darken2).PaddingBottom(5).Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("APPLICATION SECURITY").FontSize(9).Bold().FontColor(Colors.Grey.Medium);
                                c.Item().Text("VULNERABILITY ASSESSMENT REPORT").FontSize(16).Bold().FontColor(Colors.Blue.Darken3);
                                c.Item().PaddingTop(2).Text("Static Application Security Testing").FontSize(8).FontColor(Colors.Grey.Medium);
                            });
                            
                            row.ConstantItem(120).AlignRight().Column(c =>
                            {
                                c.Item().AlignRight().Text(DateTime.Now.ToString("MMMM dd, yyyy")).FontSize(9).FontColor(Colors.Grey.Medium);
                                c.Item().AlignRight().Text($"Report ID: {scan.Id:D6}").FontSize(8).FontColor(Colors.Grey.Darken1);
                            });
                        });
                        
                        col.Item().PaddingTop(8).PaddingBottom(5).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("PROJECT").FontSize(7).Bold().FontColor(Colors.Grey.Medium);
                                // Layout engine automatically handles text wrapping
                                c.Item().Text($"{scan.Repository.Owner}/{scan.Repository.Name}")
                                       .FontSize(9).Bold().FontColor(Colors.Grey.Darken3);
                            });
                            
                            row.ConstantItem(120).AlignRight().Column(c =>
                            {
                                c.Item().AlignRight().Text("SCAN ENGINE").FontSize(7).Bold().FontColor(Colors.Grey.Medium);
                                c.Item().AlignRight().Text(scan.ToolName).FontSize(9).FontColor(Colors.Grey.Darken2);
                            });
                        });
                    });

                    page.Content().PaddingVertical(20).Column(column =>
                    {
                        // Executive Summary Card
                        column.Item().Border(1).BorderColor(Colors.Grey.Lighten1).Column(col =>
                        {
                            col.Item().Background(Colors.Grey.Lighten4).Padding(10).Text("EXECUTIVE SUMMARY").FontSize(12).Bold().FontColor(Colors.Grey.Darken3);
                            
                            col.Item().Padding(15).Row(row =>
                            {
                                // Risk Score Badge
                                row.ConstantItem(150).Border(2).BorderColor(GetRiskBorderColor(riskScore)).Background(Colors.White).Padding(15).Column(c =>
                                {
                                    c.Item().AlignCenter().Text("RISK SCORE").FontSize(8).Bold().FontColor(Colors.Grey.Medium);
                                    c.Item().AlignCenter().PaddingVertical(5).Text($"{riskScore:F1}").FontSize(36).Bold().FontColor(GetRiskBorderColor(riskScore));
                                    c.Item().AlignCenter().Text(GetRiskLevel(riskScore).ToUpper()).FontSize(9).Bold().FontColor(GetRiskBorderColor(riskScore));
                                });
                                
                                row.Spacing(15);
                                
                                // Key Metrics
                                row.RelativeItem().Column(c =>
                                {
                                    c.Item().PaddingBottom(8).Text(text =>
                                    {
                                        text.Span("Total Vulnerabilities: ").FontSize(10);
                                        text.Span($"{scan.Vulnerabilities.Count}").FontSize(10).Bold();
                                    });
                                    
                                    c.Item().PaddingBottom(5).Row(r =>
                                    {
                                        r.ConstantItem(15).Height(15).Width(15).Background(Colors.Red.Lighten1);
                                        r.ConstantItem(5);
                                        r.RelativeItem().AlignMiddle().Text($"Critical: {criticalCount}").FontSize(9);
                                        
                                        r.ConstantItem(15).Height(15).Width(15).Background(Colors.Orange.Medium);
                                        r.ConstantItem(5);
                                        r.RelativeItem().AlignMiddle().Text($"High: {highCount}").FontSize(9);
                                    });
                                    
                                    c.Item().Row(r =>
                                    {
                                        r.ConstantItem(15).Height(15).Width(15).Background(Colors.Yellow.Darken1);
                                        r.ConstantItem(5);
                                        r.RelativeItem().AlignMiddle().Text($"Medium: {mediumCount}").FontSize(9);
                                        
                                        r.ConstantItem(15).Height(15).Width(15).Background(Colors.Blue.Lighten2);
                                        r.ConstantItem(5);
                                        r.RelativeItem().AlignMiddle().Text($"Low: {lowCount}").FontSize(9);
                                    });
                                });
                            });
                        });

                        column.Item().PaddingTop(20);

                        // Vulnerability Distribution
                        column.Item().Text("VULNERABILITY DISTRIBUTION BY SEVERITY").FontSize(11).Bold().FontColor(Colors.Grey.Darken3);
                        column.Item().PaddingTop(10).Border(1).BorderColor(Colors.Grey.Lighten1).Padding(15).Column(col =>
                        {
                            var maxCount = Math.Max(Math.Max(criticalCount, highCount), Math.Max(mediumCount, lowCount));
                            if (maxCount == 0) maxCount = 1;

                            AddSeverityBar(col, "CRITICAL", criticalCount, maxCount, Colors.Red.Lighten1);
                            AddSeverityBar(col, "HIGH", highCount, maxCount, Colors.Orange.Medium);
                            AddSeverityBar(col, "MEDIUM", mediumCount, maxCount, Colors.Yellow.Darken1);
                            AddSeverityBar(col, "LOW", lowCount, maxCount, Colors.Blue.Lighten2);
                        });

                        column.Item().PaddingTop(20);

                        // OWASP Mapping
                        var owaspCats = GetOwaspCategories(scan.Vulnerabilities);
                        if (owaspCats.Any())
                        {
                            column.Item().Text("OWASP TOP 10:2021 CLASSIFICATION").FontSize(11).Bold().FontColor(Colors.Grey.Darken3);
                            column.Item().PaddingTop(10).Border(1).BorderColor(Colors.Grey.Lighten1).Table(table =>
                            {
                                table.ColumnsDefinition(c =>
                                {
                                    c.RelativeColumn(5);
                                    c.ConstantColumn(60);
                                });

                                table.Cell().Background(Colors.Grey.Lighten4).Padding(8).Text("Category").FontSize(9).Bold();
                                table.Cell().Background(Colors.Grey.Lighten4).Padding(8).AlignRight().Text("Count").FontSize(9).Bold();

                                foreach (var cat in owaspCats)
                                {
                                    table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(8).Text(cat.Key).FontSize(8);
                                    table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(8).AlignRight().Text(cat.Value.ToString()).FontSize(9).Bold();
                                }
                            });
                        }

                        column.Item().PaddingTop(20);

                        // Scan Information
                        column.Item().Text("SCAN INFORMATION").FontSize(11).Bold().FontColor(Colors.Grey.Darken3);
                        column.Item().PaddingTop(10).Border(1).BorderColor(Colors.Grey.Lighten1).Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.ConstantColumn(120);
                                c.RelativeColumn();
                            });

                            AddInfoRow(table, "Repository URL", scan.Repository.Url ?? "N/A");
                            AddInfoRow(table, "Scan Type", scan.Type.ToString());
                            AddInfoRow(table, "Started At", scan.StartedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                            if (scan.CompletedAt.HasValue)
                            {
                                AddInfoRow(table, "Completed At", scan.CompletedAt.Value.ToString("yyyy-MM-dd HH:mm:ss"));
                                AddInfoRow(table, "Duration", $"{(scan.CompletedAt.Value - scan.StartedAt).TotalMinutes:F1} minutes");
                            }
                        });

                        // Vulnerability Details
                        if (scan.Vulnerabilities.Any())
                        {
                            column.Item().PageBreak();
                            column.Item().Text("DETAILED VULNERABILITY FINDINGS").FontSize(13).Bold().FontColor(Colors.Grey.Darken3);
                            column.Item().PaddingTop(5).Text("Top 20 most critical vulnerabilities identified during the scan").FontSize(8).FontColor(Colors.Grey.Medium);
                            column.Item().PaddingTop(15);

                            var vulnList = scan.Vulnerabilities.OrderByDescending(v => v.Severity).Take(20).ToList();
                            int counter = 1;
                            
                            foreach (var vuln in vulnList)
                            {
                                column.Item().PaddingBottom(12).Border(1).BorderColor(Colors.Grey.Lighten1).Column(vCol =>
                                {
                                    vCol.Item().Background(Colors.Grey.Lighten4).Padding(8).Row(r =>
                                    {
                                        r.ConstantItem(30).Text($"#{counter}").FontSize(9).Bold().FontColor(Colors.Grey.Medium);
                                        r.RelativeItem().Text(vuln.Title ?? "Untitled Vulnerability").FontSize(10).Bold().FontColor(Colors.Grey.Darken3);
                                        r.ConstantItem(80).AlignRight().Element(c => SeverityBadge(c, vuln.Severity));
                                    });

                                    vCol.Item().Padding(10).Column(c =>
                                    {
                                        if (!string.IsNullOrEmpty(vuln.CweId))
                                        {
                                            var owasp = GetOwaspCategory(vuln.CweId);
                                            c.Item().PaddingBottom(5).Row(r =>
                                            {
                                                r.ConstantItem(60).Text("CWE ID:").FontSize(8).Bold().FontColor(Colors.Grey.Medium);
                                                r.RelativeItem().Text($"{vuln.CweId}" + (owasp != null ? $"  •  {owasp}" : "")).FontSize(8).FontColor(Colors.Grey.Darken2);
                                            });
                                        }

                                        if (!string.IsNullOrEmpty(vuln.FilePath))
                                        {
                                            c.Item().PaddingBottom(5).Row(r =>
                                            {
                                                r.ConstantItem(60).Text("Location:").FontSize(8).Bold().FontColor(Colors.Grey.Medium);
                                                r.RelativeItem().Text($"{Path.GetFileName(vuln.FilePath)}  (Line {vuln.LineNumber})").FontSize(8).FontFamily("Courier New").FontColor(Colors.Blue.Darken2);
                                            });
                                        }

                                        if (!string.IsNullOrEmpty(vuln.Description))
                                        {
                                            var desc = vuln.Description.Length > 200 ? vuln.Description.Substring(0, 200) + "..." : vuln.Description;
                                            c.Item().PaddingTop(3).Background(Colors.Grey.Lighten5).Padding(8).Text(desc).FontSize(8).LineHeight(1.3f).FontColor(Colors.Grey.Darken2);
                                        }
                                    });
                                });
                                counter++;
                            }

                            if (scan.Vulnerabilities.Count > 20)
                            {
                                column.Item().PaddingTop(15).Border(1).BorderColor(Colors.Blue.Lighten2).Background(Colors.Blue.Lighten5).Padding(10).Text($"Note: {scan.Vulnerabilities.Count - 20} additional vulnerabilities were identified. Please refer to the CSV export for the complete list.")
                                    .FontSize(8).Italic().FontColor(Colors.Blue.Darken2);
                            }
                        }
                    });

                    page.Footer().BorderTop(1).BorderColor(Colors.Grey.Lighten2).PaddingTop(10).Row(row =>
                    {
                        row.RelativeItem().Text($"Generated on {DateTime.Now:MMMM dd, yyyy} at {DateTime.Now:HH:mm}").FontSize(7).FontColor(Colors.Grey.Medium);
                        row.RelativeItem().AlignRight().Text(t =>
                        {
                            t.Span("Page ").FontSize(7).FontColor(Colors.Grey.Medium);
                            t.CurrentPageNumber().FontSize(7).FontColor(Colors.Grey.Medium);
                            t.Span(" of ").FontSize(7).FontColor(Colors.Grey.Medium);
                            t.TotalPages().FontSize(7).FontColor(Colors.Grey.Medium);
                        });
                    });
                });
            });

            return document.GeneratePdf();
        }

        // --- DÜZELTME 2: AddSeverityBar ---
        // 0 zafiyet durumunda RelativeItem(0) çağırmamak için kontrol eklendi.
        private void AddSeverityBar(ColumnDescriptor col, string label, int count, int maxCount, string color)
        {
            col.Item().PaddingBottom(10).Row(r =>
            {
                r.ConstantItem(80).AlignMiddle().Text(label).FontSize(9).Bold().FontColor(Colors.Grey.Darken2);
                r.RelativeItem().AlignMiddle().Height(25).Border(1).BorderColor(Colors.Grey.Lighten2).Row(bar =>
                {
                    var percentage = maxCount > 0 ? (count * 100.0 / maxCount) : 0;
                    
                    // Eğer percentage > 0 ise dolu barı çiz. 0 ise hiç çizme (hata vermemesi için).
                    if (percentage > 0)
                    {
                        bar.RelativeItem((float)percentage).Background(color).AlignMiddle().PaddingLeft(8).Text(count.ToString()).FontSize(10).Bold().FontColor(Colors.White);
                    }
                    
                    // Eğer %100 değilse boş alanı (gri) çiz.
                    if (percentage < 100)
                    {
                        bar.RelativeItem((float)(100 - percentage)).Background(Colors.Grey.Lighten5);
                    }
                });
            });
        }

        private void SeverityBadge(IContainer container, SeverityLevel severity)
        {
            var (color, text) = severity switch
            {
                SeverityLevel.Critical => (Colors.Red.Lighten1, "CRITICAL"),
                SeverityLevel.High => (Colors.Orange.Medium, "HIGH"),
                SeverityLevel.Medium => (Colors.Yellow.Darken1, "MEDIUM"),
                _ => (Colors.Blue.Lighten2, "LOW")
            };
            
            container.Background(color).PaddingVertical(3).PaddingHorizontal(5).AlignCenter().Text(text).FontSize(7).Bold().FontColor(Colors.White);
        }

        private void AddInfoRow(TableDescriptor table, string label, string value)
        {
            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(8).Text(label).FontSize(8).Bold().FontColor(Colors.Grey.Medium);
            table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(8).Text(value).FontSize(8).FontColor(Colors.Grey.Darken2);
        }

        private string GetRiskBorderColor(double score) => score >= 7 ? Colors.Red.Medium : score >= 4 ? Colors.Orange.Medium : Colors.Green.Medium;

        public byte[] GenerateCsvReport(Scan scan)
        {
            var csv = new StringBuilder();
            csv.AppendLine("Severity,Title,Description,File,Line,CWE,OWASP,Detected");

            foreach (var v in scan.Vulnerabilities.OrderByDescending(x => x.Severity))
            {
                csv.AppendLine($"\"{v.Severity}\",\"{Esc(v.Title)}\",\"{Esc(v.Description)}\",\"{Esc(v.FilePath)}\",\"{v.LineNumber}\",\"{Esc(v.CweId)}\",\"{Esc(GetOwaspCategory(v.CweId))}\",\"{v.DetectedAt:yyyy-MM-dd HH:mm:ss}\"");
            }

            return Encoding.UTF8.GetBytes(csv.ToString());
        }

        private double CalculateRiskScore(int critical, int high, int medium, int low)
        {
            double score = (critical * 10) + (high * 7.5) + (medium * 5) + (low * 2.5);
            double max = (critical + high + medium + low) * 10.0;
            return max > 0 ? (score / max) * 10.0 : 0;
        }

        private string GetRiskLevel(double score) => score switch
        {
            >= 9 => "Critical Risk",
            >= 7 => "High Risk",
            >= 4 => "Medium Risk",
            _ => "Low Risk"
        };

        private List<KeyValuePair<string, int>> GetOwaspCategories(ICollection<Vulnerability> vulns)
        {
            return vulns
                .Select(v => GetOwaspCategory(v.CweId))
                .Where(o => !string.IsNullOrEmpty(o))
                .GroupBy(o => o)
                .OrderByDescending(g => g.Count())
                .Take(5)
                .Select(g => new KeyValuePair<string, int>(g.Key!, g.Count()))
                .ToList();
        }

        private string? GetOwaspCategory(string? cweId)
        {
            if (string.IsNullOrEmpty(cweId)) return null;
            var match = System.Text.RegularExpressions.Regex.Match(cweId, @"CWE-(\d+)");
            if (!match.Success) return null;
            var cwe = $"CWE-{match.Groups[1].Value}";
            return OwaspMapping.ContainsKey(cwe) ? OwaspMapping[cwe] : null;
        }

        private string Esc(string? s) => string.IsNullOrEmpty(s) ? "" : s.Replace("\"", "\"\"");
    }
}