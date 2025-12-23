using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public sealed class DialogueView : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text npcNameText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private Button choice1Button;
    [SerializeField] private TMP_Text choice1Text;
    [SerializeField] private Button choice2Button;
    [SerializeField] private TMP_Text choice2Text;

    private Action<int> _onChoice;

    public void Show(string npcName, DialoguePackSO pack, DialogueController controller, Action<int> onChoice)
    {

        if (pack == null || pack.choices == null || pack.choices.Count < 2)
        {
            Debug.LogError("[DialogueView] Pack missing 2 choices.");
            return;
        }

        _onChoice = onChoice;

        root.SetActive(true);
        npcNameText.text = npcName;
        bodyText.text = pack.text;

        // oletetaan 2 choicea tälle viipaleelle
        var c0 = pack.choices[0];
        var c1 = pack.choices[1];

        bool ok0 = (controller == null) || controller.AreConditionsMet(pack, c0);
        bool ok1 = (controller == null) || controller.AreConditionsMet(pack, c1);

        choice1Text.text = ok0 ? c0.text : $"{c0.text}  (locked)";
        choice2Text.text = ok1 ? c1.text : $"{c1.text}  (locked)";

        choice1Button.interactable = ok0;
        choice2Button.interactable = ok1;

        choice1Button.onClick.RemoveAllListeners();
        choice2Button.onClick.RemoveAllListeners();

        // jos disabloitu, ei voi klikata -> Pick ei koskaan tule
        choice1Button.onClick.AddListener(() => Pick(0));
        choice2Button.onClick.AddListener(() => Pick(1));
    }



    public void Hide()
    {
        root.SetActive(false);
        _onChoice = null;
    }

    private void Pick(int index)
    {
        _onChoice?.Invoke(index);
    }
}
