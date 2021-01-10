using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using CovidStats.WeeklyEpidemiology;

namespace CovidStats
{
    public class PopulationBreakdown
    {
        private static Dictionary<string, int> _breakdowns = new Dictionary<string, int>
        {
            {"0-4",      331515},
            {"5-12",     548693},
            {"13-18",    371588},
            {"19-24",    331208},
            {"25-34",    659410},
            {"35-44",    746881},
            {"45-54",    626045},
            {"55-64",    508958},
            {"65-74",    373508},
            {"75-84",    196504},
            {"85+",      67555},
            {"National", 4761865},
        };

        public static string MapAbsoluteFromRate(HpscWeeklyHeatmapEntry pEntry)
        {
            if (!_breakdowns.ContainsKey(pEntry.Name))
                return "";

            var population = (decimal) _breakdowns[pEntry.Name];
            return $"{Math.Round((population / 100000) * pEntry.Value)}";
        }
    }
}