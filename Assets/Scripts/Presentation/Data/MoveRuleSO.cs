using UnityEngine;
using Shakki.Core;

public abstract class MoveRuleSO : ScriptableObject
{
    [Header("Inspect / mechanics tags")]
    [Tooltip("Mekaniikka/ability-tagit joita tämä sääntö 'antaa'. Näitä käytetään mm. Inspect-paneelissa (computed tags).")]
    [SerializeField, EnumFlags] private PieceTag providedTags = PieceTag.None;
    public virtual PieceTag ProvidedTags => providedTags;

    public abstract IMoveRule Build();
}
