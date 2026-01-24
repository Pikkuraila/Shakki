using System.Linq;
using UnityEngine;
using Shakki.Core;

[CreateAssetMenu(fileName = "PieceDef", menuName = "Shakki/Piece")]
public class PieceDefSO : ScriptableObject
{
    [Header("Identity (Internal)")]
    [Tooltip("Tekninen ID. Ei koskaan näytetä pelaajalle.")]
    public string typeName = "Rook";

    [Header("Presentation / Lore (UI)")]
    [Tooltip("Pelaajalle näytettävä nimi (inspect, shop). Jos tyhjä, fallback typeNameen.")]
    public string displayName;

    [TextArea(3, 8)]
    [Tooltip("Lore / kuvaus inspect-paneeliin.")]
    public string loreText;

    [Header("Economy")]
    [Min(0)] public int cost = 1;

    [Header("Rules")]
    public MoveRuleSO[] rules;

    [Header("Identity Tags (for gating & lore)")]
    [Tooltip("Rotu/ontologia -tagit (Living/Undead/Construct/Amalgam...). Käytetään esim. alkemistin gateihin ja inspectiin.")]
    public IdentityTag identityTags = IdentityTag.None;

    [Header("Visuals (Board)")]
    public Sprite whiteSprite;
    public Sprite blackSprite;

    public GameObject viewPrefabOverride;

    [Header("Inspect (Portrait)")]
    [Tooltip("Erillinen portrait-kuva inspect-paneeliin. Jos tyhjä, fallbackaa whiteSpriteen.")]
    public Sprite portraitSprite;

    // --- Build helpers ---
    public Piece Build(string owner)
    {
        var built = rules?
            .Where(r => r != null)
            .Select(r => r.Build())
            .ToList()
            ?? new System.Collections.Generic.List<IMoveRule>();

        // ✅ PieceTag = mekaniikka/ability (EnPassant tms). EI identity.
        var computed = GetComputedTags();

        return new Piece(owner, typeName, built, computed);
    }

    public Sprite GetSpriteFor(string owner)
        => owner == "white" ? whiteSprite : blackSprite;

    public Sprite GetPortrait()
        => portraitSprite != null ? portraitSprite : whiteSprite;

    public string GetDisplayName()
        => string.IsNullOrWhiteSpace(displayName) ? typeName : displayName;

    public string GetLore()
        => loreText ?? "";

    /// <summary>
    /// Mekaniikka/ability-tagit sääntöjen perusteella (PieceTag).
    /// IdentityTag on erikseen identityTags-kentässä.
    /// </summary>
    public PieceTag GetComputedTags()
    {
        PieceTag acc = PieceTag.None;
        if (rules == null) return acc;

        for (int i = 0; i < rules.Length; i++)
        {
            var r = rules[i];
            if (r == null) continue;
            acc |= r.ProvidedTags;
        }

        return acc;
    }

    public bool HasIdentity(IdentityTag tag)
        => (identityTags & tag) != 0;
}
