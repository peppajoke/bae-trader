using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using bae_trader.Commands;
using bae_trader.Configuration;
using LineCommander;
using Microsoft.Extensions.Configuration;

namespace bae_trader
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var usePaperEnvironment = true;
            IConfigurationBuilder builder;
            if (args.Length == 0)
            {
                Console.WriteLine("You must specify paper or live as the trading environment. Example: dotnet run -- paper");
            }

            switch(args[0])
            {
                case "paper":
                    builder = new ConfigurationBuilder()
                        .SetBasePath(Directory.GetCurrentDirectory())
                        .AddJsonFile("stonksettings.paper.json", optional: false);
                    break;
                case "live":
                    builder = new ConfigurationBuilder()
                        .SetBasePath(Directory.GetCurrentDirectory())
                        .AddJsonFile("stonksettings.live.json", optional: false);
                    break;
                default:
                    Console.WriteLine("First argument is invalid. Must be paper or live to specify trading environment.");
                    return;
            }

            IConfiguration config = builder.Build();

            var buyConfig = config.GetSection("Buy").Get<BuyConfig>();
            //var sellConfig = config.GetSection("Sell").Get<MyFirstClass>();
            var alpacaCredentials = config.GetSection("AlpacaCredentials").Get<AlpacaCredentials>();

            var environment = new AlpacaEnvironment();
            environment.SetEnvironment(usePaperEnvironment, alpacaCredentials);

            var buyer = new Buy(environment, new BuyConfig());
            var seller = new Sell(environment);
            var auto = new AutoInvest(environment, buyer, seller);

            var commands = new List<BaseCommand>() { buyer, seller, auto};
            var commander = new Commander();
            await commander.AddCommands(commands);
            Console.WriteLine("I'm ready to go, what shall we do?");

            if (args.Length > 1)
            {
                var startingCommand = args[1].Replace("\"", "");
                Console.WriteLine("Autostarting with command: " + startingCommand);
                await commander.SendCommandInput(startingCommand);
            }

            await commander.ListenForCommands();
        }
    }
}
