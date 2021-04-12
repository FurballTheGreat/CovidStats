using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using Org.BouncyCastle.Security;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using PdfDocument = UglyToad.PdfPig.PdfDocument;

namespace CovidStats.DailyEpidemiology
{

    public class HspcDailyEpidemiology
    {
        private static readonly Dictionary<string, decimal> Counties = new Dictionary<string, decimal>
        {
            {"Carlow",56932 } ,
            {"Cavan",76176  },
            {"Clare",118817     },
            {"Cork",542868 },
            {"Donegal",159192   },
            {"Dublin",1345402 },
            {"Galway", 258058},
            {"Kerry",147707 },
            {"Kildare",222504   },
            {"Kilkenny", 99232 },
            {"Laois", 84697 },
            {"Leitrim", 32044   },
            {"Limerick", 194899 },
            {"Longford", 40873  },
            {"Louth", 128884 },
            {"Mayo", 130507},
            {"Meath", 195044 },
            {"Monaghan", 61386  },
            {"Offaly", 77961 },
            {"Roscommon", 64544 },
            {"Sligo", 65535 },
            {"Tipperary", 159553   },
            {"Waterford",116176},
            {"Westmeath", 88770 },
            {"Wexford", 149722  },
            {"Wicklow", 142425  }
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

        public HspcAbsoluteAndPercent ImportedCases { get; set; } = new HspcAbsoluteAndPercent();
        public HspcAbsoluteAndPercent TravelRelatedCases { get; set; } = new HspcAbsoluteAndPercent();
        public HspcAbsoluteAndPercent TotalConfirmedCases { get; set; } = new HspcAbsoluteAndPercent();
        public HspcDailyEpidemiologySexCharacteristics SexCharacteristics { get; set; } = new HspcDailyEpidemiologySexCharacteristics();
        public HspcDailyEpidemiologyAgeCharacteristics AgeCharacteristics { get; set; } = new HspcDailyEpidemiologyAgeCharacteristics();
        public HspcYesNoUnknown UnderlyingConditionsCharacteristics { get; set; } = new HspcYesNoUnknown();
        public HspcYesNoUnknown SymptomStatusAtTimeOfTestCharacteristics { get; set; } = new HspcYesNoUnknown();
        public HspcIncidence[] CHORates { get; set; }
        public HspcIncidence[] CountyRates { get; set; }
        public HspcIncidence[] CCARates { get; set; } 
        public HspcHospitalised[] Hospitalised { get; set; }


        private static void ApplyBest(string[] pLines, List<KeyValuePair<string, Action<string[]>>[]> pOptions)
        {
            var maxLineIndex = 0;
            KeyValuePair<string, Action<string[]>>[] actions = null;
            var lineIndex = 0;
            var gotFullMatch = false;

            for (var i = 0; i < pLines.Length; i++)
            {
                if (new Regex(@"^\d\d ").IsMatch(pLines[i]))
                    pLines[i] = pLines[i].Substring(3);
            }
            foreach (var entries in pOptions)
            {
                int i = 0;
                for (i = 0; i < entries.Length; i++)
                {
                    if (!pLines[lineIndex].StartsWith(entries[i].Key))
                    {

                        if (entries[i].Key.StartsWith("Unknown"))
                        {
                           
                            continue;
                        }

                        if (pLines[lineIndex].StartsWith("Report prepared by Health Protection Surveillance Centre"))
                            lineIndex++;

                        break;
                    }
                    lineIndex++;
                    if (i == entries.Length - 1 || lineIndex == pLines.Length -1 )
                    {
                        maxLineIndex = i;
                        gotFullMatch = true;
                        actions = entries;
                        break;
                    }
                }

                if (gotFullMatch) break;

                if (lineIndex > maxLineIndex)
                {
                    maxLineIndex = i;
                    actions = entries;
                }

                lineIndex = 0;
            }

            if (maxLineIndex <= pLines.Length && maxLineIndex < 20 && !gotFullMatch)
            {
                throw new InvalidDataException("Cannot decode characteristics ");
            }

            lineIndex = 0;
            for (var i = 0; i < actions.Length; i++)
            {

                if (new Regex(@"^\d\d ").IsMatch(pLines[lineIndex]))
                    pLines[lineIndex] = pLines[lineIndex].Substring(3);

                if (!pLines[lineIndex].StartsWith(actions[i].Key))
                {
                    if (actions[i].Key.StartsWith("Unknown"))
                    {
                  
                        continue;
                    }


                    if (pLines[lineIndex].StartsWith("Report prepared by Health Protection Surveillance Centre"))
                    {
                        lineIndex++;

                    }

                    if (lineIndex >= pLines.Length) break;
                    if (!pLines[lineIndex].StartsWith(actions[i].Key))
                        break;
                }


                var splits = pLines[lineIndex].Substring(actions[i].Key.Length)
                    .Split(" ", StringSplitOptions.RemoveEmptyEntries);

                actions[i].Value(splits);

                lineIndex++;
                if (lineIndex >= pLines.Length) break;
            }
        }

        public static HspcDailyEpidemiology Load(byte[] pData, string pSourceFileName, Func<DateTime,HspcDailyEpidemiology> pGetOverrides)
        {
            var newLine = (new StringBuilder().AppendLine()).ToString();
            
        //    try
            {
                Console.WriteLine($"Parsing {pSourceFileName}");

                HspcAbsoluteAndPercent ParseAbsoluteAndPercent(string[] pValues) => new HspcAbsoluteAndPercent
                {
                    Absolute = Int32.Parse(pValues[0].Replace(",", "")),
                    Percent = pValues.Length == 1 ? 0 : Decimal.Parse(pValues[1].TrimEnd('%'))
                };

                var result = new HspcDailyEpidemiology {SourceFileName = pSourceFileName};

                var reportPreparedByRegex = new Regex(@"Report prepared by HPSC on (?<preparedDate>\d+/\d+/\d+)");
                var characteristicsRegex =
                    new Regex(
                        @"Table \d: Characteristics of confirmed COVID-19 cases notified in Ireland from (?<fromDate>\d+/\d+/\d+) up to midnight on (?<toDate>\d+/\d+/\d+)(?<content>.*)");
                var characteristicsRegex2 =
                    new Regex(
                        @"Table \d: Age and sex of confirmed COVID-19 cases notified in Ireland from (?<fromDate>\d+/\d+/\d+) up to midnight on (?<toDate>\d+/\d+/\d+)(?<content>.*)");
                var choIncidenceRegex =
                    new Regex(@"Number of confirmed COVID-19 cases by community healthcare organisation");
                var choIncidenceRegex2 =
                    new Regex(@"Number of confirmed COVID-19 cases by CHO notified in Ireland from");
                var ccaIncidenceRegex =
                    new Regex(@"Number and incidence of confirmed COVID-19 cases by Dublin Local Health Offices");
                var hospitalRegex =  new Regex(@"by age group, hospitalisation and ICU admission");
                var hospitalRegex4 = new Regex(@"by age group, hospitalisatioin and ICU admission");
                var hospitalRegex5 = new Regex(@"by age group, hospitalisation, and ICU admission");
                var hospitalRegex8 = new Regex(@"by age group, hospitalistion and ICU admission");
                var hospitalRegex6 = new Regex(@"by age group, hospitalisation,");
                var hospitalRegex7 = new Regex(@"by age group and hospitalisation");
                var hospitalRegex2 = new Regex(@"by age group and hospital and ICU admission");
                var hospitalRegex3 = new Regex(@"by age group and hospital, ICU. and vital status");
                var hospitalNewRegex = new Regex(@"by age-group, hospitalisation, ICU admission. and vital status");
                Console.WriteLine("Opening PDF");
                using (var pdf = PdfDocument.Open(pData))
                {
                    var gotPreparedBy = false;
                    var gotSummaryCharacteristics = false;
                    var gotTravelRelated = false;
                    var gotCharacteristics = false;
                    var gotChoIncidence = false;
                    var gotCcaIncidence = false;
                    var gotHospital = false;
                   
                    Console.WriteLine("Looping pages");
                    foreach (var page in pdf.GetPages())
                    {
                        Console.WriteLine("Got page");
                        var rawText = ContentOrderTextExtractor.GetText(page);
                        Debug.WriteLine(rawText);
                        if (!gotSummaryCharacteristics)
                        {
                            if (rawText.Contains(
                                "Summary characteristics of confirmed COVID-19 cases notified in Ireland"))
                            {
                                var lines = rawText.Split(newLine);
                                var imported = lines.FirstOrDefault(pX => pX.StartsWith("Number of imported cases"));
                                if (imported != null)
                                {
                                    imported = imported.Substring("Number of imported cases".Length).Trim();
                                    result.ImportedCases = ParseAbsoluteAndPercent(imported.Split(" "));
                                }
                            

                                gotSummaryCharacteristics = true;
                            }
                        }

                        if (!gotTravelRelated)
                        {
                            if (rawText.Contains(
                                "Travel related") && !rawText.Contains("Technical Notes"))
                            {
                                var lines = rawText.Split(newLine);
                                var imported = lines.FirstOrDefault(pX => pX.StartsWith("Travel related"));
                                if (imported != null)
                                {
                                    imported = imported.Substring("Travel related".Length).Trim().Trim('*', '^').Trim();
                                    result.TravelRelatedCases = ParseAbsoluteAndPercent(imported.Split(" "));
                                }


                                gotTravelRelated = true;
                            }
                        }

                        if (!gotPreparedBy)
                        {
                            var matches = reportPreparedByRegex.Matches(rawText);
                            
                            if (matches.Count > 0)
                            {
                                Console.WriteLine("Prepared By");
                                
                                var preparedDate = DateTime.Parse(matches.First().Groups["preparedDate"].Value);
                                var defaults = pGetOverrides(preparedDate);
                                if (defaults != null)
                                    result = defaults;
                                result.SourceFileName = pSourceFileName;
                                result.PreparedDate = preparedDate;
                                gotPreparedBy = true;
                            }
                        }

                        HspcIncidence ParseIncidence(string[] pValues) =>
                            new HspcIncidence
                            {
                                Name = pValues[0].Replace(" ", ""),
                                ConfirmedCases = pValues[1] == "<5" ? 0 :  Int32.Parse(pValues[1].Replace(",", "")),
                                IncidencePer100k = Decimal.Parse(pValues[2])
                            };

                        HspcIncidence ParseCountyIncidence(string[] pValues)
                        {
                            var confirmedCases = pValues[1] == "<5" ? 0 : Int32.Parse(pValues[1].Replace(",", ""));
                            var name = pValues[0].Replace(" ", "");  
                            return new HspcIncidence
                            {
                                Name = name,
                                ConfirmedCases = confirmedCases,
                                IncidencePer100k = (((decimal)confirmedCases) / Counties[name])*100000
                            };
                        }

                        if (rawText.Contains(
                            "Table 4: Number of confirmed COVID-19 cases by county notified in Ireland from"))
                        {
                            var lines = rawText.Split(newLine);//.SkipWhile(pX=>!choIncidenceRegex.IsMatch(pX));


                            var countyIncidence = new List<HspcIncidence>();

                            var choRegex = new Regex(@"^CHO\d");



                            foreach (var line in lines)
                            {

                                var splits = line.Split(" ", StringSplitOptions.RemoveEmptyEntries);

                                if (Counties.Keys.Contains(splits[0]))
                                    countyIncidence.Add(ParseCountyIncidence(splits));
                                
                            }


                            if (countyIncidence.Count > 0)
                                result.CountyRates = countyIncidence.ToArray();
                        }
                        else
                        if (!gotChoIncidence)
                        {

                            if (choIncidenceRegex.IsMatch(rawText) || choIncidenceRegex2.IsMatch(rawText))
                            {
                                Console.WriteLine("choin");
                                var lines = rawText.Split(newLine);//.SkipWhile(pX=>!choIncidenceRegex.IsMatch(pX));


                                var choIncidence = new List<HspcIncidence>();
                                var countyIncidence = new List<HspcIncidence>();

                                var choRegex = new Regex(@"^CHO\d");



                                foreach (var line in lines)
                                {

                                    var splits = line.Split(" ", StringSplitOptions.RemoveEmptyEntries);

                                    if (Counties.Keys.Contains(splits[0]))
                                        countyIncidence.Add(ParseIncidence(splits));
                                    else if (choRegex.IsMatch(splits[0]) && splits[0].Length==4  && splits.Length >2&& splits.Length<5)
                                        choIncidence.Add(ParseIncidence(splits));
                                }

                                if(choIncidence.Count>0)
                                    result.CHORates = choIncidence.ToArray();
                                if(countyIncidence.Count>0)
                                    result.CountyRates = countyIncidence.ToArray();

                                gotChoIncidence = true;


                            }
                        }


                        if (!gotCcaIncidence)
                        {

                            if (ccaIncidenceRegex.IsMatch(rawText))
                            {
                                Console.WriteLine("ccain");
                                var lines = rawText.Split(newLine);


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
                           
                            if (hospitalRegex.IsMatch(rawText) || hospitalNewRegex.IsMatch(rawText) || hospitalRegex2.IsMatch(rawText) || hospitalRegex3.IsMatch(rawText) || hospitalRegex4.IsMatch(rawText) || hospitalRegex5.IsMatch(rawText) || hospitalRegex6.IsMatch(rawText) || hospitalRegex7.IsMatch(rawText) || hospitalRegex8.IsMatch(rawText))
                            {
                                Console.WriteLine("hospital");
                                IEnumerable<string> lines; 
                                if (result.PreparedDate.Equals(DateTime.Parse("30/01/2021")))
                                {
                                    var templines = rawText.Split(newLine).SkipWhile(pX => !pX.StartsWith("0-4 yrs"));

                                    lines = templines.Take(2).Union(templines.Skip(6).TakeWhile(pX => !pX.StartsWith("Total") && !pX.Contains("hospitalised")));
                                        
                                }
                                else lines = rawText.Split(newLine).SkipWhile(pX => !pX.StartsWith("0-4 yrs"))
                                    .TakeWhile(pX => !pX.StartsWith("Total") && !pX.Contains("hospitalised"));



                                var hospitalised = new List<HspcHospitalised>();


                                if (!lines.Any())
                                {
                                    try
                                    {
                                        lines = rawText.Split(newLine);
                                        var numCases = lines.FirstOrDefault(pX => pX.StartsWith("cases (n) "))
                                            .Split(" ", StringSplitOptions.RemoveEmptyEntries).Skip(2).ToArray();
                                        var numHospitalised = lines
                                            .FirstOrDefault(pX => pX.StartsWith("(n) ") || pX.StartsWith("d (n) "))
                                            .Split(" ", StringSplitOptions.RemoveEmptyEntries).Skip(1).SkipWhile(pX=>!char.IsDigit(pX[0])).ToArray();
                                        
                                        var numHospitalisedPcnt = lines.FirstOrDefault(pX => pX.StartsWith("(%) "))
                                            .Split(" ", StringSplitOptions.RemoveEmptyEntries).Skip(1).ToArray();
                                        var numICU = lines.FirstOrDefault(pX => pX.StartsWith("ICU (n) "))
                                            .Split(" ", StringSplitOptions.RemoveEmptyEntries).Skip(2).ToArray();
                                        var numICUPcnt = lines.FirstOrDefault(pX => pX.StartsWith("ICU (%) "))
                                            .Split(" ", StringSplitOptions.RemoveEmptyEntries).Skip(2).ToArray();

                                        var categories = new[]
                                        {
                                            "0To4", "5To12", "13To18", "19To24", "25To34", 
                                            "35To44", "45To54", "55To64", "65To74", "75To84",
                                            "85Plus", "Unknown"
                                        };

                                        for (var j = 0; j < categories.Length; j++)
                                        {
                                            hospitalised.Add(new HspcHospitalised
                                            {
                                                Name = categories[j],
                                                NumberOfCases = j < numCases.Length ? Int32.Parse(numCases[j].Replace(",","")) : 0,
                                                CasesHospitalised = j <  numHospitalised.Length ? Int32.Parse(numHospitalised[j].Replace(",", "")) : 0,
                                                CasesHospitalisedPercent = j < numHospitalisedPcnt.Length ? Decimal.Parse(numHospitalisedPcnt[j].Replace(",", "")) : 0,
                                                CasesAdmittedToIcu = j < numICU.Length ? Int32.Parse(numICU[j].Replace(",", "")) : 0,
                                                CasesAdmittedToIcuPercent = j < numICUPcnt.Length ? Decimal.Parse(numICUPcnt[j].Replace(",", "")) : 0,
                                            });
                                        }


                                    }
                                    catch (Exception e)
                                    {

                                    }
                                }
                                else
                                {

                                    foreach (var line in lines)
                                    {
                                        try
                                        {
                                            decimal ParseSplitDecimal(string pInput)
                                            {
                                                pInput = pInput.Replace(",", "").Replace("**", "");
                                                if (pInput == "-") return 0;
                                                if (pInput == "<5") return 0;
                                                return Decimal.Parse(pInput);
                                            }
                                            Int32 ParseSplitInt(string pInput)
                                            {
                                                pInput = pInput.Replace(",", "").Replace("**", ""); ;
                                                if (pInput == "-") return 0;
                                                if (pInput == "<5") return 0;
                                                return Int32.Parse(pInput);
                                            }
                                            var splits = line.Split(" ", StringSplitOptions.RemoveEmptyEntries);
                                            if (splits.Length == 7)
                                            {
                                                hospitalised.Add(new HspcHospitalised
                                                {
                                                    Name = splits[0].Replace("-", "To").Replace("+", "Plus"),
                                                    NumberOfCases = ParseSplitInt(splits[2]),
                                                    CasesHospitalised = ParseSplitInt(splits[3]),
                                                    CasesHospitalisedPercent = ParseSplitDecimal(splits[4]),
                                                    CasesAdmittedToIcu = ParseSplitInt(splits[5]),
                                                    CasesAdmittedToIcuPercent = ParseSplitDecimal(splits[6]),
                                                });
                                                ;
                                            }
                                            else if (splits.Length == 5)
                                            {
                                                hospitalised.Add(new HspcHospitalised
                                                {
                                                    Name = splits[0].Replace("-", "To").Replace("+", "Plus"),
                                                    NumberOfCases = Int32.Parse(splits[splits[1]=="yrs" ? 2 : 1].Replace(",", "")),
                                                    CasesAdmittedToIcu = Int32.Parse(splits[splits[1] == "yrs" ? 3 : 2].Replace(",", "")),
                                                    CasesAdmittedToIcuPercent = Decimal.Parse(splits[splits[1] == "yrs" ? 4 : 3].Replace(",", ""))
                                                });
                                                ;
                                            }
                                            else if (splits.Length == 2)
                                            {
                                                hospitalised.Add(new HspcHospitalised
                                                {
                                                    Name = splits[0].Replace("-", "To").Replace("+", "Plus"),
                                                    NumberOfCases = Int32.Parse(splits[splits[1] == "yrs" ? 2 : 1].Replace(",", "")),
                                                   
                                                });
                                                ;
                                            }
                                            else if (splits.Length == 3 && splits[0] == "Unknown")
                                            {
                                                hospitalised.Add(new HspcHospitalised
                                                {
                                                    Name = splits[0].Replace("-", "To").Replace("+", "Plus"),
                                                    NumberOfCases = ParseSplitInt(splits[1]),
                                                    
                                                });
                                            }
                                            else
                                            {
                                                hospitalised.Add(new HspcHospitalised
                                                {
                                                    Name = splits[0].Replace("-", "To").Replace("+", "Plus"),
                                                    NumberOfCases = Int32.Parse(splits[splits[1] == "yrs" || splits[1] == "yrs^" ? 2 : 1].Replace(",", "")),
                                                    CasesHospitalised = Int32.Parse(splits[splits[1] == "yrs" || splits[1] == "yrs^" ? 3 :2].Replace(",", "")),
                                                    CasesHospitalisedPercent = Decimal.Parse(splits[splits[1] == "yrs" || splits[1] == "yrs^" ? 4 : 3].Replace(",", ""))
                                                });
                                                ;
                                            }
                                        }
                                        catch (Exception e)
                                        {

                                        }

                                    }
                                }

                                if(hospitalised.Count>0 )
                                    result.Hospitalised = hospitalised.ToArray();

                                gotHospital = true;
                            }
                        }

                        if (!gotCharacteristics)
                        {
                          
                            var matches = characteristicsRegex.Matches(rawText);
                            if (matches.Count == 0) matches = characteristicsRegex2.Matches(rawText);
                            if (matches.Count > 0)
                            {
                              
                               
                           
                                result.FromDate = DateTime.Parse(matches.First().Groups["fromDate"].Value);
                                result.ToDate = DateTime.Parse(matches.First().Groups["toDate"].Value);
                                var lines = rawText.Split(newLine)
                                    .SkipWhile(pX => !pX.StartsWith("Total number of confirmed cases")).ToArray();
                               

                                ApplyBest(lines, new List<KeyValuePair<string, Action<string[]>>[]>
                                {
                                    new[]{
                                        new KeyValuePair<string, Action<string[]>>("Total number of confirmed cases", pSplits => result.TotalConfirmedCases = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("0-4 yrs", pSplits => result.AgeCharacteristics.Age0To4 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("5-12 yrs", pSplits => result.AgeCharacteristics.Age5To12 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("13-18 yrs", pSplits => result.AgeCharacteristics.Age13To18 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("19-24 yrs", pSplits => result.AgeCharacteristics.Age19To24 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("25-34 yrs", pSplits => result.AgeCharacteristics.Age25To34 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("35-44 yrs", pSplits => result.AgeCharacteristics.Age35To44 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("45-54 yrs", pSplits => result.AgeCharacteristics.Age45To54 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("55-64 yrs", pSplits => result.AgeCharacteristics.Age55To64 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("65-74 yrs", pSplits => result.AgeCharacteristics.Age65To74 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("75-84 yrs", pSplits => result.AgeCharacteristics.Age75To84 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("85+ yrs", pSplits => result.AgeCharacteristics.Age85Plus = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Unknown", pSplits => result.AgeCharacteristics.AgeUnknown = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Sex Male:Female ratio", pSplits => result.SexCharacteristics.MaleFemaleRatio = decimal.Parse(pSplits[0])),
                                        new KeyValuePair<string, Action<string[]>>("Male", pSplits => result.SexCharacteristics.Male = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Female", pSplits => result.SexCharacteristics.Female = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Unknown", pSplits => result.SexCharacteristics.Unknown = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Age Median age (years)", pSplits => result.AgeCharacteristics.MedianAge = Int32.Parse(pSplits[0])),
                                        new KeyValuePair<string, Action<string[]>>("Mean age (years)", pSplits => result.AgeCharacteristics.MeanAge = Int32.Parse(pSplits[0])),
                                        new KeyValuePair<string, Action<string[]>>("Age range (years)", pSplits => { }),
                                        new KeyValuePair<string, Action<string[]>>("Yes", pSplits => result.UnderlyingConditionsCharacteristics.Yes = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("No", pSplits => result.UnderlyingConditionsCharacteristics.No = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Unknown", pSplits => result.UnderlyingConditionsCharacteristics.Unknown = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Underlying clinical conditions", pSplits => { }),
                                        new KeyValuePair<string, Action<string[]>>("Yes", pSplits => result.SymptomStatusAtTimeOfTestCharacteristics.Yes = ParseAbsoluteAndPercent(pSplits)), 
                                        new KeyValuePair<string, Action<string[]>>("No", pSplits => result.SymptomStatusAtTimeOfTestCharacteristics.No = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Unknown", pSplits => result.SymptomStatusAtTimeOfTestCharacteristics.Unknown = ParseAbsoluteAndPercent(pSplits)),
                                    },
                                    new[]{ // new
                                        new KeyValuePair<string, Action<string[]>>("Total number of confirmed cases", pSplits => result.TotalConfirmedCases = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("0-4 yrs", pSplits => result.AgeCharacteristics.Age0To4 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("5-12 yrs", pSplits => result.AgeCharacteristics.Age5To12 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("13-18 yrs", pSplits => result.AgeCharacteristics.Age13To18 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("19-24 yrs", pSplits => result.AgeCharacteristics.Age19To24 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("25-34 yrs", pSplits => result.AgeCharacteristics.Age25To34 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("35-44 yrs", pSplits => result.AgeCharacteristics.Age35To44 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("45-54 yrs", pSplits => result.AgeCharacteristics.Age45To54 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("55-64 yrs", pSplits => result.AgeCharacteristics.Age55To64 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("65-74 yrs", pSplits => result.AgeCharacteristics.Age65To74 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("75-84 yrs", pSplits => result.AgeCharacteristics.Age75To84 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("85+ yrs", pSplits => result.AgeCharacteristics.Age85Plus = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Unknown", pSplits => result.AgeCharacteristics.AgeUnknown = pSplits.Length>0 ? ParseAbsoluteAndPercent(pSplits) : new HspcAbsoluteAndPercent() { Absolute = 0, Percent=0}),
                                        new KeyValuePair<string, Action<string[]>>("Sex Male:Female ratio", pSplits => result.SexCharacteristics.MaleFemaleRatio = decimal.Parse(pSplits[0])),
                                        new KeyValuePair<string, Action<string[]>>("Male", pSplits => result.SexCharacteristics.Male = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Female", pSplits => result.SexCharacteristics.Female = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Unknown", pSplits => result.SexCharacteristics.Unknown = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Age Median age (years)", pSplits => result.AgeCharacteristics.MedianAge = Int32.Parse(pSplits[0])),
                                        new KeyValuePair<string, Action<string[]>>("Mean age (years)", pSplits => result.AgeCharacteristics.MeanAge = Int32.Parse(pSplits[0])),
                                        new KeyValuePair<string, Action<string[]>>("Age range (years)", pSplits => { }),
                                        new KeyValuePair<string, Action<string[]>>("Characteristic Number of cases Percent Percent where known", pSplits => { }),

                                        new KeyValuePair<string, Action<string[]>>("Total number of cases", pSplits => { }),
                                        new KeyValuePair<string, Action<string[]>>("Yes", pSplits => result.SymptomStatusAtTimeOfTestCharacteristics.Yes = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("No", pSplits => result.SymptomStatusAtTimeOfTestCharacteristics.No = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Unknown", pSplits => result.SymptomStatusAtTimeOfTestCharacteristics.Unknown = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Total number where known", pSplits => { }),
                                    },
                                    new[]{
                                        new KeyValuePair<string, Action<string[]>>("Total number of confirmed cases", pSplits => result.TotalConfirmedCases = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Sex Male:Female ratio", pSplits => result.SexCharacteristics.MaleFemaleRatio = decimal.Parse(pSplits[0])),
                                        new KeyValuePair<string, Action<string[]>>("Male", pSplits => result.SexCharacteristics.Male = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Female", pSplits => result.SexCharacteristics.Female = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Unknown", pSplits => result.SexCharacteristics.Unknown = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Yes", pSplits => result.UnderlyingConditionsCharacteristics.Yes = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("No", pSplits => result.UnderlyingConditionsCharacteristics.No = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Unknown", pSplits => result.UnderlyingConditionsCharacteristics.Unknown = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Underlying clinical conditions", pSplits => { }),
                                        new KeyValuePair<string, Action<string[]>>("Yes", pSplits => result.SymptomStatusAtTimeOfTestCharacteristics.Yes = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("No", pSplits => result.SymptomStatusAtTimeOfTestCharacteristics.No = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Unknown", pSplits => result.SymptomStatusAtTimeOfTestCharacteristics.Unknown = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Symptom", pSplits => { }), 
                                        new KeyValuePair<string, Action<string[]>>("*Symptom", pSplits => { }),
                                        new KeyValuePair<string, Action<string[]>>("cases", pSplits => { }),
                                        new KeyValuePair<string, Action<string[]>>("Report", pSplits => { }),
                                        new KeyValuePair<string, Action<string[]>>("Age Median age (years)", pSplits => result.AgeCharacteristics.MedianAge = Int32.Parse(pSplits[0])),
                                        new KeyValuePair<string, Action<string[]>>("Mean age (years)", pSplits => result.AgeCharacteristics.MeanAge = Int32.Parse(pSplits[0])),
                                        new KeyValuePair<string, Action<string[]>>("Age range (years)", pSplits => { }),
                                        new KeyValuePair<string, Action<string[]>>("0-4 yrs", pSplits => result.AgeCharacteristics.Age0To4 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("5-12 yrs", pSplits => result.AgeCharacteristics.Age5To12 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("13-18 yrs", pSplits => result.AgeCharacteristics.Age13To18 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("19-24 yrs", pSplits => result.AgeCharacteristics.Age19To24 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("25-34 yrs", pSplits => result.AgeCharacteristics.Age25To34 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("35-44 yrs", pSplits => result.AgeCharacteristics.Age35To44 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("45-54 yrs", pSplits => result.AgeCharacteristics.Age45To54 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("55-64 yrs", pSplits => result.AgeCharacteristics.Age55To64 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("65-74 yrs", pSplits => result.AgeCharacteristics.Age65To74 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("75-84 yrs", pSplits => result.AgeCharacteristics.Age75To84 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("85+ yrs", pSplits => result.AgeCharacteristics.Age85Plus = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Unknown", pSplits => result.AgeCharacteristics.AgeUnknown = ParseAbsoluteAndPercent(pSplits)),
                                        
                                       
                                    },
                                    new[]{
                                        new KeyValuePair<string, Action<string[]>>("Total number of confirmed cases", pSplits => result.TotalConfirmedCases = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("0-4 yrs", pSplits => result.AgeCharacteristics.Age0To4 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("5-14 yrs", pSplits => result.AgeCharacteristics.Age5To14 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("15-24 yrs", pSplits => result.AgeCharacteristics.Age15To24 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("25-34 yrs", pSplits => result.AgeCharacteristics.Age25To34 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("35-44 yrs", pSplits => result.AgeCharacteristics.Age35To44 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("45-54 yrs", pSplits => result.AgeCharacteristics.Age45To54 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("55-64 yrs", pSplits => result.AgeCharacteristics.Age55To64 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("65-74 yrs", pSplits => result.AgeCharacteristics.Age65To74 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("75-84 yrs", pSplits => result.AgeCharacteristics.Age75To84 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("85+ yrs", pSplits => result.AgeCharacteristics.Age85Plus = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Unknown", pSplits => result.AgeCharacteristics.AgeUnknown = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Sex Male:Female ratio", pSplits => result.SexCharacteristics.MaleFemaleRatio = decimal.Parse(pSplits[0])),
                                        new KeyValuePair<string, Action<string[]>>("Male", pSplits => result.SexCharacteristics.Male = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Female", pSplits => result.SexCharacteristics.Female = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Unknown", pSplits => result.SexCharacteristics.Unknown = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Age Median age (years)", pSplits => result.AgeCharacteristics.MedianAge = Int32.Parse(pSplits[0])),
                                        new KeyValuePair<string, Action<string[]>>("Mean age (years)", pSplits => result.AgeCharacteristics.MeanAge = Int32.Parse(pSplits[0])),
                                        new KeyValuePair<string, Action<string[]>>("Age range (years)", pSplits => { }),
                                        new KeyValuePair<string, Action<string[]>>("Yes", pSplits => result.UnderlyingConditionsCharacteristics.Yes = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("No", pSplits => result.UnderlyingConditionsCharacteristics.No = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Unknown", pSplits => result.UnderlyingConditionsCharacteristics.Unknown = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Underlying clinical conditions", pSplits => { }),
                                        new KeyValuePair<string, Action<string[]>>("Yes", pSplits => result.SymptomStatusAtTimeOfTestCharacteristics.Yes = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("No", pSplits => result.SymptomStatusAtTimeOfTestCharacteristics.No = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Unknown", pSplits => result.SymptomStatusAtTimeOfTestCharacteristics.Unknown = ParseAbsoluteAndPercent(pSplits)),
                                    },
                                    new []
                                    {
                                        new KeyValuePair<string, Action<string[]>>("Total number of confirmed cases", pSplits => result.TotalConfirmedCases = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("0-4 yrs", pSplits => result.AgeCharacteristics.Age0To4 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("5-12 yrs", pSplits => result.AgeCharacteristics.Age5To12 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("13-18 yrs", pSplits => result.AgeCharacteristics.Age13To18 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("19-24 yrs", pSplits => result.AgeCharacteristics.Age19To24 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("25-34 yrs", pSplits => result.AgeCharacteristics.Age25To34 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("35-44 yrs", pSplits => result.AgeCharacteristics.Age35To44 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("45-54 yrs", pSplits => result.AgeCharacteristics.Age45To54 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("55-64 yrs", pSplits => result.AgeCharacteristics.Age55To64 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("65-74 yrs", pSplits => result.AgeCharacteristics.Age65To74 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("75-84 yrs", pSplits => result.AgeCharacteristics.Age75To84 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("85+ yrs", pSplits => result.AgeCharacteristics.Age85Plus = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Unknown", pSplits => result.AgeCharacteristics.AgeUnknown = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Sex Male:Female ratio", pSplits => result.SexCharacteristics.MaleFemaleRatio = decimal.Parse(pSplits[0])),
                                        new KeyValuePair<string, Action<string[]>>("Male", pSplits => result.SexCharacteristics.Male = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Female", pSplits => result.SexCharacteristics.Female = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Unknown", pSplits => result.SexCharacteristics.Unknown = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Yes", pSplits => result.UnderlyingConditionsCharacteristics.Yes = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("No", pSplits => result.UnderlyingConditionsCharacteristics.No = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Unknown", pSplits => result.UnderlyingConditionsCharacteristics.Unknown = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Underlying clinical conditions", pSplits => { }),
                                        new KeyValuePair<string, Action<string[]>>("Yes", pSplits => result.SymptomStatusAtTimeOfTestCharacteristics.Yes = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("No", pSplits => result.SymptomStatusAtTimeOfTestCharacteristics.No = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Unknown", pSplits => result.SymptomStatusAtTimeOfTestCharacteristics.Unknown = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("aSymptom", pSplits => { }), new KeyValuePair<string, Action<string[]>>("*Symptom", pSplits => { }),
                                        new KeyValuePair<string, Action<string[]>>("Age Median age (years)", pSplits => result.AgeCharacteristics.MedianAge = Int32.Parse(pSplits[0])),
                                        new KeyValuePair<string, Action<string[]>>("Mean age (years)", pSplits => result.AgeCharacteristics.MeanAge = Int32.Parse(pSplits[0])),
                                        new KeyValuePair<string, Action<string[]>>("Age range (years)", pSplits => { }),
                                    },
                                    new []
                                    {
                                        new KeyValuePair<string, Action<string[]>>("Total number of confirmed cases", pSplits => result.TotalConfirmedCases = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("0-4 yrs", pSplits => result.AgeCharacteristics.Age0To4 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("5-12 yrs", pSplits => result.AgeCharacteristics.Age5To12 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("13-18 yrs", pSplits => result.AgeCharacteristics.Age13To18 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("19-24 yrs", pSplits => result.AgeCharacteristics.Age19To24 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("25-34 yrs", pSplits => result.AgeCharacteristics.Age25To34 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("35-44 yrs", pSplits => result.AgeCharacteristics.Age35To44 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("45-54 yrs", pSplits => result.AgeCharacteristics.Age45To54 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("55-64 yrs", pSplits => result.AgeCharacteristics.Age55To64 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("65-74 yrs", pSplits => result.AgeCharacteristics.Age65To74 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("75-84 yrs", pSplits => result.AgeCharacteristics.Age75To84 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("85+ yrs", pSplits => result.AgeCharacteristics.Age85Plus = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Unknown", pSplits => result.AgeCharacteristics.AgeUnknown = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Sex Male:Female ratio", pSplits => result.SexCharacteristics.MaleFemaleRatio = decimal.Parse(pSplits[0])),
                                        new KeyValuePair<string, Action<string[]>>("Male", pSplits => result.SexCharacteristics.Male = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Female", pSplits => result.SexCharacteristics.Female = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Unknown", pSplits => result.SexCharacteristics.Unknown = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Report", pSplits => { }),
                                        new KeyValuePair<string, Action<string[]>>("Age Median age (years)", pSplits => result.AgeCharacteristics.MedianAge = Int32.Parse(pSplits[0])),
                                        new KeyValuePair<string, Action<string[]>>("Mean age (years)", pSplits => result.AgeCharacteristics.MeanAge = Int32.Parse(pSplits[0])),
                                        new KeyValuePair<string, Action<string[]>>("Age range (years)", pSplits => { }),
                                    },
                                    new []
                                    {
                                        new KeyValuePair<string, Action<string[]>>("Total number of confirmed cases", pSplits => result.TotalConfirmedCases = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Sex Male:Female ratio", pSplits => result.SexCharacteristics.MaleFemaleRatio = decimal.Parse(pSplits[0])),
                                        new KeyValuePair<string, Action<string[]>>("Male", pSplits => result.SexCharacteristics.Male = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Female", pSplits => result.SexCharacteristics.Female = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Unknown", pSplits => result.SexCharacteristics.Unknown = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Age Median age (years)", pSplits => result.AgeCharacteristics.MedianAge = Int32.Parse(pSplits[0])),
                                        new KeyValuePair<string, Action<string[]>>("Mean age (years)", pSplits => result.AgeCharacteristics.MeanAge = Int32.Parse(pSplits[0])),
                                        new KeyValuePair<string, Action<string[]>>("Age range (years)", pSplits => { }),
                                        new KeyValuePair<string, Action<string[]>>("Report prepared by Health Protection Surveillance Centre", pSplits => { }),
                                        new KeyValuePair<string, Action<string[]>>("0-4 yrs", pSplits => result.AgeCharacteristics.Age0To4 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("5-12 yrs", pSplits => result.AgeCharacteristics.Age5To12 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("13-18 yrs", pSplits => result.AgeCharacteristics.Age13To18 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("19-24 yrs", pSplits => result.AgeCharacteristics.Age19To24 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("25-34 yrs", pSplits => result.AgeCharacteristics.Age25To34 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("35-44 yrs", pSplits => result.AgeCharacteristics.Age35To44 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("45-54 yrs", pSplits => result.AgeCharacteristics.Age45To54 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("55-64 yrs", pSplits => result.AgeCharacteristics.Age55To64 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("65-74 yrs", pSplits => result.AgeCharacteristics.Age65To74 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("75-84 yrs", pSplits => result.AgeCharacteristics.Age75To84 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("85+ yrs", pSplits => result.AgeCharacteristics.Age85Plus = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Unknown", pSplits => result.AgeCharacteristics.AgeUnknown = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Yes", pSplits => result.UnderlyingConditionsCharacteristics.Yes = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("No", pSplits => result.UnderlyingConditionsCharacteristics.No = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Unknown", pSplits => result.UnderlyingConditionsCharacteristics.Unknown = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Underlying clinical conditions", pSplits => { }),
                                        new KeyValuePair<string, Action<string[]>>("Yes", pSplits => result.SymptomStatusAtTimeOfTestCharacteristics.Yes = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("No", pSplits => result.SymptomStatusAtTimeOfTestCharacteristics.No = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Unknown", pSplits => result.SymptomStatusAtTimeOfTestCharacteristics.Unknown = ParseAbsoluteAndPercent(pSplits)),

                                    },
                                    new [] // 20210330
                                    {
                                        new KeyValuePair<string, Action<string[]>>("Total number of confirmed cases", pSplits => result.TotalConfirmedCases = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Sex Male:Female ratio", pSplits => result.SexCharacteristics.MaleFemaleRatio = decimal.Parse(pSplits[0])),
                                        new KeyValuePair<string, Action<string[]>>("Male", pSplits => result.SexCharacteristics.Male = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Female", pSplits => result.SexCharacteristics.Female = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Unknown", pSplits => result.SexCharacteristics.Unknown = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Age Median age (years)", pSplits => result.AgeCharacteristics.MedianAge = Int32.Parse(pSplits[0])),
                                        new KeyValuePair<string, Action<string[]>>("Mean age (years)", pSplits => result.AgeCharacteristics.MeanAge = Int32.Parse(pSplits[0])),
                                        new KeyValuePair<string, Action<string[]>>("Age range (years)", pSplits => { }),
                                        new KeyValuePair<string, Action<string[]>>("Symptom status at time of test", pSplits => { }),
                                        new KeyValuePair<string, Action<string[]>>("*Symptom status is recorded at the time of test and is not updated", pSplits => { }),
                                        new KeyValuePair<string, Action<string[]>>("cases may subsequently develop symptoms however this will ", pSplits => { }),
                                        new KeyValuePair<string, Action<string[]>>("0-4 yrs", pSplits => result.AgeCharacteristics.Age0To4 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("5-12 yrs", pSplits => result.AgeCharacteristics.Age5To12 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("13-18 yrs", pSplits => result.AgeCharacteristics.Age13To18 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("19-24 yrs", pSplits => result.AgeCharacteristics.Age19To24 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("25-34 yrs", pSplits => result.AgeCharacteristics.Age25To34 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("35-44 yrs", pSplits => result.AgeCharacteristics.Age35To44 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("45-54 yrs", pSplits => result.AgeCharacteristics.Age45To54 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("55-64 yrs", pSplits => result.AgeCharacteristics.Age55To64 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("65-74 yrs", pSplits => result.AgeCharacteristics.Age65To74 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("75-84 yrs", pSplits => result.AgeCharacteristics.Age75To84 = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("85+ yrs", pSplits => result.AgeCharacteristics.Age85Plus = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Yes", pSplits => result.SymptomStatusAtTimeOfTestCharacteristics.Yes = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("No", pSplits => result.SymptomStatusAtTimeOfTestCharacteristics.No = ParseAbsoluteAndPercent(pSplits)),
                                        new KeyValuePair<string, Action<string[]>>("Unknown", pSplits => result.SymptomStatusAtTimeOfTestCharacteristics.Unknown = ParseAbsoluteAndPercent(pSplits)),

                                    }
                                });

                          


                                gotCharacteristics = true;
                            }



                        }
                    }
                }

                if (result.FromDate.Year < 200)
                {
                    if (result.PreparedDate.Equals(DateTime.Parse("23/10/2020")))
                    {
                        result.FromDate = DateTime.Parse("");
                        result.ToDate = DateTime.Parse("23/10/2020");
                    }else
                        result.FromDate = result.ToDate.AddDays(-13);

                }

                if(result.ToDate.Year<2000)
                    result.ToDate = result.FromDate.AddDays(13);
                return result;
            }
            //catch (Exception e)
            //{
            //     Console.WriteLine(e.StackTrace);
            //     throw e;
            //}
        }

        public override string ToString() => $"{FromDate.ToShortDateString()}-{ToDate.ToShortDateString()} ({PreparedDate.ToShortDateString()})";
    }
}
