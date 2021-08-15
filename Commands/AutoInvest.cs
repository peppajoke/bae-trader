using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;
using LineCommander;
using System;

namespace bae_trader.Commands
{
    public class AutoInvest : BaseCommand
    {

        private Buy buyer;
        public AutoInvest()
        {
            buyer = new Buy();
        }
        public override string Description()
        {
            throw new System.NotImplementedException();
        }

        public override async Task<bool> Execute(IEnumerable<string> arguments)
        {
            // todo: Polling frequency as an arg
            while(true)
            {
                Console.WriteLine("I am awake.");
                // Sell?
                Console.WriteLine("Buying stonks...");
                buyer.Execute(arguments);
                Console.WriteLine("Sleeping for 5 minutes...");
                Thread.Sleep(300000);
            }
        }

        public override IEnumerable<string> MatchingBaseCommands()
        {
            return new List<string> () { "auto", "autoinvest" };
        }
    }
}