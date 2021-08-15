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
            

            return true;
        }

        public override IEnumerable<string> MatchingBaseCommands()
        {
            return new List<string> () { "sell" };
        }
    }
}