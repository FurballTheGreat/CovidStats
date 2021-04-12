using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using iText.Layout.Splitting;

namespace CovidStates.OperationsUpdate
{

    public class HseHospital
    {
        public string Name
        {
            get;
            set;
        }

        public int Count
        {
            get;
            set;
        }

        public override string ToString() => $"{Name} ({Count})";
    }

    public class HseOperationsUpdate
    {
        private static string[] _hospitals = new[]
        {
            "St. James's",
            "Mater",
            "Beaumont",
            "Connolly",
            "Tallaght",
            "Letterkenny",
            "Mullingar",
            "UHL",
            "Tullamore",
            "CUH",
            "Drogheda",
            "Mercy",
            "Kilkenny",
            "Portlaoise",
            "St. Vincent's",
            "UHK",
            "UHW",
            "Cavan",
            "CHI Crumlin",
            "GUH",
            "Mayo",
            "Naas",
            "South Tipp",
            "Wexford",
            "CHI Tallaght",
            "CHI Temple St",
            "Navan",
            "Portiuncula",
            "Sligo",
        };

        public HseHospital[] TotalConfirmed
        {
            get;
            set;
        }

        public HseHospital[] TotalConfirmedLast24hrs
        {
            get;
            set;
        }

        public HseHospital[] TotalSuspected
        {
            get;
            set;
        }

        public static bool HasAllHospitals(IEnumerable<string> pLines)
        {
            var all = _hospitals.ToList();

            foreach (var line in pLines)
            {
                if (all.Contains(line))
                    all.Remove(line);
                else
                    return false;

                if (all.Count == 0) return true;
            }

            return false;
        }

        public static HseHospital[] GetHospitals(IEnumerable<string> pLines)
        {
            var result = new List<HseHospital>();
            var names = pLines.Take(_hospitals.Length).ToArray();
            var values = pLines.Skip(_hospitals.Length).Take(_hospitals.Length).ToArray();

            for (var i = 0; i < _hospitals.Length; i++)
            {
                result.Add(new HseHospital
                {
                    Name = names[i],
                    Count =  Int32.Parse(values[i])
                });
            }

            return result.ToArray();
        }


        public enum Stage {  Total, TotalSuspected, Total24Hrs}


        public static HseOperationsUpdate Load(byte[] pData, string pSourceFileName)
        {


            var result = new HseOperationsUpdate {SourceFileName = pSourceFileName};


            var dateRegex = new Regex(@"Previous Day as at (?<preparedDate>\d+/\d+/\d+)");
          
            var pdfDocument = new PdfDocument(new PdfReader(new MemoryStream(pData)));
            var strategy = new SimpleTextExtractionStrategy();
            var gotPreparedBy = false;
            var gotHeatMap = false;
            var stage = Stage.Total;
            string previous = null;
            for (int i = 1; i <= pdfDocument.GetNumberOfPages(); ++i)
            {
                var page = pdfDocument.GetPage(i);

                string text = PdfTextExtractor.GetTextFromPage(page, strategy);
                var actualText = previous != null ? text.Substring(previous.Length) : text;
                if (text.Contains("Total Confirmed COVID-19 Cases"))
                {
                    var lines = text.Split("\n");

                    for (var j = 0; j < lines.Length; j++)
                    {
                        var matches = dateRegex.Matches(lines[j]);
                        if (matches.Count > 0 && result.CoverDate.Year>0)
                        {
                            result.CoverDate = DateTime.Parse(matches.First().Groups["preparedDate"].Value);
                        }
                        if(HasAllHospitals(lines.Skip(j)))
                        {
                            var allDone = false;
                            var hospitals = GetHospitals(lines.Skip(j));
                            switch (stage)
                            {
                                case Stage.Total:
                                    result.TotalConfirmed = hospitals;
                                    stage = Stage.TotalSuspected;
                                    break;
                                case Stage.TotalSuspected:
                                    result.TotalSuspected = hospitals;
                                    stage = Stage.Total24Hrs;
                                    break;
                                case Stage.Total24Hrs:
                                    result.TotalConfirmedLast24hrs = hospitals;
                                    allDone = true;
                                    break;
                                default:
                                    throw new ArgumentOutOfRangeException();
                            }

                            if (allDone) return result;
                        }
                    }
                }

                previous = text;
            }

            return result;
        }

        public DateTime CoverDate { get; set; }

        public string SourceFileName { get; set; }


        public override string ToString() => $"{CoverDate}";
    }
}
