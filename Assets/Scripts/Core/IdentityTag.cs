using System;

namespace Shakki.Core
{
    [Flags]
    public enum IdentityTag
    {
        None = 0,

        Living = 1 << 0,
        Undead = 1 << 1,
        Construct = 1 << 2,
        Amalgam = 1 << 3,

        // myöhemmin:
        Beast = 1 << 4,
        Humanoid = 1 << 5,
        Demon = 1 << 6,
    }
}
