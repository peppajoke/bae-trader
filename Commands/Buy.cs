using System.Collections.Generic;
using System.Threading.Tasks;
using LineCommander;
using Alpaca.Markets;
using bae_trader.Configuration;
using System;
using bae_trader.Data;
using MoreLinq;
using System.Linq;
using bae_trader.InvestmentScoring;

namespace bae_trader.Commands
{
    public class Buy : BaseCommand
    {

        private HashSet<string> SymbolsOnCooldown = new HashSet<string>();
        private readonly InvestmentScoringService _scoringService;
        public Buy(AlpacaEnvironment environment, BuyConfig config, InvestmentScoringService scoringService = null)
        {
            _environment = environment;
            _config = config;
            _scoringService = scoringService ?? new InvestmentScoringService();
        }
        public override string Description()
        {
            return "Buys stocks. Arguments -b=100 budget $100, -real trade stocks in the real world, not in a paper environment, -m=20 make a maximum of 20 distinct investments";
        }

        private AlpacaEnvironment _environment = new AlpacaEnvironment();
        private BuyConfig _config;

        private int DollarsToInvest;

        public override async Task<bool> Execute(IEnumerable<string> arguments)
        {
            var account = await _environment.alpacaTradingClient.GetAccountAsync();
            DollarsToInvest = Math.Min(_config.BuyBudgetDollarsPerRun, (int)account.TradableCash);
            if (DollarsToInvest < 1)
            {
                return true;
            }
            var allSymbols = Nasdaq.AllSymbols.Where(x => x.All(Char.IsLetterOrDigit)); // cleaning weird nasdaq values
            
            var snapshotsBySymbol = new Dictionary<string, ISnapshot>();

            // Console.WriteLine("Buying stocks with the following settings.");
            // Console.WriteLine("Budget: $" + DollarsToInvest);
            // Console.WriteLine("Maximum total investment count: " + _config.MaximumInvestmentCountPerRun);

            // get all market data
            foreach (var symbols in allSymbols.Batch(500))
            {
                var newSnapshots = await _environment.alpacaDataClient.GetSnapshotsAsync(symbols);
                newSnapshots.ToList().ForEach(x => snapshotsBySymbol.Add(x.Key, x.Value));
            }

            var viableSymbolsForPurchase = new Dictionary<string, ISnapshot>();

            // figure out what min/max dollar values should be for the investment
            var maxPricePerShare = (decimal)Math.Sqrt(Math.Sqrt((double)DollarsToInvest))*10;

            var minPricePerShare = Math.Max(maxPricePerShare / 2, 5);

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
                    Console.WriteLine("failed to process " + snapshotBySymbol.Key + ": " + ex.Message);
                }
            }

            var sortedSymbols = viableSymbolsForPurchase.Values.OrderBy(x => x.Quote.BidPrice).ToList();

            SubmitBuyOrders(await PickWinners(sortedSymbols), DollarsToInvest);
            return true;
        }

        private bool IsOnCooldown(string symbol)
        {
            if (!SymbolsOnCooldown.Contains(symbol))
            {
                SymbolsOnCooldown.Add(symbol);
                return false;
            }

            if (new Random().Next(1,50) == 1)
            {
                SymbolsOnCooldown.Remove(symbol);
            }
            return true;
        }

        private async Task<List<ISnapshot>> PickWinners(List<ISnapshot> candidates)
        {
            var scoredInvestments = new List<InvestmentCandidate>();

            foreach (var candidate in candidates)
            {
                var score = await _scoringService.ScoreInvestment(candidate);
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

        private async void SubmitBuyOrders(List<ISnapshot> investments, int budget)
        {
            var budgetPerSymbol = budget / investments.Count();

            var orderRequests = new List<NewOrderRequest>();
            
            // todo add a cooldown for symbol buying
            foreach (var investment in investments)
            {
                if (IsOnCooldown(investment.Symbol))
                {
                    continue;
                }
                var quantity = (int)(Decimal.Round(budgetPerSymbol / investment.Quote.BidPrice, 0) - 1);
                var newOrderRequest = new NewOrderRequest(
                    investment.Symbol,
                    quantity,
                    OrderSide.Buy,
                    OrderType.Market,
                    TimeInForce.Ioc
                );
                Console.WriteLine("Buying " + investment.Symbol + "x" + quantity + " @ $"+ investment.Quote.BidPrice);
                try
                {
                    var order = await _environment.alpacaTradingClient.PostOrderAsync(newOrderRequest);
                }
                catch(Exception ex)
                {
                    Console.WriteLine("failed to buy stonks.");
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