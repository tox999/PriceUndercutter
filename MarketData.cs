using CsvHelper.Configuration.Attributes;

namespace PriceUndercutter
{
    class MarketData
    {
        [Name("price")]
        public double Price { get; set; }
        [Name("volRemaining")]
        public double VolRemaining { get; set; }
        [Name("typeID")]
        public ulong TypeID { get; set; }
        [Name("range")]
        public int Range { get; set; }
        [Name("orderID")]
        public ulong OrderID { get; set; }
        [Name("volEntered")]
        public int VolEntered { get; set; }
        [Name("minVolume")]
        public int MinVolume { get; set; }
        [Name("bid")]
        public bool Bid { get; set; }
        [Name("issueDate")]
        public string IssueDate { get; set; }
        [Name("duration")]
        public uint Duration { get; set; }
        [Name("stationID")]
        public ulong StationID { get; set; }
        [Name("regionID")]
        public ulong RegionID { get; set; }
        [Name("solarSystemID")]
        public ulong SolarSystemID { get; set; }
        [Name("jumps")]
        public uint Jumps { get; set; }
    }
}
