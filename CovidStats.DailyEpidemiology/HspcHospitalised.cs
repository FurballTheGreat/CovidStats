namespace CovidStats.DailyEpidemiology
{
    public class HspcHospitalised
    {
        public string Name { get; set; }
        public int NumberOfCases { get; set; }
        public int CasesHospitalised { get; set; }
        public decimal CasesHospitalisedPercent { get; set; }
        public int CasesAdmittedToIcu  { get; set; }
        public decimal CasesAdmittedToIcuPercent { get; set; }
    }
}