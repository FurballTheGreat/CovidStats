using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CovidStats.DailyEpidemiology;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;


namespace CovidStats.WeeklyEpidemiology
{

    public class HpscWeeklyHeatmapEntry
    {
        public int Index { get; set; }
        public string Name { get; set; }
        public decimal Value { get; set; }
    }
    public class HpscWeeklyHeatmapRow
    {
        public int Week { get; set; }

        public HpscWeeklyHeatmapEntry[] Entries { get; set; }
    }
    public class HpscWeeklyEpidemiology
    {
        public string SourceFileName { get; set; }
        public DateTime PreparedDate { get; set; }

        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }

        public HpscWeeklyHeatmapRow[] AgeHeatmap { get; set; }

        public static HpscWeeklyEpidemiology Load(byte[] pData, string pSourceFileName)
        {
            HspcAbsoluteAndPercent ParseAbsoluteAndPercent(string[] pValues) => new HspcAbsoluteAndPercent
            {
                Absolute = Int32.Parse(pValues[0].Replace(",", "")),
                Percent = Decimal.Parse(pValues[1])
            };

            var result = new HpscWeeklyEpidemiology { SourceFileName = pSourceFileName};


            var reportPreparedByRegex = new Regex(@"Epi\w* Team, HPSC, (?<preparedDate>\d+/\d+/\d+)");
            var heatmapRegex = new Regex(@"Heat map of weekly age-specific incidence rates");
            var heatmapStartRegex = new Regex(@"0-4 5-12 13-18 19-24 25-34 35-44 45-54 55-64 65-74 75-84 85");

            var pdfDocument = new PdfDocument(new PdfReader(new MemoryStream(pData)));
            var strategy = new SimpleTextExtractionStrategy();
            var gotPreparedBy = false;
            var gotHeatMap = false;
            for (int i = 1; i <= pdfDocument.GetNumberOfPages(); ++i)
            {
                var page = pdfDocument.GetPage(i);

                string text = PdfTextExtractor.GetTextFromPage(page, strategy);
                if (!gotPreparedBy)
                {
                    var matches = reportPreparedByRegex.Matches(text);
                    if (matches.Count > 0)
                    {
                        result.PreparedDate = DateTime.Parse(matches.First().Groups["preparedDate"].Value);

                        gotPreparedBy = true;
                    }
                }

                if (!gotHeatMap)
                {
                    if (heatmapRegex.IsMatch(text))
                    {
                        var rows = new List<HpscWeeklyHeatmapRow>();
                        var lines =text.Split("\n").SkipWhile(pX => !heatmapStartRegex.IsMatch(pX)).TakeWhile(pX => char.IsDigit(pX[0])).ToArray();
                        var valNames = lines.First().Split(" ", StringSplitOptions.RemoveEmptyEntries);
                        foreach (var line in lines.Skip(1))
                        {
                            var splits = line.Split(" ", StringSplitOptions.RemoveEmptyEntries);
                            var entries = new List<HpscWeeklyHeatmapEntry>();
                            for (var j = 0; j < valNames.Length; j++)
                                entries.Add(new HpscWeeklyHeatmapEntry { Name = valNames[j], Value = decimal.Parse(splits[j + 1]), Index = j});
                            rows.Add(new HpscWeeklyHeatmapRow
                            {
                                Week = Int32.Parse(splits[0]),
                                Entries = entries.ToArray()
                                
                            });
                        }

                        result.AgeHeatmap = rows.ToArray();
                        gotHeatMap = true;
                    }
                }
            }
            pdfDocument.Close();

            
            return result;
        }

        public static HpscWeeklyHeatmapRow[] GetCombinedHeatmap(IEnumerable<HpscWeeklyEpidemiology> pParsedReports)
        {
            var reports = new List<HpscWeeklyEpidemiology>(pParsedReports);
            reports.Sort((pLeft,pRight)=>pLeft.PreparedDate.CompareTo(pRight.PreparedDate));
            var result = new List<HpscWeeklyHeatmapRow>();
            foreach (var report in reports)
            {
                foreach (var row in report.AgeHeatmap)
                {
                    result.RemoveAll(pX => pX.Week == row.Week);
                    result.Add(row);
                }
            }
            result.Sort((pLeft,pRight)=>pLeft.Week.CompareTo(pRight.Week));
            return result.ToArray();
        }
    }
}
