using System.Collections.Generic;
using System.Threading.Tasks;
using Alpaca.Markets;
using bae_trader.Configuration;
using LineCommander;

namespace bae_trader.Commands
{
    public class Play : BaseCommand
    {
        private AlpacaEnvironment _environment;

        public Play(AlpacaEnvironment environment)
        {
            _environment = environment;
        }
        public override string Description()
        {
            throw new System.NotImplementedException();
        }

        public override async Task<bool> Execute(IEnumerable<string> arguments)
        {
            var request = new AssetsRequest();
            var assets = await _environment.alpacaTradingClient.ListAssetsAsync(request);
            return true;
        }

        public override IEnumerable<string> MatchingBaseCommands()
        {
            return new List<string> () { "play"};
        }
    }
}