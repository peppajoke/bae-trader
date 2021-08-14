using System;
using System.Collections.Generic;
using LineCommander;

namespace bae_trader
{
    class Program
    {
        static void Main(string[] args)
        {
            var commands = new List<BaseCommand>() { };
            var commander = new Commander();
            commander.AddCommands(commands);
            commander.ListenForCommands();
        }
    }
}
