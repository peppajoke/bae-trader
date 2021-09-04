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
        Stocks = 1, Crypto = 2
    }

    class Investment
    {
        public AccountType Type { get; set; }
        public string Name { get; set; }
        public decimal TotalBoughtUsd { get; set; }
        public decimal TotalSoldUsd { get; set; }
        public decimal ToUsdFactor { get; set; }
        public decimal HeldQuantity { get; set; }

        public override string ToString() 
        { 
            return "";
        }


    }
}