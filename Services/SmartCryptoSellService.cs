using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace bae_trader.Services
{

    interface ICryptoSellService
    {
        Task<decimal> GetPercentSell(string symbol);

        void ConsiderBuying(string symbol);

        void ResetSymbol(string symbol);
    }
    public class SmartCryptoSellService : ICryptoSellService
    {
        private ConcurrentDictionary<string, int> _symbolPoints = new ConcurrentDictionary<string, int>();
        public async Task<decimal> GetPercentSell(string symbol)
        {
            if (!_symbolPoints.ContainsKey(symbol))
            {
                return .1M;
            }

            return Math.Min(_symbolPoints[symbol], 20) / 20M;

        }

        public void ConsiderBuying(string symbol)
        {
            if (!_symbolPoints.ContainsKey(symbol))
            {
                _symbolPoints[symbol] = 1;
            }
            _symbolPoints[symbol]++;
        }

        public void ResetSymbol(string symbol)
        {
            _symbolPoints[symbol] = 1;
        }
    }
}