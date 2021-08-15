using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;
using LineCommander;
using System;
using bae_trader.Configuration;

namespace bae_trader.Commands
{
    public class AutoInvest : BaseCommand
    {

        private Buy buyer;
        private Sell seller;

        private AlpacaEnvironment _environment = new AlpacaEnvironment();
        public AutoInvest()
        {
            buyer = new Buy(_environment);
            seller = new Sell(_environment);
        }
        public override string Description()
        {
            throw new System.NotImplementedException();
        }

        public override async Task<bool> Execute(IEnumerable<string> arguments)
        {
             // default to a paper environment for safety
            _environment.SetEnvironment(true);

            Console.WriteLine("Bae-trader: I am awake.");
            foreach(var arg in arguments)
            {
                if (arg == "-real")
                {
                    // Trade in the real world.
                    _environment.SetEnvironment(false);

                    Console.WriteLine("Bae-trader: Autoinvesting in the REAL MARKET...");
                }
            }
            // todo: Polling frequency as an arg
            while(true)
            {
                var clock = await _environment.alpacaTradingClient.GetClockAsync();
                while (!clock.IsOpen)
                {
                    Console.WriteLine("Bae-trader: The market is not open right now.");
                    var timeUntilMarketOpen = clock.NextOpenUtc - clock.TimestampUtc;
                    Console.WriteLine("Bae-trader: Going to sleep until the market opens. (" + Math.Round(timeUntilMarketOpen.TotalHours, 1) + " hours)");

                    Console.WriteLine("Bae-trader: The market opens at " + clock.NextOpenUtc.Date.AddMinutes(30).AddHours(9) + " EST");
                    await Task.Delay(Convert.ToInt32(timeUntilMarketOpen.TotalMilliseconds));
                }
                Console.WriteLine("The market is open, let's get to work...");

                // Sell
                await seller.Execute(arguments);

                Console.WriteLine("Buying stonks...");
                
                // todo, figure out if we can/should await this buy call
                buyer.Execute(arguments);
                Console.WriteLine("Sleeping for 5 minutes...");
                Thread.Sleep(300000);
                clock = await _environment.alpacaTradingClient.GetClockAsync();
            }
        }

        public override IEnumerable<string> MatchingBaseCommands()
        {
            return new List<string> () { "auto", "autoinvest" };
        }
    }
}