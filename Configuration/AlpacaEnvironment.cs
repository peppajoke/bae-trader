using Alpaca.Markets;

namespace bae_trader.Configuration
{
    public class AlpacaEnvironment
    {
        public IAlpacaDataClient alpacaDataClient;

        public IAlpacaTradingClient alpacaTradingClient;

        public IAlpacaStreamingClient alpacaStreamingClient;

        public IAlpacaDataStreamingClient alpacaDataStreamingClient;

        public void SetEnvironment(bool usePaperEnvironment, AlpacaCredentials credentials)
        {
            var env = usePaperEnvironment ? Environments.Paper : Environments.Live;
            var key = new SecretKey(credentials.ClientId, credentials.SecretId);
            alpacaTradingClient = env.GetAlpacaTradingClient(key);
            alpacaDataClient = env.GetAlpacaDataClient(key);
            alpacaStreamingClient = env.GetAlpacaStreamingClient(key);
        }
    }
}