using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Alpaca.Markets;
using bae_trader.Configuration;

namespace bae_trader.InvestmentScoring
{
    public class InvestmentScoringService
    {

        private readonly IEnumerable<IInvestmentScoringMethod> scoringMethods;
        
        public InvestmentScoringService()
        {
            scoringMethods = new List<IInvestmentScoringMethod>() { new DailyPriceFluctation(), new BarTradeVolumeJump()};
        }
        public async Task<decimal> ScoreInvestment(ISnapshot investment, AlpacaEnvironment environment)
        {
            decimal score = 0;

            //Console.WriteLine("Scoring " + investment.Symbol + "...");
            foreach (var method in scoringMethods)
            {
                var thisScore = await method.ScoreInvestment(investment, environment) * Convert.ToDecimal(method.ConfidenceFactor());
                thisScore = Math.Max(thisScore, 0);
                //Console.WriteLine("Method: " + method.GetType().Name);
                //Console.WriteLine("Score: " + thisScore);
                score += thisScore;
                await Task.Delay(100);
            }

            return score;
        }
    }
}