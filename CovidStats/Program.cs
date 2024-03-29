﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml;
using CovidStates.OperationsUpdate;
using CovidStats.DailyEpidemiology;
using CovidStats.SchoolsSummary;
using CovidStats.WeeklyEpidemiology;
using iText.Kernel.Pdf;
using Microsoft.VisualBasic;
using UglyToad.PdfPig.Graphics.Operations.TextState;

namespace CovidStats
{
    class Program
    {
        static void Main(string[] args)
        {
            var options = new Dictionary<string, Action>
            {
                {"parseschools", () => ParseSchoolsMassTestingReports(args[1], args[2])},
                {"parsedaily", () => ParseDailyReports(args[1], args[2], args[3])},
                {"parseweekly", () => ParseWeeklyReports(args[1], args[2])},
                {"parseops", () => ParseOpReports(args[1], args[2])}
            };
            var command = args[0].ToLowerInvariant();
            if (options.ContainsKey(command))
                options[command]();
            else
            {
                Console.WriteLine("SYNTAX: covidstats {command} [param1 param2..]");
            }
            
            
        }

        private static void ParseOpReports(string pInputDir, string pOutputDir)
        {
            var files = new List<HseOperationsUpdate>();
            foreach (var file in Directory.GetFiles(pInputDir))
            {
                Console.WriteLine($"Processing {file}");
                var result = HseOperationsUpdate.Load(File.ReadAllBytes(file), Path.GetFileName(file));
                files.Add(result);
            }

            files.RemoveAll(pX=>pX.CoverDate.Year==1);
            files.Sort((pLeft,pRight)=>pLeft.CoverDate.CompareTo(pRight.CoverDate));

            var schoolsTemplate = new HseOpsUpdate()
            {
                Session = new Dictionary<string, object> { { "Days", files } }
            };
            schoolsTemplate.Initialize();
            var transformText = schoolsTemplate.TransformText();
            File.WriteAllText($"{pOutputDir}{Path.DirectorySeparatorChar}OpsDays.xml", transformText);
            File.WriteAllText($"{pOutputDir}{Path.DirectorySeparatorChar}OpsDays.csv", XmlToCsv(transformText));

            var serial = new DataContractSerializer(typeof(HseOperationsUpdate));
            foreach (var week in files)
            {
                using var file = File.Create($"{pOutputDir}{Path.DirectorySeparatorChar}DailyWeekFrom-{week.CoverDate:yyyyMMdd}.xml");
                using var writer = XmlWriter.Create(file, new XmlWriterSettings { Indent = true });
                serial.WriteObject(writer, week);
                writer.Flush();
                file.Flush();
            }
        }


        private static string XmlToCsv(string pXml)
        {
            var xml = new XmlDocument();
            xml.LoadXml(pXml);
            var sw = new StringWriter();
            XmlNode firstNode = xml.DocumentElement.FirstChild;
            var first = true;
            foreach (XmlNode child in firstNode.ChildNodes)
            {
                if (first) first = false; else sw.Write(",");
                sw.Write(child.LocalName);
            }

            sw.WriteLine();
            
            foreach (XmlNode dayNode in xml.DocumentElement.ChildNodes)
            {
                first = true;
                foreach (XmlNode child in dayNode.ChildNodes)
                {
                    if (first) first = false; else sw.Write(",");
                    sw.Write(child.InnerText);
                }

                sw.WriteLine();
            }

            return sw.ToString();
        }

