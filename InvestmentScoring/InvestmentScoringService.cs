using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Alpaca.Markets;

namespace bae_trader.InvestmentScoring
{
    public class InvestmentScoringService
    {

        private readonly IEnumerable<IInvestmentScoringMethod> scoringMethods;
        public InvestmentScoringService()
        {
            scoringMethods = new List<IInvestmentScoringMethod>() { new DailyPriceFluctation(), new BarTradeVolumeJump()};
        }
        public async Task<decimal> ScoreInvestment(ISnapshot investment)
        {
            decimal score = 0;

            foreach (var method in scoringMethods)
            {
                score += await method.ScoreInvestment(investment) * Convert.ToDecimal(method.ConfidenceFactor());
            }

            return score;
        } 
    }
}