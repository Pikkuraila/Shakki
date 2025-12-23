using System;
using UnityEngine;

public sealed class DialogueController : MonoBehaviour
{
    [SerializeField] private DialogueView view;

    public PlayerService playerService;
    public RunController runController;

    private DialoguePackSO _currentPack;
    private Action _onFinished;

    private void Awake()
    {
        if (playerService == null) playerService = FindObjectOfType<PlayerService>();
        if (runController == null) runController = FindObjectOfType<RunController>();
    }

    public void StartDialogue(DialoguePackSO pack, string npcDisplayName = "NPC", Action onFinished = null)
    {
        _currentPack = pack;
        _onFinished = onFinished;
        view.Show(npcDisplayName, pack, this, OnChoicePicked);
    }

    private void OnChoicePicked(int index)
    {
        Debug.Log($"[Dialogue] Picked choice {index} (pack={_currentPack?.name})");


        if (_currentPack == null) { Close(); return; }
        if (index < 0 || index >= _currentPack.choices.Count) { Close(); return; }

        var action = _currentPack.choices[index].action;
        Execute(action);

        Close();
    }

    private void Close()
    {
        view.Hide();
        _currentPack = null;

        var cb = _onFinished;
        _onFinished = null;
        cb?.Invoke();
    }

    private void Execute(DialogueAction a)
    {

        Debug.Log($"[Dialogue] Execute action type={a.type} int={a.intParam} str={a.stringParam} playerService={(playerService != null)} coinsBefore={(playerService != null ? playerService.Data.coins : -999)}");


        switch (a.type)
        {
            case DialogueActionType.None:
            case DialogueActionType.CloseDialogue:
                return;

            case DialogueActionType.GiveCoins:
                playerService?.AddCoins(a.intParam);
                return;


            case DialogueActionType.StartShop:
                runController?.TriggerShopFromDialogue();
                return;

            case DialogueActionType.StartBattle:
                runController?.TriggerBattleFromDialogue();
                return;

            case DialogueActionType.ModifyNpcAlignment:
                // a.stringParam = npcId
                playerService?.AddAlignment(a.stringParam, a.intParam);
                return;

            case DialogueActionType.SetFlag:
                // a.stringParam = flagId, a.intParam = 1/0
                playerService?.SetFlag(a.stringParam, a.intParam != 0);
                return;
        }
    }

    public bool AreConditionsMet(DialoguePackSO pack, DialogueChoice choice)
    {
        if (playerService == null) playerService = FindObjectOfType<PlayerService>();
        if (choice == null || choice.conditions == null || choice.conditions.Count == 0) return true;

        foreach (var c in choice.conditions)
        {
            if (!IsConditionMet(pack, c))
                return false;
        }
        return true;
    }

    private bool IsConditionMet(DialoguePackSO pack, DialogueCondition c)
    {
        switch (c.type)
        {
            case ConditionType.None:
                return true;

            case ConditionType.AlignmentAtLeast:
                {
                    var npcId = string.IsNullOrEmpty(c.stringParam) ? pack.npcId : c.stringParam;
                    int a = playerService != null ? playerService.GetAlignment(npcId) : 0;
                    return a >= c.intParam;
                }

            case ConditionType.HasFlag:
                return playerService != null && playerService.HasFlag(c.stringParam);

            case ConditionType.NotFlag:
                return playerService == null || !playerService.HasFlag(c.stringParam);

            default:
                return true;
        }
    }

}
