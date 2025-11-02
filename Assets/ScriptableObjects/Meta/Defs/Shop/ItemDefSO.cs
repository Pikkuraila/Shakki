using UnityEngine;

[CreateAssetMenu(fileName = "ItemDef", menuName = "Shakki/Meta/Item Def")]
public class ItemDefSO : ScriptableObject
{
    [Header("Identity")]
    public string id;                   // esim. "IT_Bomb" tai "IT_ExtraMove"

    [Header("Presentation")]
    public string displayName;
    [TextArea] public string description;
    public Sprite icon;

    [Header("Gameplay (optional)")]
    public bool isPassive = true;       // true = passiivinen buffi, false = aktivoitava
    public float effectValue = 0f;      // esim. +1 move range, +10% damage, tms.
}
