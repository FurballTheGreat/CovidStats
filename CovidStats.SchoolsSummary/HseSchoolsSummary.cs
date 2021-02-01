using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.Tokens;

namespace CovidStats.SchoolsSummary
{
    [DataContract]
    public class HseSchoolsSummary
    {
        public HseSchoolsSummary()
        {
            Schools = new HseSchoolsFacilityTypeSummary();
            Childcare = new HseSchoolsFacilityTypeSummary();
        }

        [DataMember]
        public int Week { get; set; }

        [DataMember]
        public int Year { get; set; }

        [DataMember]
        public HseSchoolsSummaryValue[] Values
        {
            get;
            set;
        }
        [DataMember]

        public HseSchoolsFacilityValue[] AllFacilityTypesResultsSummary
        {
            get;
            set;
        }

        [DataMember]
        public HseSchoolsFacilityTypeSummary Schools { get; set; }

        [DataMember]
        public HseSchoolsFacilityTypeSummary Childcare { get; set; }

        [DataMember]
        public HseSchoolsSummaryGraphValue[] ChildcareFacilityByWeek { get; set; }

        [DataMember]
        public HseSchoolsSummaryGraphValue[] SpecialEducationFacilityByWeek { get; set; }

        [DataMember]
        public string SourceFileName { get; set; }


