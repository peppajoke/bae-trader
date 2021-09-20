using System.Collections.Generic;

namespace bae_trader.Configuration
{
    public class CryptoConfig
    {
        public string BinanceAPIKey {get; set; }
        public string BinanceSecret {get; set; }

        public IEnumerable<string> AutotradeCoins { get; set; }

        public int BuyPercentThreshold { get;set; }
        public int SellPercentThreshold { get;set; }
    }
}