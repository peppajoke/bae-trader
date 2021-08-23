using System.Threading.Tasks;
using Alpaca.Markets;

namespace bae_trader.InvestmentScoring
{
    public interface IInvestmentScoringMethod
    {
         Task<decimal> ScoreInvestment(ISnapshot investment);
         int ConfidenceFactor();

        // Ideas for new buy algos
        // News info? Worth it? web mentions?
        // 5 year jump
        // 1 year jump
        // 1 month jump
        // 1 week jump
    }
}