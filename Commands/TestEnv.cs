using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using bae_trader.Configuration;
using LineCommander;

namespace bae_trader.Commands
{
    public class TestEnv : BaseCommand
    {
        private AlpacaEnvironment _environment = new AlpacaEnvironment();
        
        public TestEnv(AlpacaEnvironment environment)
        {
            _environment = environment;
        }
        public override string Description()
        {
            throw new System.NotImplementedException();
        }

        public async override Task<bool> Execute(IEnumerable<string> arguments)
        {
            Console.WriteLine("Attempting to connect to Alpaca...");
            try
            {
                var account = await _environment.alpacaTradingClient.GetAccountAsync();
                Console.WriteLine("Success!");
                Console.WriteLine("Buying power: " + account.BuyingPower);
                Console.WriteLine("Tradeable cash: " + account.TradableCash);
            }
            catch(Exception ex)
            {
                Console.WriteLine("Failed to connect to Alpaca. Check your keys.");
                Console.WriteLine(ex.Message);
            }

            return true;
        }

        public override IEnumerable<string> MatchingBaseCommands()
        {
            return new List<string> () { "test" };
        }
    }
}