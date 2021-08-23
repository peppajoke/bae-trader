using System.Threading.Tasks;
using Alpaca.Markets;

namespace bae_trader.InvestmentScoring
{
    public class DailyPriceFluctation : IInvestmentScoringMethod
    {
        public int ConfidenceFactor()
        {
            return 10;
        }

        public async Task<decimal> ScoreInvestment(ISnapshot investment)
        {
            // Value is above low
            // Bid price is MUCH lower than High
            var score = (investment.CurrentDailyBar.High - investment.Quote.BidPrice) / investment.Quote.BidPrice;

            if (investment.Quote.BidPrice > investment.CurrentDailyBar.Low)
            {
                // if we're not at the floor, double the score. You know the old rhyme.
                score *= 2;
            }

            return score;
        }
    }
}