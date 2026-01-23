using UnityEngine;
using Shakki.Presentation.Inspect;

[RequireComponent(typeof(PieceView))]
[RequireComponent(typeof(Collider2D))]
public sealed class PieceDragHandle : MonoBehaviour
{
    // ✅ Back-compat: muu koodi odottaa tätä (PieceViewExtensions tms)
    public string pieceId;

    private PieceView _pv;
    private DragController _drag;

    [Header("Click vs Drag")]
    [SerializeField] private float dragThresholdPixels = 6f;

    private bool _pressed;
    private bool _dragStarted;
    private Vector3 _downScreenPos;

    void Awake()
    {
        _pv = GetComponent<PieceView>();
        _drag = FindObjectOfType<DragController>(true);

        // ✅ pidetään pieceId aina järkevänä
        pieceId = _pv != null ? _pv.TypeName : pieceId;
    }

    void OnMouseDown()
    {
        Debug.Log($"[PieceDragHandle] DOWN {name}");
        if (_pv != null) pieceId = _pv.TypeName;

        _pressed = true;
        _dragStarted = false;
        _downScreenPos = Input.mousePosition;
    }

    void Update()
    {
        if (!_pressed) return;
        if (!Input.GetMouseButton(0)) return;

        var delta = Input.mousePosition - _downScreenPos;
        if (!_dragStarted && delta.sqrMagnitude >= (dragThresholdPixels * dragThresholdPixels))
        {
            _dragStarted = true;
            _drag?.BeginDrag(_pv);
        }
    }

    void OnMouseUp()
    {
        Debug.Log($"[PieceDragHandle] UP {name} dragStarted={_dragStarted}");
        if (!_pressed) return;
        _pressed = false;

        // Jos drag ei alkanut -> click -> inspect
        if (!_dragStarted)
        {
            if (_pv != null) pieceId = _pv.TypeName;

            var data = new InspectData
            {
                id = pieceId,
                title = pieceId,
                portrait = null,
                tags = null,
                lore = "",
                hasBoardCoord = true,
                boardX = _pv != null ? _pv.X : 0,
                boardY = _pv != null ? _pv.Y : 0
            };

            InspectService.Select(data);
        }
    }
}
