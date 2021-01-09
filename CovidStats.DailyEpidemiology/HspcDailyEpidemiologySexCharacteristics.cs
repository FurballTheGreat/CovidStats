namespace CovidStats.DailyEpidemiology
{
    public class HspcDailyEpidemiologySexCharacteristics
    {
        public decimal MaleFemaleRatio { get; set; }
        public HspcAbsoluteAndPercent Male { get; set; } = new HspcAbsoluteAndPercent();
        public HspcAbsoluteAndPercent Female { get; set; } = new HspcAbsoluteAndPercent();
        public HspcAbsoluteAndPercent Unknown { get; set; } = new HspcAbsoluteAndPercent();
    }
}