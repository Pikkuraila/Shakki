using UnityEngine;

[RequireComponent(typeof(PieceView))]
[RequireComponent(typeof(Collider2D))]
public sealed class PieceDragHandle : MonoBehaviour
{
    public string pieceId;   // ainoa metadata mitä muu koodi tarvitsee

    private PieceView _pv;
    private DragController _drag;

    void Awake()
    {
        _pv = GetComponent<PieceView>();
        _drag = FindObjectOfType<DragController>(true);
    }

    void OnMouseDown() => _drag?.BeginDrag(_pv);
}
