using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using bae_trader.Configuration;
using bae_trader.SavedDtos;
using Coinbase;
using Coinbase.Models;
using LineCommander;

namespace bae_trader.Commands
{
    public class Crypto : BaseCommand
    {
        private readonly CryptoConfig _config;
        private readonly CoinbaseClient _client;

        private PaymentMethod _cashPaymentMethod;
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

            var paymentMethods = await _client.PaymentMethods.ListPaymentMethodsAsync();
            _cashPaymentMethod = paymentMethods.Data.First(x => x.Type == "fiat_account");

            var usdc = 0M;
            var cryptos = new Dictionary<string, decimal>();

            foreach (var account in accounts.Data)
            {
                Console.WriteLine(account.Name);
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

                    if (account.Currency.Code == "DOGE")
                    {
                        // BuyCrypto(account, 1M, cashAccount);
                    }
                }
            }

            // load all crypto exchange rates
            var usdFactors = new Dictionary<string, decimal>();
            var prices = await _client.Data.GetExchangeRatesAsync();
            foreach (var rate in prices.Data.Rates)
            {
                // Console.WriteLine(rate.Value);
                usdFactors.Add(rate.Key, rate.Value);
            }

            ScanPricesForSale(usdFactors, accounts.Data);
            await Task.Delay(1800000);
            await Execute(arguments);
            return false;
        }

        private async void ScanPricesForSale(Dictionary<string, decimal> exchangeRates, IEnumerable<Account> accounts)
        {
            Console.WriteLine("scanning for sales...");
            var holdings = CryptoPurchase.LoadAllFromDisk();
            // combine currencies, develop held rate per currency

            var dictionary = new Dictionary<string, decimal>();
            
            var currenciesHeld = holdings.Select(x => x.Currency).Distinct();

            foreach (var currency in currenciesHeld)
            {
                var matchingHoldings = holdings.Where(x => x.Currency == currency);
                var totalQuantity = 0M;
                var totalCost = 0M;

                foreach (var holding in matchingHoldings)
                {
                    totalQuantity += holding.Quantity;
                    totalCost += holding.TotalCost;
                }
                var investedRate = totalQuantity/totalCost;
                // rateDelta == 1 means you break even
                var rateDelta = investedRate/exchangeRates[currency];
                Console.WriteLine("Analyzing " + currency);
                Console.WriteLine("investedRate: " + investedRate);
                Console.WriteLine("exchangeRates[currency]: " + exchangeRates[currency]);
                Console.WriteLine("rate delta: " + rateDelta);

                var percentProfit = (rateDelta - 1) * 100;

                if (percentProfit >= _config.ProfitThresholdPercent)
                {
                    Console.WriteLine("OK I WANNA SELL ALL OUR " + currency);
                    var account = accounts.First(x => x.Currency.Code == currency);
                    var placeSell = new PlaceSell() { Currency = currency, Amount = totalQuantity, Commit = true, PaymentMethod = _cashPaymentMethod.Id};
                    var response = await _client.Sells.PlaceSellOrderAsync(account.Id,  placeSell);
                    var th = "";
                }
            }

        }

        private async void BuyCrypto(Account account, decimal quantity, PaymentMethod payment)
        {
            var placeBuy = new PlaceBuy() { Currency = account.Currency.Code, PaymentMethod = payment.Id, Quote = true, Amount = quantity};

            // buy the crypto
            var response = await _client.Buys.PlaceBuyOrderAsync(account.Id, placeBuy);

            if (!response.HasError())
            {
                var purchase = new CryptoPurchase(response.Data);
                //purchase.WriteToFile();
            }

        }

        public override IEnumerable<string> MatchingBaseCommands()
        {
            return new List<string> () { "crypto" };
        }
    }
}