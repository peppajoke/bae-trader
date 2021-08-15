﻿using System;
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
            var commands = new List<BaseCommand>() { new Buy() };
            var commander = new Commander();
            await commander.AddCommands(commands);
            Console.WriteLine("I'm ready to go, what shall we do?");
            await commander.ListenForCommands();
        }
    }
}
