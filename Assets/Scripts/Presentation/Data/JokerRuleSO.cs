using UnityEngine;
using Shakki.Core;

[CreateAssetMenu(fileName = "JokerRule", menuName = "Shakki/Rules/Joker")]
public class JokerRuleSO : MoveRuleSO
{
    [Tooltip("Tyyppi jota käytetään jos viimeistä siirtoa ei ole (esim. pelin alussa).")]
    public string fallbackType = "King";

    public override IMoveRule Build() => new JokerRule(fallbackType);
}