using System;
using UnityEngine;

namespace Shakki.Presentation.Inspect
{
    [Serializable]
    public sealed class InspectData
    {
        public string id;              // debug / lookup
        public string title;           // näkyvä nimi
        public Sprite portrait;        // inner portrait
        public string[] tags;          // string-tageja UI:lle
        public string lore;            // voi olla tyhjä
        public string[] infoLines;   // esim. "Moves like: Rook", "Leaps", "Range: 3" jne


        // Optional: jos myöhemmin halutaan selection-marker boardille
        public bool hasBoardCoord;
        public int boardX;
        public int boardY;
    }
}
