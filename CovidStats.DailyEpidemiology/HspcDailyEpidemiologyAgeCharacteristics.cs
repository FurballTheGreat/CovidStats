namespace CovidStats.DailyEpidemiology
{
    public class HspcDailyEpidemiologyAgeCharacteristics 
    {
        public int MedianAge { get; set; }
        public int MeanAge { get; set; }
        
        public HspcAbsoluteAndPercent Age0To4 { get; set; } = new HspcAbsoluteAndPercent();
        public HspcAbsoluteAndPercent Age5To12 { get; set; } = new HspcAbsoluteAndPercent();
        public HspcAbsoluteAndPercent Age13To18 { get; set; } = new HspcAbsoluteAndPercent();
        public HspcAbsoluteAndPercent Age19To24 { get; set; } = new HspcAbsoluteAndPercent();
        public HspcAbsoluteAndPercent Age25To34 { get; set; } = new HspcAbsoluteAndPercent();
        public HspcAbsoluteAndPercent Age35To44 { get; set; } = new HspcAbsoluteAndPercent();
        public HspcAbsoluteAndPercent Age45To54 { get; set; } = new HspcAbsoluteAndPercent();
        public HspcAbsoluteAndPercent Age55To64 { get; set; } = new HspcAbsoluteAndPercent();
        public HspcAbsoluteAndPercent Age65To74 { get; set; } = new HspcAbsoluteAndPercent();
        public HspcAbsoluteAndPercent Age75To84 { get; set; } = new HspcAbsoluteAndPercent();
        public HspcAbsoluteAndPercent Age85Plus { get; set; } = new HspcAbsoluteAndPercent();
        public HspcAbsoluteAndPercent AgeUnknown { get; set; } = new HspcAbsoluteAndPercent();
    }
}