using System.Runtime.Serialization;

namespace CovidStats
{
    [DataContract]
    public class HseSchoolsSummary
    {
        public HseSchoolsSummary()
        {
            Schools = new HseSchoolsFacilityTypeSummary();
            Childcare = new HseSchoolsFacilityTypeSummary();
        }

        [DataMember]
        public HseSchoolsSummaryValue[] Values
        {
            get;
            set;
        }
        [DataMember]

        public HseSchoolsFacilityValue[] AllFacilityTypesResultsSummary
        {
            get;
            set;
        }

        [DataMember]
        public HseSchoolsFacilityTypeSummary Schools { get; set; }

        [DataMember]
        public HseSchoolsFacilityTypeSummary Childcare { get; set; }

    }
}