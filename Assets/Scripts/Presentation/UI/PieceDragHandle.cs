using UnityEngine;

[RequireComponent(typeof(PieceView))]
[RequireComponent(typeof(Collider2D))]
public sealed class PieceDragHandle : MonoBehaviour
{
    private PieceView _pv;
    private DragController _drag;

    void Awake()
    {
        _pv = GetComponent<PieceView>();
        _drag = FindObjectOfType<DragController>();
    }

    void OnMouseDown()
    {
        _drag?.BeginDrag(_pv);
    }

    void OnMouseUp() { /* tyhjä – controller lopettaa Update:ssa */ }
}
