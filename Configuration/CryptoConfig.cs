namespace bae_trader.Configuration
{
    public class CryptoConfig
    {
        public string CoinbaseApiKey { get; set; }
        public string CoinbaseSecret { get; set; }
        public int ProfitThresholdPercent { get; set; }
        public int MinimumTradeUSD { get; set; }
    }
}