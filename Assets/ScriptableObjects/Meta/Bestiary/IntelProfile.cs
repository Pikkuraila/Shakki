namespace Shakki.Meta.Bestiary
{
    public enum MoveRevealMode
    {
        None = 0,
        HoverPseudo = 10,
        AlwaysPseudo = 20,
        HoverLegal = 30,
        AlwaysLegal = 40
    }

    public struct IntelProfile
    {
        public bool showName;
        public bool showTraits;
        public MoveRevealMode moveReveal;

        public static IntelProfile Default => new IntelProfile
        {
            showName = false,
            showTraits = false,
            moveReveal = MoveRevealMode.None
        };

        public void UpgradeMoveReveal(MoveRevealMode candidate)
        {
            if ((int)candidate > (int)moveReveal)
                moveReveal = candidate;
        }
    }
}
