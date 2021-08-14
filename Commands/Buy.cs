using System.Collections.Generic;
using System.Threading.Tasks;
using LineCommander;

namespace BaeTrader.Commands
{
    public class Buy : BaseCommand
    {
        public override string Description()
        {
            throw new System.NotImplementedException();
        }

        private const int MAX_INVESTMENTS = 10;
        private const double MIN_PRICE_IN_DOLLARS = .5;


        public override async Task<bool> Execute(IEnumerable<string> arguments)
        {
            // check args for market
            // check args for budget
            // load market
            // sort by price

            return true;
        }

        public override IEnumerable<string> MatchingBaseCommands()
        {
            return new List<string>() { "buy" };
        }
    }
}