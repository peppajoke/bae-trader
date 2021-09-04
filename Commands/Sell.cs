using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Alpaca.Markets;
using bae_trader.Configuration;
using LineCommander;

namespace bae_trader.Commands
{
    public class Sell : BaseCommand
    {
        public Sell(AlpacaEnvironment environment, SellConfig sellConfig)
        {
            _environment = environment;
            _sellConfig = sellConfig;
        }
        private AlpacaEnvironment _environment;
        private SellConfig _sellConfig;

        private HashSet<string> SymbolsOnCooldown = new HashSet<string>();

        public override string Description()
        {
            throw new System.NotImplementedException();
        }

        public override async Task<bool> Execute(IEnumerable<string> arguments)
        {
            Console.WriteLine("Selling...");
            var positions = await _environment.alpacaTradingClient.ListPositionsAsync();
            
            if (_sellConfig.ApprovedSymbolsForSale.Any())
            {   
                positions = positions.Where(x => _sellConfig.ApprovedSymbolsForSale.Contains(x.Symbol)).ToList();
            }
            foreach (var position in positions)
            {
                MakeSaleDecision(position);
            }
            // get all stonks we have currently
            // calculate % return for selling them
            // make a call for selling them
            return true;
        }

        private async void MakeSaleDecision(IPosition currentPosition)
        {
            var profitPercent = (((currentPosition.AssetCurrentPrice * currentPosition.IntegerQuantity) / currentPosition.CostBasis) * 100) - 100;
            // Console.WriteLine("price: " + currentPosition.AssetCurrentPrice * currentPosition.IntegerQuantity);
            // Console.WriteLine("cost: " + currentPosition.CostBasis);
            if (profitPercent <= 0)
            {
                // this sale would be a loss, so don't do it.
                // Console.WriteLine(currentPosition.Symbol + " isn't worth selling. (no profit) " + profitPercent);
                return;
            }

            var percentIncrease = profitPercent * 100 * 2;

            // Console.WriteLine("Analyzing position " + currentPosition.Symbol + "...");
            // Console.WriteLine("Change percent: " + profitPercent);
            // Console.WriteLine("Sale thresh: " + _sellConfig.ProfitThresholdPercent);

            if (currentPosition.IntegerQuantity > 0 && profitPercent > (Convert.ToDecimal(_sellConfig.ProfitThresholdPercent)/2))
            {
                var newOrderRequest = new NewOrderRequest(
                    currentPosition.Symbol,
                    currentPosition.IntegerQuantity,
                    OrderSide.Sell,
                    OrderType.Market,
                    TimeInForce.Day
                );

                // SELL STONKS!!
                try
                {
                    if (IsOnCooldown(currentPosition.Symbol))
                    {
                        return;
                    }

                    // todo: add a sale wait to hold out for a better deal

                    var saleMessage = "Attempting to sell " + currentPosition.Symbol + "x" + currentPosition.IntegerQuantity + " (" + Math.Round(profitPercent, 3) + "% profit)...";
                    
                    var order = await _environment.alpacaTradingClient.PostOrderAsync(
                        newOrderRequest
                    );

                    saleMessage += order.OrderStatus == OrderStatus.Accepted ? "Accepted!" : "Failed!";
                    Console.WriteLine(saleMessage);
                }
                catch(RestClientErrorException ex)
                {
                    Console.WriteLine("Error when try to sell " + currentPosition.Symbol);
                    Console.WriteLine(ex.Message);
                }
                
            }
        }

        public override IEnumerable<string> MatchingBaseCommands()
        {
            return new List<string> () { "sell" };
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
    }
}