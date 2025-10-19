using System.Collections.Generic;

namespace Shakki.Core
{
    [System.Flags]
    public enum PieceTag
    {
        None = 0,
        EnPassant = 1 << 0,
        // Voit lisätä tänne muitakin jos haluat:
        // Flying     = 1 << 1,
        // Boss       = 1 << 2,
        // Healer     = 1 << 3,
    }

    public sealed class Piece
    {
        public string Owner { get; }
        public string TypeName { get; }              // esim. "Rook", "Empress"
        public IReadOnlyList<IMoveRule> Rules { get; }
        public bool HasMoved { get; set; }

        public PieceTag Tags { get; private set; }   // <– uusi kenttä tageille

        public Piece(string owner, string typeName, IReadOnlyList<IMoveRule> rules, PieceTag tags = PieceTag.None)
        {
            Owner = owner;
            TypeName = typeName;
            Rules = rules;
            HasMoved = false;
            Tags = tags;
        }

        // Kätevä apumetodi: tarkistaa onko tagi asetettu
        public bool HasTag(PieceTag tag) => (Tags & tag) != 0;

        // Halutessasi voit lisätä dynaamisen muokkauksen (jos pelissä on power-uppeja)
        public void AddTag(PieceTag tag) => Tags |= tag;
        public void RemoveTag(PieceTag tag) => Tags &= ~tag;
    }
}
