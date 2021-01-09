using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Xml;
using CovidStats.DailyEpidemiology;
using CovidStats.SchoolsSummary;

namespace CovidStats
{
    class Program
    {
        static void Main(string[] args)
        {
            var options = new Dictionary<string, Action>
            {
                {"parseschools", () => ParseSchoolsMassTestingReports(args[1], args[2])},
                {"dailycopyrename", () => RenameAndCopyDaily(args[1], args[2])}
            };
            var command = args[0].ToLowerInvariant();
            if (options.ContainsKey(command))
                options[command]();
            else
            {
                Console.WriteLine("SYNTAX: covidstats {command} [param1 param2..]");
            }
            
            
        }

        private static void RenameAndCopyDaily(string pInputDir, string pOutputDir)
        {
            var sources = new List<HspcDailyEpidemiology>();
            foreach (var file in Directory.GetFiles(pInputDir))
                sources.Add(HspcDailyEpidemiology.Load(File.ReadAllBytes(file)));
            sources.Sort((pLeft, pRight)=>pLeft.FromDate.CompareTo(pRight.FromDate));
        }

        private static void ParseSchoolsMassTestingReports(string pInputDir, string pOutputDir)
        {
            var sources = new List<HseSchoolsSummary>();
            foreach (var file in Directory.GetFiles(pInputDir))
                sources.Add(HseSchoolsSummary.LoadSummary(File.ReadAllBytes(file)));

            var schoolsTemplate = new SchoolWeeksXml
            {
                Session = new Dictionary<string, object> {{"Weeks", sources}}
            };
            schoolsTemplate.Initialize();
            var transformText = schoolsTemplate.TransformText();
            File.WriteAllText($"{pOutputDir}\\Weeks.xml", transformText);
            var serial = new DataContractSerializer(typeof(HseSchoolsSummary));
            foreach (var week in sources)
            {
                using var file = File.Create($"{pOutputDir}\\Week{week.Week}.xml");
                using var writer = XmlWriter.Create(file, new XmlWriterSettings {Indent = true});
                serial.WriteObject(writer, week);
                writer.Flush();
                file.Flush();
            }
        }
    }
}
