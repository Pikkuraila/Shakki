using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Shakki/Meta/Macro Event Icons", fileName = "MacroEventIcons")]
public sealed class MacroEventIconsSO : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public MacroEventType type;
        public Sprite icon;
    }

    [Header("Mapping: type -> icon")]
    public List<Entry> entries = new();

    private Dictionary<MacroEventType, Sprite> _map;

    public Sprite GetIconOrNull(MacroEventType type)
    {
        if (_map == null)
        {
            _map = new Dictionary<MacroEventType, Sprite>();
            foreach (var e in entries)
            {
                // viimeinen voittaa jos duplikaatteja
                _map[e.type] = e.icon;
            }
        }

        return _map.TryGetValue(type, out var s) ? s : null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Reset cache editorissa kun listaa muokataan
        _map = null;
    }
#endif
}
