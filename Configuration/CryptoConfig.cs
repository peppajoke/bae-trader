using System.Collections.Generic;
using System.Linq;

namespace bae_trader.Configuration
{
    public class CryptoConfig
    {
        public string BinanceAPIKey {get; set; }
        public string BinanceSecret {get; set; }
        public IEnumerable<string> AutoBuy { get; set; }
        public IEnumerable<string> AutoSell { get; set; }

        public int BuyPercentThreshold { get;set; }
        public int SellPercentThreshold { get;set; }

        public bool AutoTradeNewCoins { get; set; }

        public IEnumerable<string> AllUsedSymbols
        {
            get { return AutoBuy.Concat(AutoSell); }
        }
    }
}