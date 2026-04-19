using System;

namespace Shakki.Core
{
    public static class IdentityTagExtensions
    {
        public static bool HasTag(this IdentityTag tags, IdentityTag flag)
        {
            return (tags & flag) != 0;
        }
    }
}