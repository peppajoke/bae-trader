using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using bae_trader.Configuration;
using Coinbase;
using Coinbase.Models;
using LineCommander;

namespace bae_trader.Commands
{
    public class Crypto : BaseCommand
    {
        private readonly CryptoConfig _config;
        private readonly CoinbaseClient _client;
        public Crypto(CryptoConfig config)
        {
            _config = config;
            _client = new CoinbaseClient(new ApiKeyConfig{ ApiKey = config.CoinbaseApiKey, ApiSecret = config.CoinbaseSecret});
        }
        public override string Description()
        {
            throw new System.NotImplementedException();
        }

        public override async Task<bool> Execute(IEnumerable<string> arguments)
        {
            var accounts = await _client.Accounts.ListAccountsAsync();

            var usdc = 0M;
            var cryptos = new Dictionary<string, decimal>();

            foreach (var account in accounts.Data)
            {
                if (account.Balance.Currency == "USDC")
                {
                    usdc = account.Balance.Amount;
                }
                else
                {
                    if (!cryptos.ContainsKey(account.Balance.Currency))
                    {
                        cryptos.Add(account.Balance.Currency, 0);
                    }
                    cryptos[account.Balance.Currency] += account.Balance.Amount;
                }
            }

            // load all crypto exchange rates
            var usdFactors = new Dictionary<string, decimal>();
            var prices = await _client.Data.GetExchangeRatesAsync();
            foreach (var rate in prices.Data.Rates)
            {
                usdFactors.Add(rate.Key, rate.Value);
            }

            

            return false;
        }

        public override IEnumerable<string> MatchingBaseCommands()
        {
            return new List<string> () { "crypto"};
        }
    }
}