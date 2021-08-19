using System.Collections.Generic;

namespace bae_trader.Configuration
{
    public class BuyConfig
    {
        public int MaximumInvestmentCountPerRun { get; set; }
        public int BuyBudgetDollarsPerRun { get; set; }
        public IEnumerable<string> ApprovedSymbolsForPurchase { get; set; }
        public bool AddRandomVarianceInPurchaseDecisions { get; set; }

        public int MaxBuyWinners { get; set; }
    }
}