        public static HseSchoolsSummary LoadSummary(byte[] pBytes, string pSourceFileName)
        {
            var weekRegex = new Regex(@" Week (?<weeknum>\d+) ");

            IEnumerable<HseSchoolsSummaryValue> ParseSchoolValues(string pText, string[][] pNames)
            {
                Decimal ParseValue(string pValue)
                {
                    if (pValue == "<5") return 0;
                    if (Decimal.TryParse(pValue.Replace(",", "").Replace("%", ""), out var result))
                        return result;
                    throw new InvalidDataException($"Could not parse NoDetected value of {pValue}");

                }

                for (var i = 0; i < pNames.Length; i++)
                {
                    var done = false;
                    foreach (var name in pNames[i])
                    {
                        var pos = pText.IndexOf(name, StringComparison.Ordinal);
                        if (pos == -1) continue;
                        var substr = pText.Substring(pos + name.Length);
                        var splits = substr.Split(" ", StringSplitOptions.RemoveEmptyEntries);

                        yield return new HseSchoolsSummaryValue
                        {
                            Name = pNames[i][0].Replace(".",""),
                            CumulativeToDate = Decimal.Parse(splits[0].Replace(",", "").Replace("%", "")),
                            WeekValue = ParseValue(splits[1].Replace(",", ""))

                        };
                        done = true;
                    }

                    if(!done) throw new InvalidDataException("This shouldn't happen ;)");
                }
            }

            string CutToHeading(string pText, params string[] pHeadings)
            {
                foreach (var heading in pHeadings)
                {
                    if (pText.IndexOf(heading) > 0)
                    {
                        return pText.Substring(pText.IndexOf(heading) + heading.Length);
                    }
                }
                throw new InvalidDataException("Cannot find heading in " + pText);
            }

            string CutToFromHeading(string pText, string[] pFromHeadings, string[] pToHeadings)
            {
                foreach (var heading in pFromHeadings)
                {
                    if (pText.IndexOf(heading, StringComparison.InvariantCultureIgnoreCase) > 0)
                    {
                        var remaining = pText.Substring(pText.IndexOf(heading, StringComparison.InvariantCultureIgnoreCase) + heading.Length);
                        foreach (var toHeading in pToHeadings)
                        {
                            if (remaining.IndexOf(toHeading, StringComparison.InvariantCultureIgnoreCase) > 0)
                            {
                                return remaining.Substring(0, remaining.IndexOf(toHeading, StringComparison.InvariantCultureIgnoreCase));

                            }
                        }
                        throw new InvalidDataException("Cannot find to heading in " + pText);
                    }
                }
                throw new InvalidDataException("Cannot find from heading in " + pText);
            }

            Int32 ParseNoDetected(string pNoDetected)
            {
                if (pNoDetected == "<5") return 0;
                if (Int32.TryParse(pNoDetected.Replace(",", ""), out var result))
                    return result;
                throw new InvalidDataException($"Could not parse NoDetected value of {pNoDetected}");

            }

            IEnumerable<HseSchoolsFacilityValue> ParseFacilityValues(string pText, (string,string)[] pNames) =>
                ParseFacilityValuesWithVariations(pText, pNames.Select((pX)=>
                {

                    var (displayName, intName) = pX;
                    return (displayName, new string[] {intName});
                }).ToArray());

            IEnumerable<HseSchoolsFacilityValue> ParseFacilityValuesWithVariations(string pText, (string,string[])[] pNames)
            {
                HseSchoolsFacilityValue GetValue(string[] pName, string pDisplayName)
                {
                    foreach (var name in pName)
                    {
                        var pos = pText.IndexOf(name, StringComparison.InvariantCultureIgnoreCase);
                        if (pos == -1) continue;
                        var substr = pText.Substring(pos + name.Length);
                        var splits = substr.Split(" ", StringSplitOptions.RemoveEmptyEntries);
                        return new HseSchoolsFacilityValue
                        {
                            Name = pDisplayName,
                            NoFacilities = ParseNoDetected(splits[0]),
                            NoTested = ParseNoDetected(splits[1]),
                            NoDetected = ParseNoDetected(splits[2]),
                            DetectedPercent = Decimal.Parse(splits[3].Replace("%", "")),
                            NoNotDetected = ParseNoDetected(splits[4])
                        };
                    }

                    throw new InvalidDataException("This shouldn't happen ;)");
                }

                var runningTotal = new HseSchoolsFacilityValue { Name = "Calculated Total" };

                for (var i = 0; i < pNames.Length; i++)
                {
                    var (displayName, intNames) = pNames[i];
                    var value = GetValue(intNames, displayName);
                    runningTotal.Add(value);
                    yield return value;
                }

                var totalsValue = GetValue(new[] { "Total" }, "Total");
                runningTotal.DetectedPercent = Math.Round((decimal)100 * runningTotal.NoDetected / runningTotal.NoTested, 1);
                if (!totalsValue.Equals(runningTotal))
                    Console.WriteLine($"WARNING - Calculated totals: {runningTotal}\nDo not match totals in source PDF: {totalsValue}");
            }

            IEnumerable<Int32> ExtractNumbers(string pText)
            {
                bool inNumeric = false;
                var sb = new StringBuilder();
                foreach (var c in pText)
                {
                    if (char.IsDigit(c))
                    {
                        sb.Append(c);
                        inNumeric = true;
                    }
                    else
                    {
                        if (inNumeric)
                        {
                            yield return Int32.Parse(sb.ToString());
                            inNumeric = false;
                            sb.Clear();
                        }
                    }
                }
                if (inNumeric)
                    yield return Int32.Parse(sb.ToString());
            }

            var result = new HseSchoolsSummary();
            result.SourceFileName = pSourceFileName;
            IEnumerable<HseSchoolsSummaryGraphValue> ParseGraphValues(
                string pText, 
                string[] pValuesFrom, 
                string[] pValuesTo, 
                string[] pWeeksFrom, 
                string[] pWeeksTo)
            {
                
                var values = CutToFromHeading(pText,pValuesFrom,pValuesTo).
                    Replace("\r\n", " ").Split(" ", StringSplitOptions.RemoveEmptyEntries).Where(pX=>!pX.Contains("%")).ToArray();
                var weeksText = CutToFromHeading(pText,
                    pWeeksFrom,
                    pWeeksTo);
                var weeks = ExtractNumbers(weeksText).ToArray().ToList();
                var hit53 = false;
                for(var i = 0; i < weeks.Count; i++)
                    if (hit53)
                        weeks[i] += 53;
                    else if (i == 53) hit53 = true;

                while(weeks.Count<values.Length)
                    weeks.Insert(0, weeks.Min()-1);
                
                if(weeks.Count!=values.Length) 
                    throw new InvalidDataException("Lengths of week and value lists are not the same");
                for(var i = 0; i < weeks.Count; i++)
                    yield return new HseSchoolsSummaryGraphValue { Value = Int32.Parse(values[i]), Week = weeks[i]};
            }

            var regex = new Regex(@"Data up to \d+:\d+\w+ \d+\w+ \w+ (?<Year>\d\d\d\d)");
            using (var pdf = PdfDocument.Open(pBytes))
            {
                foreach (var page in pdf.GetPages())
                {
                    var rawText = page.Text;
                    var matches = regex.Matches(rawText);
                    if (matches.Count > 0)
                    {
                        var year = matches.First().Groups["Year"].Captures.First().Value;
                        result.Year = Int32.Parse(year);
                    }
                    if (result.Week == 0)
                    {
                        foreach (Match match in weekRegex.Matches(rawText))
                        {
                            result.Week = Int32.Parse(match.Groups["weeknum"].Value);
                            break;
                        }
                    }
                    if (rawText.Contains("Summary of Mass Testing in Schools & Childcare"))
                    {

                        result.Values = ParseSchoolValues(rawText, new string[][]
                        {
                           new [] { "Number Tested" },
                           new [] {"Number of Detected Cases1","Number of Detected Cases" },
                           new [] { "Detection Rate %" },
                           new [] {"Detected 0-17" },
                           new [] {"Detected 18+" },
                           new [] {"No. of Facilities Tested" },
                           new [] {"Facilities with Detected Case" }
                        }).ToArray();
                        var allTypes = CutToHeading(rawText, new[]
                        {
                            "Table 2 Results Summary for Schools and Childcare Testing YTD (All Facility Types)",
                            "Table 2 Results Summary for Schools Testing YTD (All Facility Types)"
                        });
                        result.AllFacilityTypesResultsSummary = ParseFacilityValuesWithVariations(allTypes, new[]
                        {
                            ("Childcare", new []{ "Childcare" } ),
                            ("Primary",new []{ "Primary" } ) ,
                            ("PostPrimary", new []{ "Post Primary*", "Post Primary" } ),
                            ("SpecialEducation", new []{ "Special Education" })
                        }).ToArray();

                    }
                    else if (rawText.Contains("Summary of Mass Testing in Schools Only"))
                    {
                        result.Schools.Testing = ParseFacilityValuesWithVariations(rawText, new[]
                        {
                           ("Primary", new[]{ "Primary" } ),
                           ("PostPrimary", new[]{ "Post Primary*", "Post Primary" }), 
                           ("SpecialEducation", new[]{ "Special Education" } )
                        }).ToArray();

                        var remainingText = CutToHeading(rawText, "Table 4 Results Summary for Schools Testing", "Table 4 Results Summary for Schools Testing WK");

                        result.Schools.MassTesting = ParseFacilityValuesWithVariations(remainingText, new[]
                        {
                             ("Primary", new[]{ "Primary*", "Primary" }),
                             ("PostPrimary", new[]{ "Post Primary*", "Post Primary" }),
                             ("SpecialEducation",new[]{ "Special Education**", "Special Education" })
                        }).ToArray();
                    }
                    else if (rawText.Contains("Summary of Childcare Facility Testing") || rawText.Contains("Summary of Childcare Facilities Testing"))
                    {
                        var table7Text = CutToFromHeading(rawText, new[] { "Table 7 Results Summary YTD for Childcare Facilities", "Table 8 Results Summary YTD for Childcare Facilities" }, new[] { "Results Summary for " });

                        result.Childcare.Testing = ParseFacilityValuesWithVariations(table7Text, new[]
                        {
                            ("", new []
                            {
                                "Childcare facilities",
                                "Childcare"
                            }),
                        }).ToArray();

                        var table8Text = CutToFromHeading(CutToHeading(rawText, "Table 8 Results Summary", "Table 8 Results Summary"),
                            new[] { "Childcare Facilities" }, new[] { "Age Analysis for " });

                        result.Childcare.MassTesting = ParseFacilityValuesWithVariations(table8Text, new[]
                        {
                            ("", new []
                            {
                                "No. Not Detected Childcare facilities",
                                "Childcare facilities",
                                "Childcare"
                            }),
                        }).ToArray();
                    } else if (rawText.Contains(
                        "4. Supporting Graphs"))
                    {
                        //result.ChildcareFacilityByWeek = ParseGraphValues(
                        //    ContentOrderTextExtractor.GetText(page),
                        //    new[]
                        //    {
                        //        "Total Tested % weekly increase/ decrease",
                        //        "Table 11 Weekly Analysis of Childcare Facility Testing by Week",
                        //    },
                        //    new[] { "-600%", "-150%" },
                        //    new[] {"WK","4000"},
                        //    new[] {"Weekly Comparison of Tests Completed"}
                        //).ToArray();


                        //result.SpecialEducationFacilityByWeek = ParseGraphValues(
                        //    ContentOrderTextExtractor.GetText(page),
                        //    new[]
                        //    {
                        //        "Figure 11 Weekly Analysis of Childcare Facility Testing by Week",
                        //        "Table 11 Weekly Analysis of Childcare Facility Testing by Week",
                        //    },
                        //    new[] { "-150%", "-100.0%" },
                           
                        //    new[] { "900" , "1200" },
                        //    new[] { "Weekly Comparison of Tests Completed in Childcare Facilities" }
                        //).ToArray();
                        //var altText = ContentOrderTextExtractor.GetText(page);
                       

                    }
                }

            }

            return result;
        }

    }
}