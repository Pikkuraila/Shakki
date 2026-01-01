using System;

namespace Shakki.Meta.Bestiary
{
    [Flags]
    public enum BestiaryUnlock
    {
        None = 0,
        NameKnown = 1 << 0,
        MoveKnown = 1 << 1,

        // later:
        // SpecialKnown = 1 << 2,
        // WeaknessKnown = 1 << 3,
        // LootKnown = 1 << 4,
    }
}
