using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Alpaca.Markets;
using bae_trader.Configuration;
using LineCommander;

namespace bae_trader.Commands
{
    public class Sell : BaseCommand
    {
        public Sell(AlpacaEnvironment environment)
        {
            _environment = environment;
        }
        private AlpacaEnvironment _environment;

        public override string Description()
        {
            throw new System.NotImplementedException();
        }

        public override async Task<bool> Execute(IEnumerable<string> arguments)
        {
            var positions = await _environment.alpacaTradingClient.ListPositionsAsync();
            
            Console.WriteLine("Current positions...");
            foreach (var position in positions)
            {
                Console.WriteLine(position.ToString());
                MakeSaleDecision(position);
            }
            // get all stonks we have currently
            // calculate % return for selling them
            // make a call for selling them
            return true;
        }

        private void MakeSaleDecision(IPosition currentPosition)
        {
            if (currentPosition.AssetChangePercent <= 0)
            {
                // this sale would be a loss, so don't do it.
                return;
            }

            var percentIncrease = currentPosition.AssetChangePercent * 100;

            // don't sell more than half your assets
            var percentToSell = Math.Min(50, percentIncrease);

            var unitsToSell = Decimal.ToInt64(currentPosition.IntegerQuantity * (percentToSell*100));

            if (unitsToSell > 0 && percentIncrease > 5)
            {
                var newOrderRequest = new NewOrderRequest(
                    currentPosition.Symbol,
                    unitsToSell,
                    OrderSide.Buy,
                    OrderType.Market,
                    TimeInForce.Ioc
                );

                newOrderRequest.LimitPrice = currentPosition.MarketValue;

                // SELL STONKS!!
                _environment.alpacaTradingClient.PostOrderAsync(
                    newOrderRequest
                );
            }
        }

        public override IEnumerable<string> MatchingBaseCommands()
        {
            return new List<string> () { "sell" };
        }
    }
}