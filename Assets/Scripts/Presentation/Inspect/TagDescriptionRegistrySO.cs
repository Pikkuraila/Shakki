// Assets/Scripts/Presentation/Inspect/TagDescriptionRegistrySO.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using Shakki.Core; // ✅ TÄMÄ KORJAA PieceTag-näkyvyyden jos enum on Core-namespace:ssa

namespace Shakki.Presentation.Inspect
{
    [CreateAssetMenu(menuName = "Shakki/Inspect/PieceTag Registry", fileName = "PieceTagRegistry")]
    public sealed class TagDescriptionRegistrySO : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public PieceTag tag;
            public string title;
            [TextArea(2, 6)] public string description;
            public Sprite icon;
            public bool showAsChip = true;
            public bool showInInfo = true;
        }

        public List<Entry> entries = new();

        private Dictionary<PieceTag, Entry> _map;

        void OnEnable() => Rebuild();
        void OnValidate() => Rebuild();

        void Rebuild()
        {
            _map = new Dictionary<PieceTag, Entry>();
            foreach (var e in entries)
            {
                if (e == null || e.tag == PieceTag.None) continue;
                _map[e.tag] = e;
            }
        }

        public bool TryGet(PieceTag tag, out Entry entry)
        {
            entry = null;                 // ✅ pakollinen CS0177 fix
            if (_map == null) Rebuild();
            if (_map == null) return false;
            return _map.TryGetValue(tag, out entry) && entry != null;
        }

    }
}
