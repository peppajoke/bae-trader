using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using bae_trader.Configuration;
using bae_trader.SavedDtos;
using BinanceClient;
using BinanceClient.Crypto;
using BinanceClient.Enums;
using LineCommander;

namespace bae_trader.Commands
{
    public class Crypto : BaseCommand
    {
        private readonly CryptoConfig _config;

        private readonly Client _client;

        private HashSet<string> _liquidatedCurrencies = new HashSet<string>();

        public Crypto(CryptoConfig config)
        {
            _config = config;
            var wallet = Wallet.RestoreWalletFromMnemonic(_config.WalletPassPhrase, Network.Mainnet);
            _client = new Client(wallet);
        }
        public override string Description()
        {
            throw new System.NotImplementedException();
        }
        public override async Task<bool> Execute(IEnumerable<string> arguments)
        {
            var response = _client.NewOrder("ETH", OrderType.Limit, Side.Sell, (decimal)499.999, (decimal)0.00001, TimeInForce.GTE);
            return false;
        }

        public override IEnumerable<string> MatchingBaseCommands()
        {
            return new List<string> () { "crypto" };
        }
    }
}