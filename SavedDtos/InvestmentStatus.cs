using System.Collections.Generic;

namespace bae_trader.SavedDtos
{
    public class InvestmentStatus
    {
        public bool IsPaper { get; set; }
        List<InvestmentAccount> _accounts { get; set; }
    }

    class InvestmentAccount
    {
        public AccountType Type { get; set; }

        public decimal Cash { get; set; }
        public List<Investment> Investments;

    }

    enum AccountType
    {
        Stocks, Crypto
    }

    class Investment
    {
        public string Name { get; set; }
        public decimal TotalSpent { get; set; }
        public decimal TotalWithdrawn { get; set; }
        public decimal CurrentValue { get; set; }
        public decimal ToUsdFactor { get; set; }
        public decimal HeldQuantity { get; set; }
    }
}