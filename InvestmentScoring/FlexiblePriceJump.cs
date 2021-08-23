using Alpaca.Markets;
using System;
using System.Linq;
using System.Runtime.Caching;
using System.Threading.Tasks;

namespace bae_trader.InvestmentScoring
{
    public class FlexiblePriceJump : IInvestmentScoringMethod
    {
        private readonly IAlpacaDataClient _alpacaDataClient;
        private DateTimeInterval _lookbackInterval;
        private int _confidenceFactor;
        private static MemoryCache _cache = new MemoryCache("flexpricejump");
        public FlexiblePriceJump(IAlpacaDataClient alpacaDataClient, DateTimeInterval lookbackInterval, int confidenceFactor)
        {
            _alpacaDataClient = alpacaDataClient;
            _lookbackInterval = lookbackInterval;
            _confidenceFactor = confidenceFactor;
        }
        public int ConfidenceFactor()
        {
            return _confidenceFactor;
        }

        public async Task<decimal> ScoreInvestment(ISnapshot investment)
        {
            var cacheKey = investment.Symbol + ":" + _lookbackInterval;
            if (_cache.Contains(cacheKey))
            {
                return Convert.ToDecimal(_cache.Get(cacheKey));
            }

            var now = DateTime.Now;
            var start = GetLookback(_lookbackInterval);

            var barRequest = new HistoricalBarsRequest(investment.Symbol, start, now, BarTimeFrame.Day);

            var bars = await _alpacaDataClient.ListHistoricalBarsAsync(barRequest);
            var firstBar = bars.Items.First();
            var lastBar = bars.Items.Last();
            var delta = lastBar.Close - firstBar.Close;

            var cacheItem = new CacheItem(cacheKey, delta);
            _cache.Add(cacheItem, 
                new CacheItemPolicy
                {  
                    AbsoluteExpiration = now.AddDays(1)
                }
            );

            return delta;
        }

        private DateTime GetLookback(DateTimeInterval interval)
        {
            var now = DateTime.Now;
            switch(interval)
            {
                case DateTimeInterval.Week:
                    return now.AddDays(-1);
                case DateTimeInterval.Month:
                    return now.AddMonths(-1);
                case DateTimeInterval.Year:
                    return now.AddYears(-1);
                case DateTimeInterval.FiveYears:
                    return now.AddYears(-5);
                default:
                    throw new Exception("Not a valid interval.");
            }
        }
    }

    public enum DateTimeInterval 
    {
        Week = 1,
        Month = 2,
        Year = 3,
        FiveYears = 4
    }
}