using UnityEngine;

[CreateAssetMenu(menuName = "Shakki/Upgrade", fileName = "UpgradeDef")]
public sealed class UpgradeDefSO : ScriptableObject
{
    [Tooltip("Pysyvä ID. ÄLÄ MUUTA julkaisemisen jälkeen.")]
    public string id = "u_double_pawn_move";
    public string displayName;
    [TextArea] public string description;
    public int baseCost = 10;
    public AnimationCurve costCurve = AnimationCurve.Linear(0, 1, 10, 5); // esim. kerroin
}