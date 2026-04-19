using UnityEngine;
using TMPro;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class PieceView : MonoBehaviour
{
    public int X;
    public int Y;
    public string Owner;
    public string TypeLabel;

    public string TypeName => TypeLabel;

    [Header("Collider")]
    [SerializeField] float colliderPadding = 0.01f;
    [SerializeField] bool colliderIsTrigger = true;

    TextMeshPro _tmp3D;
    TextMeshProUGUI _tmpUI;
    TextMesh _injuredBadge;

    void Awake()
    {
        _tmp3D = GetComponentInChildren<TextMeshPro>(includeInactive: true);
        _tmpUI = GetComponentInChildren<TextMeshProUGUI>(includeInactive: true);

        EnsureCollider2D();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        EnsureCollider2D();
        if (_injuredBadge != null)
            PositionInjuredBadge();
    }
#endif

    public void Init(int x, int y, string owner, string typeLabel, Color color)
    {
        X = x;
        Y = y;
        Owner = owner;
        TypeLabel = typeLabel;

        var sr = GetComponent<SpriteRenderer>();
        if (sr)
            sr.color = Color.white;

        string ch = string.IsNullOrEmpty(typeLabel) ? "?" : typeLabel.Substring(0, 1).ToUpper();

        if (_tmp3D != null)
        {
            _tmp3D.text = ch;
            _tmp3D.alignment = TextAlignmentOptions.Center;
        }

        if (_tmpUI != null)
        {
            _tmpUI.text = ch;
            _tmpUI.alignment = TextAlignmentOptions.Center;
            var canvas = _tmpUI.GetComponentInParent<Canvas>();
            if (canvas && canvas.renderMode != RenderMode.WorldSpace)
                canvas.renderMode = RenderMode.WorldSpace;
        }

        name = $"Piece_{typeLabel}_{owner}_{x}_{y}";
        SetBoardPos(x, y);
        EnsureCollider2D();

        if (_injuredBadge != null)
            PositionInjuredBadge();
    }

    public void SetBoardPos(int x, int y)
    {
        X = x;
        Y = y;
        transform.position = new Vector3(x, y, -1f);
        name = $"Piece_{TypeLabel}_{Owner}_{x}_{y}";
    }

    public void RefreshColliderForSprite()
    {
        EnsureCollider2D();
        if (_injuredBadge != null)
            PositionInjuredBadge();
    }

    public void SetInjuredIndicator(bool injured)
    {
        if (!injured)
        {
            if (_injuredBadge != null)
                _injuredBadge.gameObject.SetActive(false);
            return;
        }

        EnsureInjuredBadge();
        PositionInjuredBadge();
        _injuredBadge.gameObject.SetActive(true);
    }

    void EnsureCollider2D()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (!sr || !sr.sprite)
            return;

        var col = GetComponent<BoxCollider2D>();
        if (!col)
            col = gameObject.AddComponent<BoxCollider2D>();

        var spriteLocalSize = sr.sprite.bounds.size;
        Vector2 size = new Vector2(
            Mathf.Max(0.0001f, spriteLocalSize.x + colliderPadding),
            Mathf.Max(0.0001f, spriteLocalSize.y + colliderPadding)
        );

        col.size = size;
        col.offset = Vector2.zero;
        col.isTrigger = colliderIsTrigger;
    }

    void EnsureInjuredBadge()
    {
        if (_injuredBadge == null)
        {
            var badgeGO = new GameObject("WoundedBadge");
            badgeGO.transform.SetParent(transform, false);

            _injuredBadge = badgeGO.AddComponent<TextMesh>();
            _injuredBadge.text = "!";
            _injuredBadge.anchor = TextAnchor.MiddleCenter;
            _injuredBadge.alignment = TextAlignment.Center;
            _injuredBadge.fontSize = 64;
            _injuredBadge.fontStyle = FontStyle.Bold;
            _injuredBadge.characterSize = 0.08f;
            _injuredBadge.color = new Color(1f, 0.45f, 0.1f, 1f);

            var badgeRenderer = badgeGO.GetComponent<MeshRenderer>();
            var sr = GetComponent<SpriteRenderer>();
            if (badgeRenderer != null && sr != null)
            {
                badgeRenderer.sortingLayerID = sr.sortingLayerID;
                badgeRenderer.sortingOrder = sr.sortingOrder + 5;
            }
        }
    }

    void PositionInjuredBadge()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr == null || _injuredBadge == null)
            return;

        Vector3 extents = sr.sprite != null ? sr.sprite.bounds.extents : Vector3.one * 0.4f;
        _injuredBadge.transform.localPosition = new Vector3(extents.x * 0.72f, extents.y * 0.72f, -0.2f);
        _injuredBadge.transform.localScale = Vector3.one;
    }
}
