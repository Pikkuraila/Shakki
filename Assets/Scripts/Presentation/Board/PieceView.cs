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
    [SerializeField] float colliderPadding = 0.25f; // lisää osuma-aluetta
    [SerializeField] bool colliderIsTrigger = true; // dragissa yleensä paras

    TextMeshPro _tmp3D;
    TextMeshProUGUI _tmpUI;

    void Awake()
    {
        _tmp3D = GetComponentInChildren<TextMeshPro>(includeInactive: true);
        _tmpUI = GetComponentInChildren<TextMeshProUGUI>(includeInactive: true);

        EnsureCollider2D(); // <-- tärkeä
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // Päivitä myös edit-tilassa, näkyy prefabissa heti
        EnsureCollider2D();
    }
#endif

    public void Init(int x, int y, string owner, string typeLabel, Color color)
    {
        X = x; Y = y; Owner = owner; TypeLabel = typeLabel;
        var sr = GetComponent<SpriteRenderer>();
        if (sr) sr.color = Color.white; // <-- tärkeä: aina valkoinen tint

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

        // Jos sprite vaihtui Initissä (esim. promotion), päivitä collideri
        EnsureCollider2D();
    }

    public void SetBoardPos(int x, int y)
    {
        X = x; Y = y;
        transform.position = new Vector3(x, y, -1f);
        name = $"Piece_{TypeLabel}_{Owner}_{x}_{y}";
    }

    public void RefreshColliderForSprite()
    {
        EnsureCollider2D(); // kutsuu jo olemassa olevaa privaattia metodia
    }

    // --- UUSI: varmistaa että BoxCollider2D on olemassa ja oikein mitoitettu ---
    void EnsureCollider2D()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (!sr || !sr.sprite) return;

        var col = GetComponent<BoxCollider2D>();
        if (!col) col = gameObject.AddComponent<BoxCollider2D>();

        // Spriten rajat paikallisyksiköissä: bounds.size on jo "local"
        // (kerrotaan skaalalla vasta maailmaan). Tämä on mitä haluamme BoxCollider2D.size:en.
        var spriteLocalSize = sr.sprite.bounds.size;

        // Lisää pieni marginaali X- ja Y-suunnassa
        Vector2 size = new Vector2(
            Mathf.Max(0.0001f, spriteLocalSize.x + colliderPadding),
            Mathf.Max(0.0001f, spriteLocalSize.y + colliderPadding)
        );

        col.size = size;
        col.offset = Vector2.zero;
        col.isTrigger = colliderIsTrigger;
    }
}
