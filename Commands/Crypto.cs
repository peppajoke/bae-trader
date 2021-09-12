using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using bae_trader.Configuration;
using Binance.Net;
using Binance.Net.Objects;
using CryptoExchange.Net.Authentication;
using LineCommander;
using Binance.Net.Enums;
using System.Collections.Concurrent;
using Binance.Net.Objects.Spot.UserStream;
using bae_trader.Brains;

namespace bae_trader.Commands
{
    public class Crypto : BaseCommand
    {
        private readonly CryptoConfig _config;

        private readonly ICryptoBrain _cryptoBrain;

        public Crypto(CryptoConfig config, ICryptoBrain cryptoBrain)
        {
            _config = config;
            _cryptoBrain = cryptoBrain;

        }
        public override string Description()
        {
            throw new System.NotImplementedException();
        }
        public override async Task<bool> Execute(IEnumerable<string> arguments)
        {
            await _cryptoBrain.Trade();
            return false;
        }

        

        public override IEnumerable<string> MatchingBaseCommands()
        {
            return new List<string> () { "crypto" };
        }
        
    }
}