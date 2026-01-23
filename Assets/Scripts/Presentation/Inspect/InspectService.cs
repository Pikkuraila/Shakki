using System;
using UnityEngine;

namespace Shakki.Presentation.Inspect
{
    public static class InspectService
    {
        public static event Action<InspectData> Changed;

        public static InspectData Current { get; private set; }

        public static void Select(InspectData data)
        {
            Current = data;
            try { Changed?.Invoke(Current); }
            catch (Exception ex) { Debug.LogException(ex); }
        }
    }
}
