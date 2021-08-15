using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using bae_trader.Commands;
using LineCommander;

namespace bae_trader
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var commands = new List<BaseCommand>() { new Buy(), new Sell(), new AutoInvest()};
            var commander = new Commander();
            await commander.AddCommands(commands);
            Console.WriteLine("I'm ready to go, what shall we do?");

            if (args.Length > 0)
            {
                var startingCommand = args[0].Replace("\"", "");
                Console.WriteLine("Autostarting with command: " + startingCommand);
                await commander.SendCommandInput(startingCommand);
            }

            await commander.ListenForCommands();
        }
    }
}
