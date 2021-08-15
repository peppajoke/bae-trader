using System.Collections.Generic;
using System.Threading.Tasks;
using LineCommander;
using Alpaca.Markets;
using bae_trader.Configuration;
using System;
using bae_trader.Data;
using MoreLinq;
using System.Linq;

namespace bae_trader.Commands
{
    public class Buy : BaseCommand
    {
        public Buy(AlpacaEnvironment environment, BuyConfig config)
        {
            _environment = environment;
            _config = config;
        }
        public override string Description()
        {
            return "Buys stocks. Arguments -b=100 budget $100, -real trade stocks in the real world, not in a paper environment, -m=20 make a maximum of 20 distinct investments";
        }

        private AlpacaEnvironment _environment = new AlpacaEnvironment();
        private BuyConfig _config;

        public override async Task<bool> Execute(IEnumerable<string> arguments)
        {
            var chaosMode = _config.AddRandomVarianceInPurchaseDecisions;

            foreach(var arg in arguments)
            {
                if (arg.Contains("-b="))
                {
                    _config.BuyBudgetDollarsPerRun = Int32.Parse(arg.Split("=")[1]);
                }
                else if (arg.Contains("-c"))
                {
                    chaosMode = true;
                }

            }

            var allSymbols = Nasdaq.AllSymbols.Where(x => x.All(Char.IsLetterOrDigit)); // cleaning weird nasdaq values
            
            var snapshotsBySymbol = new Dictionary<string, ISnapshot>();

            Console.WriteLine("Buying stocks with the following settings.");
            Console.WriteLine("Budget: $" + _config.BuyBudgetDollarsPerRun);
            Console.WriteLine("Maximum total investment count: " + _config.MaximumInvestmentCountPerRun);
            if (chaosMode)
            {
                Console.WriteLine("Chaos mode is enabled. Random price variance will occur in your investment strategy.");
            }

            // get all market data
            foreach (var symbols in allSymbols.Batch(1000))
            {
                var newSnapshots = await _environment.alpacaDataClient.GetSnapshotsAsync(symbols);
                newSnapshots.ToList().ForEach(x => snapshotsBySymbol.Add(x.Key, x.Value));
            }

            var viableSymbolsForPurchase = new Dictionary<string, ISnapshot>();

            // figure out what min/max dollar values should be for the investment
            var maxPricePerShare = (decimal)Math.Sqrt(Math.Sqrt((double)_config.BuyBudgetDollarsPerRun));

            if (chaosMode)
            {
                maxPricePerShare *= new Random().Next(2, 30);
                maxPricePerShare /= 10;
            }

            var minPricePerShare = maxPricePerShare / 2;

            // Remove bad fits
            foreach (var snapshotBySymbol in snapshotsBySymbol) 
            {
                try
                {
                    if (snapshotBySymbol.Value.Quote.BidPrice >= minPricePerShare 
                    && snapshotBySymbol.Value.Quote.BidPrice <= maxPricePerShare)
                    {
                        viableSymbolsForPurchase.Add(snapshotBySymbol.Key, snapshotBySymbol.Value);
                        Console.WriteLine(snapshotBySymbol.Key + ": " + snapshotBySymbol.Value.Quote.BidPrice);
                    }
                }
                catch(Exception ex)
                {
                    Console.WriteLine("failed to process " + snapshotBySymbol.Key);
                }
            }

            var sortedSymbols = viableSymbolsForPurchase.Values.OrderBy(x => x.Quote.BidPrice).ToList();
            SubmitBuyOrders(sortedSymbols, _config.BuyBudgetDollarsPerRun);
            return true;
        }

        private async void SubmitBuyOrders(List<ISnapshot> snapshots, int budget)
        {
            // using max investments, take top N snapshots
            var investments = snapshots.Take(_config.MaximumInvestmentCountPerRun);

            var budgetPerSymbol = budget / investments.Count();

            var orderRequests = new List<NewOrderRequest>();

            Console.WriteLine("Sending buy orders...");
            
            foreach (var investment in investments)
            {
                var quantity = (int)(Decimal.Round(budgetPerSymbol / investment.Quote.BidPrice, 0) - 1);
                var newOrderRequest = new NewOrderRequest(
                    investment.Symbol,
                    quantity,
                    OrderSide.Buy,
                    OrderType.Limit,
                    TimeInForce.Ioc
                );
                // Only buy if you can get a 10% off or better deal
                newOrderRequest.LimitPrice = investment.Quote.BidPrice * .9M;
                Console.WriteLine(investment.Symbol + "x" + quantity + " price: $"+ investment.Quote.BidPrice);
                try
                {
                    var order = await _environment.alpacaTradingClient.PostOrderAsync(newOrderRequest);
                    Console.WriteLine(order.OrderStatus);
                }
                catch(Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }

        public override IEnumerable<string> MatchingBaseCommands()
        {
            return new List<string>() { "buy" };
        }
    }
}