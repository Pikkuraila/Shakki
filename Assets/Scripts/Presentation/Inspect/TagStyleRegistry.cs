using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shakki.Presentation.Inspect
{
    [CreateAssetMenu(menuName = "Shakki/Inspect/Tag Style Registry", fileName = "TagStyleRegistry")]
    public sealed class TagStyleRegistrySO : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string id;          // "Living"
            public string label;       // "Living" / "Elävä" tms
            public Color background;   // pillin tausta
            public Color textColor;    // (valinnainen, jos haluat myöhemmin)
        }

        [SerializeField] private List<Entry> entries = new();

        Dictionary<string, Entry> _map;

        void OnEnable() => Rebuild();

        public void Rebuild()
        {
            _map = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in entries)
            {
                if (string.IsNullOrWhiteSpace(e.id)) continue;
                _map[e.id.Trim()] = e;
            }
        }

        public bool TryGet(string id, out Entry entry)
        {
            if (_map == null) Rebuild();

            if (string.IsNullOrWhiteSpace(id))
            {
                entry = default;
                return false;
            }

            return _map.TryGetValue(id.Trim(), out entry);
        }

        public Entry GetOrDefault(string id, Color fallbackBg)
        {
            if (TryGet(id, out var e)) return e;

            return new Entry
            {
                id = id ?? "",
                label = id ?? "",
                background = fallbackBg,
                textColor = Color.white
            };
        }
    }
}
