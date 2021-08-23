using System.Threading.Tasks;
using Alpaca.Markets;

namespace bae_trader.InvestmentScoring
{
    public class BarTradeVolumeJump : IInvestmentScoringMethod
    {
        public int ConfidenceFactor()
        {
            return 10;
        }

        public async Task<decimal> ScoreInvestment(ISnapshot investment)
        {
            var volumeIncreaseSinceYesterday = investment.CurrentDailyBar.TradeCount - investment.PreviousDailyBar.TradeCount;
            var percentTradeGrowth = (volumeIncreaseSinceYesterday / investment.PreviousDailyBar.TradeCount) * 100;

            return percentTradeGrowth;
        }
    }
}