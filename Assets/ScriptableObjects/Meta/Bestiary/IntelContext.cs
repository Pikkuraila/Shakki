namespace Shakki.Meta.Bestiary
{
    // Extend this later with run-state, difficulty, active relics, curses, etc.
    public struct IntelContext
    {
        public bool isPlayerTurn;
        public bool inCombat;

        public static IntelContext Default => new IntelContext { isPlayerTurn = true, inCombat = true };
    }
}
