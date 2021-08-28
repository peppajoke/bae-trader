using System;
using System.Threading.Tasks;
using Alpaca.Markets;
using bae_trader.Configuration;

namespace bae_trader.InvestmentScoring
{
    public class DailyPriceFluctation : IInvestmentScoringMethod
    {
        public int ConfidenceFactor()
        {
            return 10;
        }

        public async Task<decimal> ScoreInvestment(ISnapshot investment, AlpacaEnvironment environment)
        {
            if (DateTime.Now.TimeOfDay.TotalHours < 10)
            {
                // not enough time passed for daily price fluctation to matter yet.
                return 0;
            }
            try
            {
                var score = (investment.CurrentDailyBar.High - investment.Quote.BidPrice) / investment.Quote.BidPrice;

                if (investment.Quote.BidPrice > investment.CurrentDailyBar.Low)
                {
                    // if we're not at the floor, double the score. You know the old rhyme.
                    score *= 2;
                }
                return score;
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