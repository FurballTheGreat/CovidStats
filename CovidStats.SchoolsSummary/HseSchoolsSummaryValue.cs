using System.Runtime.Serialization;

namespace CovidStats.SchoolsSummary
{
    [DataContract]
    public class HseSchoolsSummaryValue
    {
        [DataMember]
        public string Name { get; set; }
        [DataMember]
        public decimal CumulativeToDate { get; set; }
        [DataMember]
        public decimal WeekValue { get; set; }
        
        public override string ToString()
        {
            return $"{Name} - ToDate: {CumulativeToDate}, Week Value: {WeekValue}";
        }
    }
}