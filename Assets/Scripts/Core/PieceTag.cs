using System;

namespace Shakki.Core
{
    [System.Flags]
    public enum PieceTag
    {
        None = 0,

        // Movement archetypes
        Glider = 1 << 0,  // rook/bishop/queen (sliding)
        Jumper = 1 << 1,  // knight (leaps)
        Stepper = 1 << 2,  // king (1 step), tms

        // Special mechanics
        Shapeshifter = 1 << 3,
        Obstacle = 1 << 4,
        test2 = 1 << 5,
        test3 = 1 << 6,
    }
}
