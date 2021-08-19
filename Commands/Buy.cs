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
            if (_config.BuyBudgetDollarsPerRun < 1)
            {
                Console.WriteLine("No budget to buy stocks with!");
                return true;
            }
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
            foreach (var symbols in allSymbols.Batch(500))
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
                        //Console.WriteLine(snapshotBySymbol.Key + ": " + snapshotBySymbol.Value.Quote.BidPrice);
                    }
                }
                catch(Exception ex)
                {
                    Console.WriteLine("failed to process " + snapshotBySymbol.Key);
                }
            }

            Console.WriteLine("Done!");

            var sortedSymbols = viableSymbolsForPurchase.Values.OrderBy(x => x.Quote.BidPrice).ToList();

            SubmitBuyOrders(PickWinners(sortedSymbols), _config.BuyBudgetDollarsPerRun);
            return true;
        }

        private List<ISnapshot> PickWinners(List<ISnapshot> candidates)
        {

            var scoredInvestments = new List<InvestmentCandidate>();

            foreach (var candidate in candidates)
            {
                var score = Score(candidate);
                if (score < .2M)
                {
                    continue;
                }
                scoredInvestments.Add(new InvestmentCandidate() { Score = score, Snapshot = candidate});
            }

            var orderedInvestments = scoredInvestments.OrderBy(x => x.Score).ToList();

            var winners = new List<ISnapshot>();
           
            for(var i=0; i < _config.MaxBuyWinners; i++)
            {
                winners.Add(orderedInvestments[i].Snapshot);
            }
            
            return winners;
        }

        private decimal Score(ISnapshot candidateInvestment)
        {
            // Value is above low
            // Bid price is MUCH lower than High
            var score = (candidateInvestment.CurrentDailyBar.High - candidateInvestment.Quote.BidPrice) / candidateInvestment.Quote.BidPrice;

            if (candidateInvestment.Quote.BidPrice > candidateInvestment.CurrentDailyBar.Low)
            {
                // if we're not at the floor, double the score. You know the old rhyme.
                score *= 2;
            }

            return score;
        }

        private async void SubmitBuyOrders(List<ISnapshot> investments, int budget)
        {
            var budgetPerSymbol = budget / investments.Count();

            var orderRequests = new List<NewOrderRequest>();

            Console.WriteLine("Sending buy orders...");
            
            // todo add a cooldown for symbol buying
            foreach (var investment in investments)
            {
                var quantity = (int)(Decimal.Round(budgetPerSymbol / investment.Quote.BidPrice, 0) - 1);
                var newOrderRequest = new NewOrderRequest(
                    investment.Symbol,
                    quantity,
                    OrderSide.Buy,
                    OrderType.Market,
                    TimeInForce.Ioc
                );
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

    struct InvestmentCandidate
    {
        public ISnapshot Snapshot;
        public decimal Score;
    }
}