using System;
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
            try
            {
                var volumeIncreaseSinceYesterday = investment.CurrentDailyBar.TradeCount - investment.PreviousDailyBar.TradeCount;
                var percentTradeGrowth = (volumeIncreaseSinceYesterday / investment.PreviousDailyBar.TradeCount) * 100;

                return percentTradeGrowth;
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
    }
}