namespace CovidStats.SchoolsSummary
{
    public class HseSchoolsSummaryGraphValue
    {
        public int Value { get; set; }
        public int Week { get; set; }

        public override string ToString() => $"WK{Week} = {Value}";
    }
}