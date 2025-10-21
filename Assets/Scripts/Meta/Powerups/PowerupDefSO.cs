using UnityEngine;

public enum PowerupKind { SwapPiece /* laajennettavissa: PromotePawn, Teleport, ShieldKing ... */ }

[CreateAssetMenu(menuName = "Shakki/Powerup", fileName = "PowerupDef")]
public sealed class PowerupDefSO : ScriptableObject
{
    [Tooltip("Pysyvä ID. Esim. 'SwapPiece'")]
    public string id = "SwapPiece";
    public string displayName;
    [TextArea] public string description;
    public PowerupKind kind = PowerupKind.SwapPiece;

    [Header("Balance")]
    public int shopPrice = 10;     // jos ostat kaupasta
}
