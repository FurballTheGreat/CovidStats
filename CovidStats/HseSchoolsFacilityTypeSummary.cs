using System.Runtime.Serialization;

namespace CovidStats
{
    [DataContract]
    public class HseSchoolsFacilityTypeSummary
    {
        [DataMember]
        public HseSchoolsFacilityValue[] Testing
        {
            get;
            set;
        }

        [DataMember]
        public HseSchoolsFacilityValue[] MassTesting
        {
            get;
            set;
        }
    }
}