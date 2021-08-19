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

        public override string Description()
        {
            throw new System.NotImplementedException();
        }

        public override async Task<bool> Execute(IEnumerable<string> arguments)
        {
            var positions = await _environment.alpacaTradingClient.ListPositionsAsync();
            
            Console.WriteLine("Current positions...");
            if (_sellConfig.ApprovedSymbolsForSale.Any())
            {   
                positions = positions.Where(x => _sellConfig.ApprovedSymbolsForSale.Contains(x.Symbol)).ToList();
            }
            foreach (var position in positions)
            {
                Console.WriteLine(position.Symbol);
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
            Console.WriteLine("price: " + currentPosition.AssetCurrentPrice * currentPosition.IntegerQuantity);
            Console.WriteLine("cost: " + currentPosition.CostBasis);
            if (profitPercent <= 0)
            {
                // this sale would be a loss, so don't do it.

            Console.WriteLine(currentPosition.Symbol + " isn't worth selling. (no profit) " + profitPercent);
                return;
            }

            var percentIncrease = profitPercent * 100;

            Console.WriteLine("Analyzing position " + currentPosition.Symbol + "...");
            Console.WriteLine("Change percent: " + profitPercent);
            Console.WriteLine("Sale thresh: " + _sellConfig.ProfitThresholdPercent);

            // don't sell more than half your assets
            var percentToSell = Math.Max(20, Math.Min(80, percentIncrease));

            var unitsToSell = Decimal.ToInt64(currentPosition.IntegerQuantity * (percentToSell/100));

            if (unitsToSell > 0 && profitPercent > _sellConfig.ProfitThresholdPercent)
            {
                Console.WriteLine("Selling " + currentPosition.Symbol + "x" + unitsToSell);
                
                var newOrderRequest = new NewOrderRequest(
                    currentPosition.Symbol,
                    unitsToSell,
                    OrderSide.Sell,
                    OrderType.Market,
                    TimeInForce.Day
                );

                // SELL STONKS!!
                try
                {
                    var order = await _environment.alpacaTradingClient.PostOrderAsync(
                        newOrderRequest
                    );

                    Console.WriteLine(order.OrderStatus);
                }
                catch(RestClientErrorException ex)
                {
                    Console.WriteLine("Failed to sell " + currentPosition.Symbol);
                    Console.WriteLine(ex.Message);
                }
                
            }
            Console.WriteLine(currentPosition.Symbol + " isn't worth selling.");
        }

        public override IEnumerable<string> MatchingBaseCommands()
        {
            return new List<string> () { "sell" };
        }
    }
}