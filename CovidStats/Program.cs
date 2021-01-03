using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Xml;
using UglyToad.PdfPig;

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
                using WebResponse response = requestFichier.GetResponse();
                using MemoryStream ms = new MemoryStream();
                response.GetResponseStream().CopyTo(ms);
                listeByte = ms.ToArray();
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
                HseSchoolsFacilityValue GetValue(string pName)
                {
                    var pos = pText.IndexOf(pName, StringComparison.Ordinal);
                    if (pos == -1) throw new InvalidDataException("This shouldn't happen ;)");
                    var substr = pText.Substring(pos + pName.Length);
                    var splits = substr.Split(" ", StringSplitOptions.RemoveEmptyEntries);
                    return new HseSchoolsFacilityValue
                    {
                        Name = pName,
                        NoFacilities = Int32.Parse(splits[0].Replace(",", "")),
                        NoTested = Int32.Parse(splits[1].Replace(",", "")),
                        NoDetected = Int32.Parse(splits[2].Replace(",", "")),
                        DetectedPercent = Decimal.Parse(splits[3].Replace("%", "")),
                        NoNotDetected = Int32.Parse(splits[4].Replace(",", ""))
                    };
                }

                var runningTotal =  new HseSchoolsFacilityValue { Name="Calculated Total" };

                for (var i = 0; i < pNames.Length; i++)
                {
                    var value = GetValue(pNames[i]);
                    runningTotal.Add(value);
                    yield return value;
                }

                var totalsValue = GetValue("Total");
                runningTotal.DetectedPercent = Math.Round((decimal)100*runningTotal.NoDetected/runningTotal.NoTested,1);
                if (!totalsValue.Equals(runningTotal))
                    throw new InvalidDataException($"Calculated totals: {runningTotal}\nDo not match totals in source PDF: {totalsValue}");
            }

            var result = new HseSchoolsSummary();

         
            using (var pdf = PdfDocument.Open(listeByte))
            {
                foreach (var page in pdf.GetPages())
                {
                    var rawText = page.Text;
                    if (rawText.Contains("Summary of Mass Testing in Schools & Childcare"))
                    {

                        result.Values = ParseSchoolValues(rawText, new[]
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
                        var allTypes = rawText.Substring(rawText.IndexOf(tableStr) + tableStr.Length);

                        result.AllFacilityTypesResultsSummary = ParseFacilityValues(allTypes, new[]
                        {
                            "Childcare",
                            "Primary",
                            "Post Primary*",
                            "Special Education"
                        }).ToArray();

                    }
                    else if (rawText.Contains("Summary of Mass Testing in Schools Only"))
                    {
                        result.Schools.Testing = ParseFacilityValues(rawText, new[]
                        {
                            "Primary",
                            "Post Primary",
                            "Special Education"
                        }).ToArray();

                        const string schoolMassSection = "Results Summary for School Facility Testing";
                        var remainingText =
                            rawText.Substring(rawText.IndexOf(schoolMassSection) + schoolMassSection.Length);

                        result.Schools.MassTesting = ParseFacilityValues(remainingText, new[]
                        {
                            "Primary",
                            "Post Primary",
                            "Special Education"
                        }).ToArray();
                    }
                    else if (rawText.Contains("Summary of Childcare Facility Testing"))
                    {
                        rawText = rawText.Substring(
                            rawText.IndexOf("Table 7 Results Summary YTD for Childcare Facilities") +
                            "Table 7 Results Summary YTD for Childcare Facilities".Length);
                        result.Childcare.Testing = ParseFacilityValues(rawText, new[]
                        {
                            "Childcare",
                        }).ToArray();

                        const string childcareMassSection = "Table 8 Results Summary WK 51 for Childcare Facilities";
                        var remainingText =
                            rawText.Substring(rawText.IndexOf(childcareMassSection) + childcareMassSection.Length);

                        result.Childcare.MassTesting = ParseFacilityValues(remainingText, new[]
                        {
                            "Childcare facilities",
                        }).ToArray();
                    }
                    else
                        Console.WriteLine(page.Text);
                }

            }

            return result;
        }

        

        static void Main(string[] args)
        {
            const string filePath =
                @"https://www.hse.ie/eng/services/news/newsfeatures/covid19-updates/covid-19-schools-and-childcare-facilities-mass-testing-report-week-51.pdf";
            var summary = LoadSummary(filePath);
            var ds = new DataContractSerializer(typeof(HseSchoolsSummary));
            var sw = new StringWriter();
            var xml = XmlWriter.Create(sw, new XmlWriterSettings{ Indent = true });
            ds.WriteObject(xml, summary);
            xml.Flush();
            sw.Flush();
            Console.WriteLine(sw.ToString());
        }
    }
}
