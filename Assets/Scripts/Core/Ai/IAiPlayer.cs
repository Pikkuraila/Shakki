using Shakki.Core;

namespace Shakki.Core
{
    public interface IAiPlayer
    {
        Move ChooseMove(GameState state, string color, IRulesResolver rules);
    }
}
