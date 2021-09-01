using System.Collections.Generic;
using System.Threading.Tasks;
using LineCommander;

namespace bae_trader.Commands
{
    public class Report : BaseCommand
    {
        public override string Description()
        {
            throw new System.NotImplementedException();
        }

        public override async Task<bool> Execute(IEnumerable<string> arguments)
        {
            // TODO Auto-generated method stub

            return true;
        }

        public override IEnumerable<string> MatchingBaseCommands()
        {
            return new List<string> () { "report" };
        }
    }
}