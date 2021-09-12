using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using bae_trader.Brains;
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
                return;
            }

            switch(args[0])
            {
                case "paper":
                    builder = new ConfigurationBuilder()
                        .SetBasePath(Directory.GetCurrentDirectory())
                        .AddJsonFile("stonksettings.paper.json", optional: false);
                    Console.WriteLine("Running in a paper environment.");
                    break;
                case "live":
                    builder = new ConfigurationBuilder()
                        .SetBasePath(Directory.GetCurrentDirectory())
                        .AddJsonFile("stonksettings.live.json", optional: false);
                    Console.WriteLine("Running a LIVE environment.");
                    usePaperEnvironment = false;
                    break;
                default:
                    Console.WriteLine("First argument is invalid. Must be paper or live to specify trading environment.");
                    return;
            }

            IConfiguration config = builder.Build();

            var buyConfig = config.GetSection("Buy").Get<BuyConfig>();
            var sellConfig = config.GetSection("Sell").Get<SellConfig>();
            var alpacaCredentials = config.GetSection("AlpacaCredentials").Get<AlpacaCredentials>();
            var cryptoConfig = config.GetSection("Crypto").Get<CryptoConfig>();

            if (String.IsNullOrEmpty(alpacaCredentials.ClientId))
            {
                Console.WriteLine("No client id found! Did you add one in your stonksettings json file? Exiting...");
                return;
            }

            if (String.IsNullOrEmpty(alpacaCredentials.SecretId))
            {
                Console.WriteLine("No secret id found! Did you add one in your stonksettings json file? Exiting...");
                return;
            }

            var environment = new AlpacaEnvironment();
            environment.SetEnvironment(usePaperEnvironment, alpacaCredentials);

            var buyer = new Buy(environment, buyConfig);
            var seller = new Sell(environment, sellConfig);
            var auto = new AutoInvest(environment, buyer, seller);
            var testEnv = new TestEnv(environment);
            testEnv.Execute(new List<string>());
            var play = new Play(environment);

            var commands = new List<BaseCommand>() { buyer, seller, auto, testEnv, play};

            if (!usePaperEnvironment)
            {
                var binanceBrain = new BinanceCryptoBrain(cryptoConfig);
                var crypto = new Crypto(cryptoConfig, binanceBrain);
                commands.Add(crypto);
            }

            var commander = new Commander();
            await commander.AddCommands(commands);
            Console.WriteLine("Bae-trader: What'll it be? (commands: autoinvest/auto, buy, sell)");

            if (args.Length > 1)
            {
                var remainingCommands = args.Skip(1).ToArray();
                var startingCommand = String.Join(' ', remainingCommands);
                Console.WriteLine("Autostarting with command: " + startingCommand);
                await commander.SendCommandInput(startingCommand);
            }

            await commander.ListenForCommands();
        }
    }
}
