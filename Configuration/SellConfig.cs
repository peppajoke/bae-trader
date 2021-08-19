using System.Collections.Generic;

namespace bae_trader.Configuration
{
    public class SellConfig
    {
        public int ProfitThresholdPercent { get; set; }
        public List<string> ApprovedSymbolsForSale { get; set; } = new List<string>(); //hashset? 
    }
}