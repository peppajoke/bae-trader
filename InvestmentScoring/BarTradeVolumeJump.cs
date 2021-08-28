using System;
using System.Runtime.Caching;
using System.Threading.Tasks;
using Alpaca.Markets;
using bae_trader.Configuration;

namespace bae_trader.InvestmentScoring
{
    public class BarTradeVolumeJump : IInvestmentScoringMethod
    {

        private static MemoryCache _cache = new MemoryCache("barvolumejump");
        public int ConfidenceFactor()
        {
            return 1;
        }

        public async Task<decimal> ScoreInvestment(ISnapshot investment, AlpacaEnvironment environment)
        {
            try
            {
                if (_cache.Contains(GetCacheKey(investment.Symbol)))
                {
                    return Convert.ToDecimal(_cache.Get(GetCacheKey(investment.Symbol)));
                }

                if (DateTime.Now.DayOfWeek == DayOfWeek.Monday)
                {
                    return 0;
                }
                var request = new HistoricalBarsRequest(investment.Symbol, DateTime.Today.AddDays(-1), DateTime.Today, BarTimeFrame.Day);
                var bars = await environment.alpacaDataClient.ListHistoricalBarsAsync(request);
                var output = Convert.ToDecimal(bars.Items[1].Volume) / Convert.ToDecimal(bars.Items[0].Volume);
                var cacheItem = new CacheItem(investment.Symbol, output);
                _cache.Add(cacheItem, 
                    new CacheItemPolicy
                    {  
                        AbsoluteExpiration = DateTime.Now.AddDays(1)
                    }
                );

                return output;
            }
            catch(DivideByZeroException ex)
            {
                return 0;
            }
            catch(NullReferenceException ex)
            {
                return 0;
            }
        }

        private string GetCacheKey(string symbol)
        {
            return DateTime.Today.DayOfYear + ":" + symbol;
        }
    }
}