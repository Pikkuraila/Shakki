using System;

namespace Shakki.Core
{
    public sealed class Move : IEquatable<Move>
    {
        public Coord From { get; }
        public Coord To { get; }
        public string AsTypeName { get; }

        public Move(Coord from, Coord to, string asTypeName = null)
        {
            From = from;
            To = to;
            AsTypeName = asTypeName;
        }

        public override string ToString()
            => $"{From} -> {To}" + (AsTypeName != null ? $" as {AsTypeName}" : "");

        // IEquatable + override
        public bool Equals(Move other)
        {
            if (other is null) return false;
            return From.Equals(other.From)
                && To.Equals(other.To)
                && string.Equals(AsTypeName, other.AsTypeName, StringComparison.Ordinal);
        }

        public override bool Equals(object obj) => obj is Move m && Equals(m);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + From.GetHashCode();
                h = h * 31 + To.GetHashCode();
                h = h * 31 + (AsTypeName?.GetHashCode() ?? 0);
                return h;
            }
        }

        public static bool operator ==(Move a, Move b)
            => a is null ? b is null : a.Equals(b);

        public static bool operator !=(Move a, Move b) => !(a == b);
    }
}
