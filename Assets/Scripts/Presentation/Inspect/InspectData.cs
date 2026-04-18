using System;
using UnityEngine;
using Shakki.Meta.Bestiary;

namespace Shakki.Presentation.Inspect
{
    [Serializable]
    public sealed class InspectData
    {
        public string id;
        public string title;
        public Sprite portrait;
        public string[] tags;
        public string lore;
        public string[] infoLines;

        public bool hasBoardCoord;
        public int boardX;
        public int boardY;

        public MoveRevealMode moveRevealMode = MoveRevealMode.AlwaysLegal;
    }
}
