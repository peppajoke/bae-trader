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

        private AlpacaEnvironment _environment;

        private bool UsePaperEnvironment = true; // flip this to paper when you don't want REAL trades

        public override string Description()
        {
            throw new System.NotImplementedException();
        }

        public override async Task<bool> Execute(IEnumerable<string> arguments)
        {
            var usePaperEnvironment = true;

            // default to a paper environment for safety
            _environment.SetEnvironment(true);

            foreach(var arg in arguments)
            {
                if (arg == "-real")
                {
                    // Trade in the real world.
                    _environment.SetEnvironment(false);

                    usePaperEnvironment = false;
                }

            }

            var positions = await _environment.alpacaTradingClient.ListPositionsAsync();
            
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