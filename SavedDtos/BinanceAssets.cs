using System.Collections.Generic;

namespace bae_trader.SavedDtos
{
    public class BinanceAssets
    {
        public decimal Cash { get;set; }
        public Dictionary<string,decimal> Coins { get; set; }
    }
}