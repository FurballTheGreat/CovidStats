using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Xml;

namespace CovidStats
{
    class Program
    {
        private static readonly HseSchoolsSummaryWeekSource[] WeekSources = new[]
        {
            new HseSchoolsSummaryWeekSource(@"https://www.hse.ie/eng/services/news/newsfeatures/covid19-updates/covid-19-schools-mass-testing-report-week-47.pdf", "Week47.pdf"),
            new HseSchoolsSummaryWeekSource(@"https://www.hse.ie/eng/services/news/newsfeatures/covid19-updates/covid19-schools-mass-testing-report-week-48.pdf", "Week48.pdf"),
            new HseSchoolsSummaryWeekSource(@"https://www.hse.ie/eng/services/news/newsfeatures/covid19-updates/covid-19-schools-mass-testing-report-week-49.pdf", "Week49.pdf"),
            new HseSchoolsSummaryWeekSource(@"https://www.hse.ie/eng/services/news/newsfeatures/covid19-updates/covid-19-schools-mass-testing-report-week-50.pdf", "Week50.pdf"),
            new HseSchoolsSummaryWeekSource(@"https://www.hse.ie/eng/services/news/newsfeatures/covid19-updates/covid-19-schools-and-childcare-facilities-mass-testing-report-week-51.pdf", "Week51.pdf"),

        };
        static void Main(string[] args)
        {
            var sources = new List<HseSchoolsSummary>();
            foreach (var source in WeekSources) 
                sources.Add(HseSchoolsSummary.LoadSummary(source.GetBytes()));



        }
    }
}
