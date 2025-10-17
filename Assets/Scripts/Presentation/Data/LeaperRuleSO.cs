using System.Linq;
using UnityEngine;
using Shakki.Core;

[CreateAssetMenu(fileName = "LeaperRule", menuName = "Shakki/Rules/Leaper")]
public class LeaperRuleSO : MoveRuleSO
{
    [Tooltip("Hyppy-offsetit ruuduissa. Esim. Dabbaba: (±2,0),(0,±2)")]
    public Vector2Int[] offsets;

    [Header("Rajoitteet (valinnaiset)")]
    public bool captureOnly = false;     // vain syˆv‰t hypyt
    public bool nonCaptureOnly = false;  // vain tyhj‰‰n ruutuun

    public override IMoveRule Build()
    {
        if (offsets == null || offsets.Length == 0)
            throw new System.InvalidOperationException($"{name}: LeaperRuleSO.offsets on tyhj‰.");

        if (captureOnly && nonCaptureOnly)
            throw new System.InvalidOperationException($"{name}: captureOnly ja nonCaptureOnly eiv‰t voi olla molemmat true.");

        // (Valinnainen) poista duplikaatit
        var arr = offsets.Distinct().Select(v => (v.x, v.y)).ToArray();

        return new LeaperRule(arr, captureOnly, nonCaptureOnly);
    }

#if UNITY_EDITOR
    // Pieni laatuparannus: normalisoi heti Inspector-muutosten yhteydess‰
    private void OnValidate()
    {
        if (offsets != null)
            offsets = offsets.Distinct().ToArray();

        if (captureOnly && nonCaptureOnly)
            nonCaptureOnly = false; // tai p‰‰t‰ kumpi voittaa
    }
#endif
}
