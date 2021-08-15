using System.Collections.Generic;

namespace bae_trader.Configuration
{
    public class BuyConfig
    {
        public int MaximumInvestmentCountPerRun;
        public int BuyBudgetDollarsPerRun;
        public IEnumerable<string> ApprovedSymbolsForPurchase;
        public bool AddRandomVarianceInPurchaseDecisions;
    }
}