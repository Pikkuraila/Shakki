using System;
using System.Collections.Generic;

namespace Shakki.Meta.Bestiary
{
    [Serializable]
    public sealed class BestiaryEntrySave
    {
        public string archetypeId;
        public int seen;                  // counts individuals seen (spawn/reveal)
        public int kills;
        public BestiaryUnlock unlocks;
    }

    [Serializable]
    public sealed class BestiarySaveData
    {
        public List<BestiaryEntrySave> entries = new();
    }
}
