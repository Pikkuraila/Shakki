using System;
using System.Collections.Generic;
using UnityEngine;

public enum DialogueActionType
{
    None,
    CloseDialogue,
    StartShop,
    StartBattle,
    GiveCoins,
    ModifyNpcAlignment,
    SetFlag
}

public enum ConditionType
{
    None,
    AlignmentAtLeast,
    HasFlag,
    NotFlag
}

[Serializable]
public struct DialogueCondition
{
    public ConditionType type;

    // AlignmentAtLeast: npcId (tyhj‰ = k‰yt‰ pack.npcId)
    // HasFlag/NotFlag: flagId
    public string stringParam;

    // AlignmentAtLeast: threshold
    public int intParam;
}


[Serializable]
public struct DialogueAction
{
    public DialogueActionType type;

    // geneeriset paramit (pidet‰‰n aluksi yksinkertaisena)
    public string stringParam;   // esim flagId, npcId
    public int intParam;         // esim coins delta, alignment delta
}

[Serializable]
public sealed class DialogueChoice
{
    [TextArea] public string text;
    public List<DialogueCondition> conditions = new(); // <-- UUSI
    public DialogueAction action;
}


[CreateAssetMenu(menuName = "Shakki/Meta/Dialogue Pack", fileName = "DialoguePack")]
public sealed class DialoguePackSO : ScriptableObject
{
    public string npcId = "hermit";

    [TextArea] public string text;

    public List<DialogueChoice> choices = new();
}
