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
        public override string Description()
        {
            throw new System.NotImplementedException();
        }

        private const int MAX_INVESTMENTS = 20;
        private const decimal SPENDING_BUDGET = 200000;

        private IAlpacaDataClient alpacaDataClient;

        private IAlpacaTradingClient alpacaTradingClient;

        private IAlpacaStreamingClient alpacaStreamingClient;

        private IAlpacaDataStreamingClient alpacaDataStreamingClient;

        private bool UsePaperEnvironment = true; // flip this to paper when you don't want REAL trades

        public Buy()
        {

            SecretKey key;
            IEnvironment env;
            if (UsePaperEnvironment)
            {
                key = new SecretKey(AlpacaCredentials.PaperClientID, AlpacaCredentials.PaperClientSecret);
                env = Environments.Paper;
            }
            else
            {
                key = new SecretKey(AlpacaCredentials.LiveClientID, AlpacaCredentials.LiveClientSecret);
                env = Environments.Live;
            }

            alpacaTradingClient = env.GetAlpacaTradingClient(key);
            alpacaDataClient = env.GetAlpacaDataClient(key);
            alpacaStreamingClient = env.GetAlpacaStreamingClient(key);
        }

        public override async Task<bool> Execute(IEnumerable<string> arguments)
        {

            var allSymbols = Nasdaq.AllSymbols.Where(x => x.All(Char.IsLetterOrDigit)); // cleaning weird nasdaq values
            
            var snapshotsBySymbol = new Dictionary<string, ISnapshot>();

            Console.WriteLine("Fetching NASDAQ prices...");

            // get all market data
            foreach (var symbols in allSymbols.Batch(1000))
            {
                var newSnapshots = await alpacaDataClient.GetSnapshotsAsync(symbols);
                newSnapshots.ToList().ForEach(x => snapshotsBySymbol.Add(x.Key, x.Value));
            }

            Console.WriteLine("Done!");

            var viableSymbolsForPurchase = new Dictionary<string, ISnapshot>();

            // figure out what min/max dollar values should be for the investment
            var maxPricePerShare = (decimal)Math.Sqrt(Math.Sqrt((double)SPENDING_BUDGET));
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
            SubmitBuyOrders(sortedSymbols);
            return true;
        }

        private async void SubmitBuyOrders(List<ISnapshot> snapshots)
        {
            // using max investments, take top N snapshots
            var investments = snapshots.Take(MAX_INVESTMENTS);

            var budgetPerSymbol = SPENDING_BUDGET / investments.Count();

            var orderRequests = new List<NewOrderRequest>();

            Console.WriteLine("Sending buy orders...");
            
            foreach (var investment in investments)
            {
                var quantity = (int)(Decimal.Round(budgetPerSymbol / investment.Quote.BidPrice, 0) - 1);
                var newOrderRequest = new NewOrderRequest(
                    investment.Symbol,
                    quantity,
                    OrderSide.Buy,
                    OrderType.Market,
                    TimeInForce.Gtc
                );
                Console.WriteLine(investment.Symbol + "x" + quantity + " price: $"+ investment.Quote.BidPrice);
                try
                {
                    var order = await alpacaTradingClient.PostOrderAsync(newOrderRequest);

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