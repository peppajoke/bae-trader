using System.Collections.Generic;
using System.Threading.Tasks;
using LineCommander;
using Alpaca.Markets;
using bae_trader.Configuration;
using System;
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

            Console.WriteLine("Buying...");
            
            var request = new AssetsRequest();
            var assets = await _environment.alpacaTradingClient.ListAssetsAsync(request);
            var allSymbols = assets.Where(x => x.IsTradable && x.Symbol != "W").Select(x => x.Symbol);
            
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
            var maxPricePerShare = DollarsToInvest / _config.MaxBuyWinners;

            var minPricePerShare = Math.Max(maxPricePerShare / 2, 5);

            if (maxPricePerShare <= minPricePerShare)
            {
                return true;
            }

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
                    //Console.WriteLine("failed to process " + snapshotBySymbol.Key + ": " + ex.Message);
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
                var score = await _scoringService.ScoreInvestment(candidate, _environment);
                if (score < .2M)
                {
                    continue;
                }
                scoredInvestments.Add(new InvestmentCandidate() { Score = score, Snapshot = candidate});
            }

            var orderedInvestments = scoredInvestments.OrderByDescending(x => x.Score).ToList();

            if (!orderedInvestments.Any()) 
            {
                return new List<ISnapshot>();
            }

            var winners = new List<ISnapshot>();
           
            for(var i=0; i < _config.MaxBuyWinners; i++)
            {
                winners.Add(orderedInvestments[i].Snapshot);
            }
            
            return winners;
        }

        private async void SubmitBuyOrders(List<ISnapshot> investments, int budget)
        {
            if (!investments.Any())
            {
                return;
            }
            var budgetPerSymbol = budget / _config.MaxBuyWinners;

            var orderRequests = new List<NewOrderRequest>();
            
            // todo add a cooldown for symbol buying
            foreach (var investment in investments)
            {
                
                var quantity = (int)(Decimal.Round(budgetPerSymbol / investment.Quote.BidPrice, 0) - 1);
                if (IsOnCooldown(investment.Symbol) || quantity < 1)
                {
                    continue;
                }
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