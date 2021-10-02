using System.Threading.Tasks;

namespace bae_trader.Brains
{
    public interface ICryptoBrain
    {
         Task Trade(bool liquidate);
    }
}