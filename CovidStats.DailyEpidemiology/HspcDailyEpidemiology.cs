using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic.FileIO;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.Export.PAGE;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace CovidStats.DailyEpidemiology
{
    public class HspcDailyEpidemiology
    {
        private static readonly string[] Counties = new[]
        {
            "Carlow",
            "Cavan",
            "Clare",
            "Cork",
            "Donegal",
            "Dublin",
            "Galway",
            "Kerry",
            "Kildare",
            "Kilkenny",
            "Laois",
            "Leitrim",
            "Limerick",
            "Longford",
            "Louth",
            "Mayo",
            "Meath",
            "Monaghan",
            "Offaly",
            "Roscommon",
            "Sligo",
            "Tipperary",
            "Waterford",
            "Westmeath",
            "Wexford",
            "Wicklow"
        };
        public string SourceFileName
        {
            get;
            set;
        }
        public DateTime PreparedDate
        {
            get;
            set;
        }

        public DateTime FromDate
        {
            get;
            set;
        }

        public DateTime ToDate
        {
            get;
            set;
        }

        public HspcAbsoluteAndPercent TotalConfirmedCases { get; set; } = new HspcAbsoluteAndPercent();
        public HspcDailyEpidemiologySexCharacteristics SexCharacteristics { get; set; } = new HspcDailyEpidemiologySexCharacteristics();
        public HspcDailyEpidemiologyAgeCharacteristics AgeCharacteristics { get; set; } = new HspcDailyEpidemiologyAgeCharacteristics();
        public HspcYesNoUnknown UnderlyingConditionsCharacteristics { get; set; } = new HspcYesNoUnknown();
        public HspcYesNoUnknown SymptomStatusAtTimeOfTestCharacteristics { get; set; } = new HspcYesNoUnknown();
        public HspcIncidence[] CHORates { get; set; }
        public HspcIncidence[] CountyRates { get; set; }
        public HspcIncidence[] CCARates { get; set; } 
        public HspcHospitalised[] Hospitalised { get; set; }

        public static HspcDailyEpidemiology Load(byte[] pData, string pSourceFileName)
        {
            HspcAbsoluteAndPercent ParseAbsoluteAndPercent(string[] pValues) => new HspcAbsoluteAndPercent
            {
                Absolute = Int32.Parse(pValues[0].Replace(",", "")),
                Percent = Decimal.Parse(pValues[1])
            };

            var result = new HspcDailyEpidemiology {SourceFileName = pSourceFileName};

            var reportPreparedByRegex = new Regex(@"Report prepared by HPSC on (?<preparedDate>\d+/\d+/\d+)");
            var characteristicsRegex = new Regex(@"Table \d: Characteristics of confirmed COVID-19 cases notified in Ireland from (?<fromDate>\d+/\d+/\d+) up to midnight on (?<toDate>\d+/\d+/\d+)(?<content>.*)");
            var choIncidenceRegex =
                new Regex(@"Number of confirmed COVID-19 cases by community healthcare organisation");
            var ccaIncidenceRegex = 
                new Regex(@"Number and incidence of confirmed COVID-19 cases by Dublin Local Health Offices");
            var hospitalRegex = new Regex(@"by age group, hospitalisation and ICU admission");
            using (var pdf = PdfDocument.Open(pData))
            {
                var gotPreparedBy = false;
                var gotCharacteristics = false;
                var gotChoIncidence = false;
                var gotCcaIncidence = false;
                var gotHospital = false;
                foreach (var page in pdf.GetPages())
                {
                    
                    var rawText = ContentOrderTextExtractor.GetText(page);
                    if (!gotPreparedBy)
                    {
                        var matches = reportPreparedByRegex.Matches(rawText);
                        if (matches.Count > 0)
                        {
                            result.PreparedDate = DateTime.Parse(matches.First().Groups["preparedDate"].Value);

                            gotPreparedBy = true;
                        }
                    }
                    HspcIncidence ParseIncidence(string[] pValues) =>
                        new HspcIncidence
                        {
                            Name = pValues[0].Replace(" ",""),
                            ConfirmedCases = Int32.Parse(pValues[1].Replace(",", "")),
                            IncidencePer100k = Decimal.Parse(pValues[2])
                        };

                    if (!gotChoIncidence)
                    {

                        if (choIncidenceRegex.IsMatch(rawText))
                        {
                            var lines = rawText.Split("\r\n");
                            

                            var choIncidence = new List<HspcIncidence>();
                            var countyIncidence = new List<HspcIncidence>();

                            var choRegex = new Regex(@"^CHO\d");

                     

                            foreach (var line in lines)
                            {

                                var splits = line.Split(" ", StringSplitOptions.RemoveEmptyEntries);
                               
                                if (Counties.Contains(splits[0]))
                                    countyIncidence.Add(ParseIncidence(splits));
                                else if (choRegex.IsMatch(splits[0])) 
                                    choIncidence.Add(ParseIncidence(splits));
                            }

                            result.CHORates = choIncidence.ToArray();
                            result.CountyRates = countyIncidence.ToArray();

                            gotChoIncidence = true;
                        }
                    }


                    if (!gotCcaIncidence)
                    {

                        if (ccaIncidenceRegex.IsMatch(rawText))
                        {
                            var lines = rawText.Split("\r\n");


                            var ccaIncidence = new List<HspcIncidence>();
                            var ccaRegex = new Regex(@"^CCA\d");

                            foreach (var line in lines)
                            {
                                var rawSplits = line.Split(" ", StringSplitOptions.RemoveEmptyEntries);
                                var tempSplits = new List<string>();
                                var hitNum = false;
                                var sb = new StringBuilder();
                                foreach (var split in rawSplits)
                                    if (split.StartsWith("CCA"))
                                        continue;
                                    else if (!hitNum)
                                    {
                                        if (char.IsDigit(split[0]))
                                        {
                                            hitNum = true;
                                            tempSplits.Add(sb.ToString());
                                            sb = new StringBuilder();
                                            tempSplits.Add(split);
                                        }
                                        else
                                        {
                                            if (sb.Length > 0) sb.Append(" ");
                                            sb.Append(split);
                                        }
                                    }
                                    else
                                        tempSplits.Add(split);
                                if (sb.Length > 0) tempSplits.Add(sb.ToString());


                                var splits = tempSplits.ToArray();
                                if (ccaRegex.IsMatch(rawSplits[0]))
                                    ccaIncidence.Add(ParseIncidence(splits));
                            }

                            result.CCARates = ccaIncidence.ToArray();
                         

                            gotCcaIncidence = true;
                        }
                    }

                    if (!gotHospital)
                    {

                        if (hospitalRegex.IsMatch(rawText))
                        {
                            var lines = rawText.Split("\r\n").SkipWhile(pX=>!pX.StartsWith("0-4 yrs")).TakeWhile(pX=>!pX.StartsWith("Total") && !pX.Contains("hospitalised"));

                            var hospitalised = new List<HspcHospitalised>();
                            foreach (var line in lines)
                            {
                                try
                                {
                                    var splits = line.Split(" ", StringSplitOptions.RemoveEmptyEntries);
                                    if (splits.Length == 7)
                                    {
                                        hospitalised.Add(new HspcHospitalised
                                        {
                                            Name = splits[0].Replace("-", "To").Replace("+", "Plus"),
                                            NumberOfCases = Int32.Parse(splits[2]),
                                            CasesHospitalised = Int32.Parse(splits[3]),
                                            CasesHospitalisedPercent = Decimal.Parse(splits[4]),
                                            CasesAdmittedToIcu = Int32.Parse(splits[5]),
                                            CasesAdmittedToIcuPercent = Decimal.Parse(splits[6])
                                        });
                                        ;
                                    }
                                    else if (splits.Length == 5)
                                    {
                                        hospitalised.Add(new HspcHospitalised
                                        {
                                            Name = splits[0].Replace("-", "To").Replace("+", "Plus"),
                                            NumberOfCases = Int32.Parse(splits[2]),
                                            CasesAdmittedToIcu = Int32.Parse(splits[3]),
                                            CasesAdmittedToIcuPercent = Decimal.Parse(splits[4])
                                        });
                                        ;
                                    }
                                    else
                                    {
                                        hospitalised.Add(new HspcHospitalised
                                        {
                                            Name = splits[0].Replace("-", "To").Replace("+","Plus"),
                                            NumberOfCases = Int32.Parse(splits[1]),
                                            CasesAdmittedToIcu = Int32.Parse(splits[2]),
                                            CasesAdmittedToIcuPercent = Decimal.Parse(splits[3])
                                        });
                                        ;
                                    }
                                }
                                catch (Exception e)
                                {

                                }

                            }

                            result.Hospitalised = hospitalised.ToArray();

                            gotHospital = true;
                        }
                    }

                    if (!gotCharacteristics)
                    {
                        var matches = characteristicsRegex.Matches(rawText);
                        if (matches.Count > 0)
                        {
                            result.FromDate = DateTime.Parse(matches.First().Groups["fromDate"].Value);
                            result.ToDate = DateTime.Parse(matches.First().Groups["toDate"].Value);
                            var lines = rawText.Split("\r\n")
                                .SkipWhile(pX => !pX.StartsWith("Total number of confirmed cases")).ToArray();
                            var splits = new string[0];


                            var entries = !lines[1].StartsWith("Sex")
                                ? new List<KeyValuePair<string, Action>>
                                {
                                    new KeyValuePair<string, Action>("Total number of confirmed cases",
                                        () => result.TotalConfirmedCases = ParseAbsoluteAndPercent(splits)),
                                    new KeyValuePair<string, Action>("0-4 yrs",
                                        () => result.AgeCharacteristics.Age0To4 = ParseAbsoluteAndPercent(splits)),
                                    new KeyValuePair<string, Action>("5-12 yrs",
                                        () => result.AgeCharacteristics.Age5To12 = ParseAbsoluteAndPercent(splits)),
                                    new KeyValuePair<string, Action>("13-18 yrs",
                                        () => result.AgeCharacteristics.Age13To18 = ParseAbsoluteAndPercent(splits)),
                                    new KeyValuePair<string, Action>("19-24 yrs",
                                        () => result.AgeCharacteristics.Age19To24 = ParseAbsoluteAndPercent(splits)),
                                    new KeyValuePair<string, Action>("25-34 yrs",
                                        () => result.AgeCharacteristics.Age25To34 = ParseAbsoluteAndPercent(splits)),
                                    new KeyValuePair<string, Action>("35-44 yrs",
                                        () => result.AgeCharacteristics.Age35To44 = ParseAbsoluteAndPercent(splits)),
                                    new KeyValuePair<string, Action>("45-54 yrs",
                                        () => result.AgeCharacteristics.Age45To54 = ParseAbsoluteAndPercent(splits)),
                                    new KeyValuePair<string, Action>("55-64 yrs",
                                        () => result.AgeCharacteristics.Age55To64 = ParseAbsoluteAndPercent(splits)),
                                    new KeyValuePair<string, Action>("65-74 yrs",
                                        () => result.AgeCharacteristics.Age65To74 = ParseAbsoluteAndPercent(splits)),
                                    new KeyValuePair<string, Action>("75-84 yrs",
                                        () => result.AgeCharacteristics.Age75To84 = ParseAbsoluteAndPercent(splits)),
                                    new KeyValuePair<string, Action>("85+ yrs",
                                        () => result.AgeCharacteristics.Age85Plus = ParseAbsoluteAndPercent(splits)),
                                    new KeyValuePair<string, Action>("Unknown",
                                        () => result.AgeCharacteristics.AgeUnknown = ParseAbsoluteAndPercent(splits)),

                                    new KeyValuePair<string, Action>("Sex Male:Female ratio",
                                        () => result.SexCharacteristics.MaleFemaleRatio = decimal.Parse(splits[0])),
                                    new KeyValuePair<string, Action>("Male",
                                        () => result.SexCharacteristics.Male = ParseAbsoluteAndPercent(splits)),
                                    new KeyValuePair<string, Action>("Female",
                                        () => result.SexCharacteristics.Female = ParseAbsoluteAndPercent(splits)),
                                    new KeyValuePair<string, Action>("Unknown",
                                        () => result.SexCharacteristics.Unknown = ParseAbsoluteAndPercent(splits)),

                                    new KeyValuePair<string, Action>("Age Median age (years)",
                                        () => result.AgeCharacteristics.MedianAge = Int32.Parse(splits[0])),
                                    new KeyValuePair<string, Action>("Mean age (years)",
                                        () => result.AgeCharacteristics.MeanAge = Int32.Parse(splits[0])),
                                    new KeyValuePair<string, Action>("Age range (years)", () => { }),

                                    new KeyValuePair<string, Action>("Yes",
                                        () => result.UnderlyingConditionsCharacteristics.Yes =
                                            ParseAbsoluteAndPercent(splits)),
                                    new KeyValuePair<string, Action>("No",
                                        () => result.UnderlyingConditionsCharacteristics.No =
                                            ParseAbsoluteAndPercent(splits)),
                                    new KeyValuePair<string, Action>("Unknown",
                                        () => result.UnderlyingConditionsCharacteristics.Unknown =
                                            ParseAbsoluteAndPercent(splits)),

                                    new KeyValuePair<string, Action>("Underlying clinical conditions", () => { }),

                                    new KeyValuePair<string, Action>("Yes",
                                        () => result.SymptomStatusAtTimeOfTestCharacteristics.Yes =
                                            ParseAbsoluteAndPercent(splits)),
                                    new KeyValuePair<string, Action>("No",
                                        () => result.SymptomStatusAtTimeOfTestCharacteristics.No =
                                            ParseAbsoluteAndPercent(splits)),
                                    new KeyValuePair<string, Action>("Unknown",
                                        () => result.SymptomStatusAtTimeOfTestCharacteristics.Unknown =
                                            ParseAbsoluteAndPercent(splits)),

                                }
                                : new List<KeyValuePair<string, Action>>
                                {
                                    new KeyValuePair<string, Action>("Total number of confirmed cases",
                                        () => result.TotalConfirmedCases = ParseAbsoluteAndPercent(splits)),
                                    new KeyValuePair<string, Action>("Sex Male:Female ratio",
                                        () => result.SexCharacteristics.MaleFemaleRatio = decimal.Parse(splits[0])),
                                    new KeyValuePair<string, Action>("Male",
                                        () => result.SexCharacteristics.Male = ParseAbsoluteAndPercent(splits)),
                                    new KeyValuePair<string, Action>("Female",
                                        () => result.SexCharacteristics.Female = ParseAbsoluteAndPercent(splits)),
                                    new KeyValuePair<string, Action>("Unknown",
                                        () => result.SexCharacteristics.Unknown = ParseAbsoluteAndPercent(splits)),

                                    new KeyValuePair<string, Action>("Age Median age (years)",
                                        () => result.AgeCharacteristics.MedianAge = Int32.Parse(splits[0])),
                                    new KeyValuePair<string, Action>("Mean age (years)",
                                        () => result.AgeCharacteristics.MeanAge = Int32.Parse(splits[0])),
                                    new KeyValuePair<string, Action>("Age range (years)", () => { }),
                                    new KeyValuePair<string, Action>(
                                        "Report prepared by Health Protection Surveillance Centre", () => { }),
                                    new KeyValuePair<string, Action>("0-4 yrs",
                                        () => result.AgeCharacteristics.Age0To4 = ParseAbsoluteAndPercent(splits)),
                                    new KeyValuePair<string, Action>("5-12 yrs",
                                        () => result.AgeCharacteristics.Age5To12 = ParseAbsoluteAndPercent(splits)),
                                    new KeyValuePair<string, Action>("13-18 yrs",
                                        () => result.AgeCharacteristics.Age13To18 = ParseAbsoluteAndPercent(splits)),
                                    new KeyValuePair<string, Action>("19-24 yrs",
                                        () => result.AgeCharacteristics.Age19To24 = ParseAbsoluteAndPercent(splits)),
                                    new KeyValuePair<string, Action>("25-34 yrs",
                                        () => result.AgeCharacteristics.Age25To34 = ParseAbsoluteAndPercent(splits)),
                                    new KeyValuePair<string, Action>("35-44 yrs",
                                        () => result.AgeCharacteristics.Age35To44 = ParseAbsoluteAndPercent(splits)),
                                    new KeyValuePair<string, Action>("45-54 yrs",
                                        () => result.AgeCharacteristics.Age45To54 = ParseAbsoluteAndPercent(splits)),
                                    new KeyValuePair<string, Action>("55-64 yrs",
                                        () => result.AgeCharacteristics.Age55To64 = ParseAbsoluteAndPercent(splits)),
                                    new KeyValuePair<string, Action>("65-74 yrs",
                                        () => result.AgeCharacteristics.Age65To74 = ParseAbsoluteAndPercent(splits)),
                                    new KeyValuePair<string, Action>("75-84 yrs",
                                        () => result.AgeCharacteristics.Age75To84 = ParseAbsoluteAndPercent(splits)),
                                    new KeyValuePair<string, Action>("85+ yrs",
                                        () => result.AgeCharacteristics.Age85Plus = ParseAbsoluteAndPercent(splits)),
                                    new KeyValuePair<string, Action>("Unknown",
                                        () => result.AgeCharacteristics.AgeUnknown = ParseAbsoluteAndPercent(splits)),




                                    new KeyValuePair<string, Action>("Yes",
                                        () => result.UnderlyingConditionsCharacteristics.Yes =
                                            ParseAbsoluteAndPercent(splits)),
                                    new KeyValuePair<string, Action>("No",
                                        () => result.UnderlyingConditionsCharacteristics.No =
                                            ParseAbsoluteAndPercent(splits)),
                                    new KeyValuePair<string, Action>("Unknown",
                                        () => result.UnderlyingConditionsCharacteristics.Unknown =
                                            ParseAbsoluteAndPercent(splits)),

                                    new KeyValuePair<string, Action>("Underlying clinical conditions", () => { }),

                                    new KeyValuePair<string, Action>("Yes",
                                        () => result.SymptomStatusAtTimeOfTestCharacteristics.Yes =
                                            ParseAbsoluteAndPercent(splits)),
                                    new KeyValuePair<string, Action>("No",
                                        () => result.SymptomStatusAtTimeOfTestCharacteristics.No =
                                            ParseAbsoluteAndPercent(splits)),
                                    new KeyValuePair<string, Action>("Unknown",
                                        () => result.SymptomStatusAtTimeOfTestCharacteristics.Unknown =
                                            ParseAbsoluteAndPercent(splits)),

                                };
                            var lineIndex = 0;
                            for (var i = 0; i < entries.Count; i++)
                            {
                                if (lineIndex >= lines.Length)
                                    throw new InvalidDataException("Cannot decode characteristics, failed at " +
                                                                   entries[i].Key);
                                if (new Regex(@"^\d\d ").IsMatch(lines[lineIndex]))
                                    lines[lineIndex] = lines[lineIndex].Substring(3);
                                if (!lines[lineIndex].StartsWith(entries[i].Key))
                                {
                                    if (entries[i].Key == "Unknown" || i >= 20)
                                        continue;
                                    throw new InvalidDataException(
                                        $"Expected line starting with '{entries[i].Key}' instead got: {lines[lineIndex]}");
                                }

                                splits = lines[lineIndex].Substring(entries[i].Key.Length)
                                    .Split(" ", StringSplitOptions.RemoveEmptyEntries);
                                entries[i].Value();
                                lineIndex++;
                            }

                            gotCharacteristics = true;
                        }



                    }
                }
            }

            return result;
        }

        public override string ToString() => $"{FromDate.ToShortDateString()}-{ToDate.ToShortDateString()} ({PreparedDate.ToShortDateString()})";
    }
}
