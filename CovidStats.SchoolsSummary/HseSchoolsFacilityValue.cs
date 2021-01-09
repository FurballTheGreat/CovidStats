using System;
using System.Runtime.Serialization;

namespace CovidStats.SchoolsSummary
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

        public override string ToString() => $"{Name}: NoFacilities={NoFacilities}, NoTested={NoTested}, NoDetected={NoDetected}, DetectedPercent={DetectedPercent}%, NoNotDetected={NoNotDetected}";

        public void Add(HseSchoolsFacilityValue pValue)
        {
            NoFacilities += pValue.NoFacilities;
            NoTested += pValue.NoTested;
            NoDetected += pValue.NoDetected;
           
            NoNotDetected += pValue.NoNotDetected;
        }

        protected bool Equals(HseSchoolsFacilityValue pOther) => 
            NoFacilities == pOther.NoFacilities && 
            NoTested == pOther.NoTested && 
            NoDetected == pOther.NoDetected && 
            DetectedPercent == pOther.DetectedPercent && 
            NoNotDetected == pOther.NoNotDetected;

        public override bool Equals(object pObj)
        {
            if (ReferenceEquals(null, pObj)) return false;
            if (ReferenceEquals(this, pObj)) return true;
            if (pObj.GetType() != this.GetType()) return false;
            return Equals((HseSchoolsFacilityValue) pObj);
        }

        public override int GetHashCode() => 
            HashCode.Combine(NoFacilities, NoTested, NoDetected, DetectedPercent, NoNotDetected);
    }
}