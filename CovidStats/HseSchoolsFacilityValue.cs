using System.Runtime.Serialization;

namespace CovidStats
{
    [DataContract]
    public class HseSchoolsFacilityValue
    {
        [DataMember]
        public string Name { get; set; }
        [DataMember]
        public int NoFacilities { get; set; }
        [DataMember]
        public int NoTested { get; set; }
        [DataMember]
        public int NoDetected { get; set; }
        [DataMember]
        public decimal DetectedPercent { get; set; }
        [DataMember]
        public int NoNotDetected { get; set; }
    }
}