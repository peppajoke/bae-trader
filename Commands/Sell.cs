using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Alpaca.Markets;
using LineCommander;

namespace bae_trader.Commands
{
    public class Sell : BaseCommand
    {

        private IAlpacaDataClient alpacaDataClient;

        private IAlpacaTradingClient alpacaTradingClient;

        private IAlpacaStreamingClient alpacaStreamingClient;

        private IAlpacaDataStreamingClient alpacaDataStreamingClient;

        private bool UsePaperEnvironment = true; // flip this to paper when you don't want REAL trades

        public override string Description()
        {
            throw new System.NotImplementedException();
        }

        public override async Task<bool> Execute(IEnumerable<string> arguments)
        {
            var positions = await alpacaTradingClient.ListPositionsAsync();
            
            Console.WriteLine("Current positions...");
            foreach (var position in positions)
            {
                Console.WriteLine(position.ToString());
            }
            // get all stonks we have currently
            // calculate % return for selling them
            // make a call for selling them
            return true;
        }

        public override IEnumerable<string> MatchingBaseCommands()
        {
            return new List<string> () { "sell" };
        }
    }
}