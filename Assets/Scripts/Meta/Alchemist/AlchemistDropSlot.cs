using UnityEngine;
using UnityEngine.EventSystems;

public enum AlchemistSlotKind { InputA, InputB, Output }

public sealed class AlchemistDropSlot : MonoBehaviour, IDropHandler
{
    public AlchemistSlotKind kind;
    public AlchemistEncounterView view;

    public void OnDrop(PointerEventData eventData)
    {
        var drag = eventData.pointerDrag?.GetComponent<UIDraggablePiece>();
        if (drag == null) return;

        if (view == null)
        {
            Debug.LogWarning("[AlchemistDropSlot] view missing");
            return;
        }

        view.HandleDrop(kind, drag);
    }
}
