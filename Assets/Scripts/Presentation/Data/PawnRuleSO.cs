using UnityEngine;
using Shakki.Core;

[CreateAssetMenu(fileName = "PawnRule", menuName = "Shakki/Rules/Pawn")]
public class PawnRuleSO : MoveRuleSO
{
    // N‰m‰ voi j‰tt‰‰ n‰kyviin jos haluat, mutta niit‰ EI k‰ytet‰:
    [Tooltip("Ei k‰ytˆss‰ ñ suunta p‰‰tell‰‰n nappulan Ownerista.")]
    public int directionY = +1;

    [Tooltip("Ei k‰ytˆss‰ ñ aloitusrivi p‰‰tell‰‰n Ownerista (white=1, black=6).")]
    public int startRank = 1;

    public bool canDouble = true;

    public override IMoveRule Build()
    {
        // Owner-pohjainen s‰‰ntˆ: suunta/aloitusrivi p‰‰tell‰‰n Generate:ssa
        return new PawnRule(); // canDouble on true oletuksena; jos haluat, tee ctor PawnRule(bool canDouble)
    }
}
