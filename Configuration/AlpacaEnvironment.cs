using Alpaca.Markets;

namespace bae_trader.Configuration
{
    public class AlpacaEnvironment
    {
        public IAlpacaDataClient alpacaDataClient;

        public IAlpacaTradingClient alpacaTradingClient;

        public IAlpacaStreamingClient alpacaStreamingClient;

        public IAlpacaDataStreamingClient alpacaDataStreamingClient;

        public void SetEnvironment(bool usePaperEnvironment)
        {
            SecretKey key;
            IEnvironment env;
            if (usePaperEnvironment)
            {
                key = new SecretKey(AlpacaCredentials.PaperClientID, AlpacaCredentials.PaperClientSecret);
                env = Environments.Paper;
            }
            else
            {
                key = new SecretKey(AlpacaCredentials.LiveClientID, AlpacaCredentials.LiveClientSecret);
                env = Environments.Live;
            }
            alpacaTradingClient = env.GetAlpacaTradingClient(key);
            alpacaDataClient = env.GetAlpacaDataClient(key);
            alpacaStreamingClient = env.GetAlpacaStreamingClient(key);
        }
    }
}