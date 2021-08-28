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

        private AlpacaEnvironment _environment;
        public AutoInvest(AlpacaEnvironment environment, Buy buy, Sell sell)
        {
            _environment = environment;
            buyer = buy;
            seller = sell;
        }
        public override string Description()
        {
            throw new System.NotImplementedException();
        }

        public override async Task<bool> Execute(IEnumerable<string> arguments)
        {

            Console.WriteLine("Bae-trader: I am awake.");
         
            // todo: Polling frequency as an arg
            while(true)
            {
                try
                {
                    var clock = await _environment.alpacaTradingClient.GetClockAsync();
                    while (!clock.IsOpen)
                    {
                        Console.WriteLine("The market is not open right now.");
                        var timeUntilMarketOpen = clock.NextOpenUtc - clock.TimestampUtc;
                        Console.WriteLine("Going to sleep until the market opens. (" + Math.Round(timeUntilMarketOpen.TotalHours, 1) + " hours)");
                        
                        Console.WriteLine("The market opens at " + clock.NextOpenUtc.AddHours(-4) + " Eastern standard time");
                        await Task.Delay((int)timeUntilMarketOpen.TotalHours * 3600000);

                        clock = await _environment.alpacaTradingClient.GetClockAsync();
                    }
                    Console.WriteLine("The market is open, let's get to work...");

                    // Sell
                    await seller.Execute(arguments);
                    
                    // todo, figure out if we can/should await this buy call
                    await buyer.Execute(arguments);
                    await Task.Delay(60000);
                    clock = await _environment.alpacaTradingClient.GetClockAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    Console.WriteLine(ex.StackTrace);
                    await Task.Delay(60000);
                }
            }
        }

        public override IEnumerable<string> MatchingBaseCommands()
        {
            return new List<string> () { "auto", "autoinvest" };
        }
    }
}