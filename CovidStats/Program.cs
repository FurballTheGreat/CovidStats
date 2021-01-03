using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Xml;
using UglyToad.PdfPig;
//using iTextSharp.text.pdf;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace CovidStats
{
    class Program
    {
        static HseSchoolsSummary LoadSummary(string pFileName)
        {
            byte[] listeByte;
            try
            {

                var requestFichier = WebRequest.Create(pFileName);

                //    requestFichier.Method = WebRequestMethods.Ftp.DownloadFile;
                using (WebResponse response = requestFichier.GetResponse())
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        response.GetResponseStream().CopyTo(ms);
                        listeByte = ms.ToArray();
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;

            }

            IEnumerable<HseSchoolsSummaryValue> ParseSchoolValues(string pText, string[] pNames)
            {
                for (var i = 0; i < pNames.Length; i++)
                {
                    var pos = pText.IndexOf(pNames[i], StringComparison.Ordinal);
                    if (pos == -1) throw new InvalidDataException("This shouldn't happen ;)");
                    var substr = pText.Substring(pos + pNames[i].Length);
                    var splits = substr.Split(" ", StringSplitOptions.RemoveEmptyEntries);
                    //splits = splits[0].Split(" ");
                    yield return new HseSchoolsSummaryValue
                    {
                        Name = pNames[i],
                        CumulativeToDate = Decimal.Parse(splits[0].Replace(",", "").Replace("%", "")),
                        WeekValue = Decimal.Parse(splits[1].Replace(",", "").Replace("%", ""))

                    };
                }
            }

            IEnumerable<HseSchoolsFacilityValue> ParseFacilityValues(string pText, string[] pNames)
            {
                
                for (var i = 0; i < pNames.Length; i++)
                {
                    var pos = pText.IndexOf(pNames[i], StringComparison.Ordinal);
                    if (pos == -1) throw new InvalidDataException("This shouldn't happen ;)");
                    var substr = pText.Substring(pos + pNames[i].Length);
                    var splits = substr.Split(" ", StringSplitOptions.RemoveEmptyEntries);
                    //splits = splits[0].Split(" ");
                    yield return GetValue(pNames, i, splits);
                }
            }

            var allFaciltyValues = new List<HseSchoolsFacilityValue>();
            HseSchoolsSummaryValue[] values = null;
            using (var pdf = PdfDocument.Open(listeByte))
            {
                foreach (var page in pdf.GetPages())
                {
                    // Either extract based on order in the underlying document with newlines and spaces.
                    var text = ContentOrderTextExtractor.GetText(page);

                    // Or based on grouping letters into words.
                    var otherText = string.Join(" ", page.GetWords());

                    // Or the raw text of the page's content stream.
                    var rawText = page.Text;
                    var lines = rawText.Split("\n");
                    if (rawText.Contains("Summary of Mass Testing in Schools & Childcare"))
                    {

                        values = ParseSchoolValues(rawText, new[]
                        {
                            "Number Tested",
                            "Number of Detected Cases",
                            "Detection Rate %",
                            "Detected 0-17",
                            "Detected 18+",
                            "No. of Facilities Tested",
                            "Facilities with Detected Case"
                        }).ToArray();
                        var tableStr =
                            "Table 2 Results Summary for Schools and Childcare Testing YTD (All Facility Types)";
                        var allTypes = rawText.Substring(rawText.IndexOf(tableStr
                            ) + tableStr.Length);

                        allFaciltyValues.AddRange(ParseFacilityValues(allTypes, new[]
                        {
                            "Childcare",
                            "Primary",
                            "Post Primary*",
                            "Special Education"
                        }));

                    }
                    else
                        Console.WriteLine(text);
                }

            }
            return new HseSchoolsSummary
            {
                FacilityValues = allFaciltyValues.ToArray(),
                Values = values
            };
        }

        

        static void Main(string[] args)
        {
            const string filePath =
                @"https://www.hse.ie/eng/services/news/newsfeatures/covid19-updates/covid-19-schools-and-childcare-facilities-mass-testing-report-week-51.pdf";
            var summary = LoadSummary(filePath);
            var ds = new DataContractSerializer(typeof(HseSchoolsSummary));
            var sw = new StringWriter();
            var xml = XmlWriter.Create(sw);
            ds.WriteObject(xml, summary);
            xml.Flush();
            sw.Flush();
            Console.WriteLine(sw.ToString());
        }
    }
}