        private static void ParseDailyReports(string pInputDir, string pOutputDir, string pOverridesDir)
        {
            var serial = new DataContractSerializer(typeof(HspcDailyEpidemiology));
            var sources = new List<HspcDailyEpidemiology>();
            foreach (var file in Directory.GetFiles(pInputDir))
            {
                Console.WriteLine($"Processing {file}");
                sources.Add(HspcDailyEpidemiology.Load(File.ReadAllBytes(file), Path.GetFileName(file),
                    pPreparedDate =>
                    {
                        var filename = $"{pOverridesDir}{Path.DirectorySeparatorChar}{pPreparedDate:yyyyMMdd}.xml";
                        if (!File.Exists(filename))
                            return null;

                        using var strm = File.OpenRead(filename);
                        return serial.ReadObject(strm) as HspcDailyEpidemiology;
                    }));
            }

            sources.Sort((pLeft, pRight)=>pLeft.FromDate.CompareTo(pRight.FromDate));

            var schoolsTemplate = new DailyEpidemiologyXml()
            {
                Session = new Dictionary<string, object> { { "Days", sources } }
            };
            schoolsTemplate.Initialize();
            var transformText = schoolsTemplate.TransformText();
            File.WriteAllText($"{pOutputDir}{Path.DirectorySeparatorChar}Days.xml", transformText);
            File.WriteAllText($"{pOutputDir}{Path.DirectorySeparatorChar}Days.csv",  XmlToCsv(transformText));
            
            foreach (var week in sources)
            {
                using var file = File.Create($"{pOutputDir}{Path.DirectorySeparatorChar}DailyWeekFrom-{week.FromDate:yyyyMMdd}.xml");
                using var writer = XmlWriter.Create(file, new XmlWriterSettings { Indent = true });
                serial.WriteObject(writer, week);
                writer.Flush();
                file.Flush();
            }
        }


        private static void ParseWeeklyReports(string pInputDir, string pOutputDir)
        {
            var sources = new List<HpscWeeklyEpidemiology>();
            foreach (var file in Directory.GetFiles(pInputDir))
                sources.Add(HpscWeeklyEpidemiology.Load(File.ReadAllBytes(file), Path.GetFileName(file)));
            sources.Sort((pLeft, pRight) => pLeft.FromDate.CompareTo(pRight.FromDate));

            var schoolsTemplate = new WeeklyIncidenceRatesXml()
            {
                Session = new Dictionary<string, object> { { "Weeks", HpscWeeklyEpidemiology.GetCombinedHeatmap(sources).ToList() } }
            };
            schoolsTemplate.Initialize();
            var transformText = schoolsTemplate.TransformText();
            File.WriteAllText($"{pOutputDir}{Path.DirectorySeparatorChar}WeeklyIncidence.xml", transformText);
            File.WriteAllText($"{pOutputDir}{Path.DirectorySeparatorChar}WeeklyIncidence.csv", XmlToCsv(transformText));
            var serial = new DataContractSerializer(typeof(HpscWeeklyEpidemiology));
            foreach (var week in sources)
            {
                using var file = File.Create($"{pOutputDir}{Path.DirectorySeparatorChar}WeeklyPrepared-{week.FromDate:yyyyMMdd}.xml");
                using var writer = XmlWriter.Create(file, new XmlWriterSettings { Indent = true });
                serial.WriteObject(writer, week);
                writer.Flush();
                file.Flush();
            }
        }

        private static void ParseSchoolsMassTestingReports(string pInputDir, string pOutputDir)
        {
            var sources = new List<HseSchoolsSummary>();
            foreach (var file in Directory.GetFiles(pInputDir))
                sources.Add(HseSchoolsSummary.LoadSummary(File.ReadAllBytes(file), Path.GetFileName(file))); 

            foreach(var source in sources)
                if (source.Year == 2020)
                {
                   

                } else if (source.Year == 2021 )
                {
                    if (source.Week != 52)
                        source.Week += 53;
                    else
                    {
                        source.Week = 53;
                        source.Year = 2020;
                    }
                }

            sources.Sort((pLeft, pRight)=>pLeft.Week.CompareTo(pRight.Week));
            var schoolsTemplate = new SchoolWeeksXml
            {
                Session = new Dictionary<string, object> {{"Weeks", sources}}
            };
            schoolsTemplate.Initialize();
            var transformText = schoolsTemplate.TransformText();
            File.WriteAllText($"{pOutputDir}{Path.DirectorySeparatorChar}SchoolWeeks.xml", transformText);
            File.WriteAllText($"{pOutputDir}{Path.DirectorySeparatorChar}SchoolWeeks.csv", XmlToCsv(transformText));
            var serial = new DataContractSerializer(typeof(HseSchoolsSummary));
            foreach (var week in sources)
            {
                using var file = File.Create($"{pOutputDir}{Path.DirectorySeparatorChar}Week{week.Week}.xml");
                using var writer = XmlWriter.Create(file, new XmlWriterSettings {Indent = true});
                serial.WriteObject(writer, week);
                writer.Flush();
                file.Flush();
            }
        }
    }
}
