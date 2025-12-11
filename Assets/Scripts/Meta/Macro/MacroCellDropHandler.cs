using UnityEngine;
using UnityEngine.EventSystems;

public sealed class MacroCellDropHandler : MonoBehaviour, IDropHandler
{
    private MacroCellView _cell;

    private void Awake()
    {
        // sama gameobject tai parentilla MacroCellView
        _cell = GetComponent<MacroCellView>();
        if (_cell == null)
            _cell = GetComponentInParent<MacroCellView>();
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (_cell == null)
        {
            Debug.LogError("[MacroCellDropHandler] Missing MacroCellView");
            return;
        }

        var piece = eventData.pointerDrag
            ? eventData.pointerDrag.GetComponent<UIDraggablePiece>()
            : null;

        if (piece == null)
            return; // ei raahattu nappulaa

        var board = _cell.board;
        if (board == null)
        {
            Debug.LogError("[MacroCellDropHandler] Missing board reference");
            return;
        }

        // Tämä hoitaa liikevalidoinnin + OnAdvance-callbackin → RunControlleriin
        board.HandleDropToCell(_cell.Index);
    }
}
