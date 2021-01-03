using System.Runtime.Serialization;

namespace CovidStats
{
    [DataContract]
    public class HseSchoolsSummary
    {
        [DataMember]
        public HseSchoolsSummaryValue[] Values
        {
            get;
            set;
        }
        [DataMember]

        public HseSchoolsFacilityValue[] FacilityValues
        {
            get;
            set;
        }

 
    }
}