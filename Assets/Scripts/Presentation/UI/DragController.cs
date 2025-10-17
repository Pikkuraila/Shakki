using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Shakki.Core;

public class DragController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private BoardView board;   // vedä Inspectorissa
    [SerializeField] private Camera cam;        // jätä tyhjäksi → käyttää Camera.main

    [Header("Follow & Lift")]
    [SerializeField] private float followSpeed = 25f;
    [SerializeField] private float liftZ = -2f;
    [SerializeField] private int dragSortingOrder = 200;

    [Header("FX")]
    [SerializeField] private float tiltMaxDeg = 10f;
    [SerializeField] private float tiltResponsiveness = 12f;
    [SerializeField] private float tiltBankFactor = 6f;
    [SerializeField] private float scaleWhileDrag = 1.06f;
    [SerializeField] private float squashBySpeed = 0.015f;
    [SerializeField] private float squashMax = 0.06f;
    [SerializeField] private float illegalShakeDur = 0.08f;
    [SerializeField] private float illegalShakeAmp = 0.05f;
    [SerializeField] private float snapBackDur = 0.12f;

    [Header("Snap Success")]
    [SerializeField] private float snapToDur = 0.12f;   // onnistuneen droppauksen liuku
    [SerializeField] private float landSquash = 0.06f;  // kuinka paljon “plop”
    [SerializeField] private float landRebound = 0.04f; // pieni palautus squashin jälkeen

    // State
    private PieceView _dragPV;
    private Vector3 _dragStartWorld;
    private (int x, int y) _dragStartBoard;
    private Vector3 _renderPos;
    private List<Move> _legals = new();
    private SpriteRenderer _dragSR;
    private int _prevSortingOrder;
    private bool _dropping;

    // FX state
    private Vector3 _lastPos;
    private Vector3 _defaultScale = Vector3.one;
    private float _tiltDeg;

    Camera Cam => cam != null ? cam : Camera.main;

    // === Public API (kutsutaan PieceDragHandlesta) ===
    public void BeginDrag(PieceView pv)
    {
        if (pv == null || board == null || Cam == null) return;
        if (!board.CanHumanMove(pv)) return;

        _dragPV = pv;
        _dragStartWorld = pv.transform.position;
        _dragStartBoard = (pv.X, pv.Y);
        _renderPos = _dragStartWorld;

        _legals = board.GenerateMovesFrom(_dragStartBoard).ToList();
        board.ShowHighlightsPublic(_legals);

        _dragSR = pv.GetComponent<SpriteRenderer>() ?? pv.GetComponentInChildren<SpriteRenderer>(true);
        _prevSortingOrder = _dragSR ? _dragSR.sortingOrder : 0;
        if (_dragSR) _dragSR.sortingOrder = dragSortingOrder;

        // nosta nappulaa hieman
        pv.transform.position = new Vector3(pv.transform.position.x, pv.transform.position.y, liftZ);

        // FX init
        _lastPos = pv.transform.position;
        _defaultScale = pv.transform.localScale;
        _tiltDeg = 0f;
        pv.transform.localScale = _defaultScale * scaleWhileDrag;
        pv.transform.rotation = Quaternion.identity;
    }

    void Update()
    {
        if (_dragPV == null) return;

        // jos nappi ei ole pohjassa → päätä drag
        if (!Input.GetMouseButton(0))
        {
            if (!_dropping) StartCoroutine(FinishDrag());
            return;
        }

        // seuraa hiirtä
        var target = board.MouseWorldPublic(Cam);
        target.z = liftZ;

        float k = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
        _renderPos = Vector3.Lerp(_renderPos, target, k);
        _dragPV.transform.position = _renderPos;

        // FX: kallistus + squash
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        Vector3 vel = (_renderPos - _lastPos) / dt;
        float speed = vel.magnitude;

        float targetTilt = Mathf.Clamp(vel.x * tiltBankFactor, -tiltMaxDeg, tiltMaxDeg);
        float kTilt = 1f - Mathf.Exp(-tiltResponsiveness * Time.deltaTime);
        _tiltDeg = Mathf.Lerp(_tiltDeg, targetTilt, kTilt);
        _dragPV.transform.rotation = Quaternion.Euler(0f, 0f, _tiltDeg);

        float squash = Mathf.Clamp(speed * squashBySpeed, 0f, squashMax);
        Vector3 tgtScale = new Vector3(scaleWhileDrag + squash, scaleWhileDrag - squash, 1f);
        _dragPV.transform.localScale = Vector3.Lerp(_dragPV.transform.localScale, tgtScale, kTilt);

        _lastPos = _renderPos;
    }

    private IEnumerator FinishDrag()
    {
        _dropping = true;

        if (_dragSR) _dragSR.sortingOrder = _prevSortingOrder;

        // --- 1) laske kohderuutu (smart drop) ---
        var mouseW = board.MouseWorldPublic(Cam);
        var to = board.WorldToBoardPublic(mouseW);

        float snapRadius = 0.6f;
        if (_legals != null && _legals.Count > 0)
        {
            var best = _legals
                .OrderBy(m => Vector2.SqrMagnitude(
                    new Vector2(mouseW.x, mouseW.y) - new Vector2(m.To.X, m.To.Y)))
                .First();

            float bestDist = Vector2.Distance(
                new Vector2(mouseW.x, mouseW.y),
                new Vector2(best.To.X, best.To.Y));

            if (bestDist <= snapRadius)
                to = (best.To.X, best.To.Y);
        }

        // --- 2) erikoistapaus: pelaaja päästää irti lähtöruudun päällä ---
        if (to.x == _dragStartBoard.x && to.y == _dragStartBoard.y)
        {
            // ei tärähdystä; vain sulava paluu
            yield return StartCoroutine(SnapBack(_dragPV, _dragStartWorld, snapBackDur));
        }
        else
        {
            // --- 3) normaalit drop-säännöt ---
            bool ok = board.TryDropPublic(_dragPV, _dragStartBoard, to, _legals);

#if UNITY_EDITOR
        if (!ok)
        {
            var legalList = string.Join(", ", _legals.Select(m => $"({m.To.X},{m.To.Y})"));
            Debug.Log($"[Drag] Drop rejected at {to.x},{to.y}. Legals: {legalList}");
        }
#endif

            if (!ok)
            {
                // hylkäys: pieni tärähdys ja sitten pehmeä paluu
                yield return StartCoroutine(RejectShake(_dragPV, illegalShakeDur, illegalShakeAmp));
                yield return StartCoroutine(SnapBack(_dragPV, _dragStartWorld, snapBackDur));
            }
            else
            {
                // OK siirto: BoardView on todennäköisesti jo asettanut piece:n ruutuun (snapattu keskeen).
                // Otetaan tuo “lopullinen” paikka talteen...
                var end = _dragPV.transform.position;

                // ...palautetaan hetkeksi nappula sinne missä sormi oikeasti irtosi (pehmeä liuku kohti end)
                _dragPV.transform.position = new Vector3(_renderPos.x, _renderPos.y, liftZ);

                // pidetään vielä korkea sorting orden animaation ajan, palautetaan vasta lopuksi
                if (_dragSR) _dragSR.sortingOrder = dragSortingOrder;

                yield return StartCoroutine(SnapTo(_dragPV, end, snapToDur, landSquash, landRebound));

                // nyt voidaan palauttaa sorting order
                if (_dragSR) _dragSR.sortingOrder = _prevSortingOrder;
            }
        }



        // --- 4) siivous & FX reset ---
        if (_dragPV != null)
        {
            _dragPV.transform.localScale = _defaultScale;
            _dragPV.transform.rotation = Quaternion.identity;
            _dragPV.transform.position = new Vector3(_dragPV.transform.position.x,
                                                     _dragPV.transform.position.y, -1f);
        }

        board.ClearHighlightsPublic();
        _dragPV = null;
        _legals.Clear();
        _dropping = false;
    }

    private IEnumerator SnapTo(PieceView pv, Vector3 end, float dur, float squashAmt, float reboundAmt)
    {
        if (pv == null) yield break;

        Vector3 startPos = pv.transform.position;
        Vector3 startScale = pv.transform.localScale;

        float t = 0f;

        // 1) EaseOutCubic liuku
        while (t < dur && pv != null)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dur);

            // EaseOutCubic
            float e = 1f - Mathf.Pow(1f - u, 3f);

            pv.transform.position = Vector3.Lerp(startPos, end, e);

            // kevyt “lentokulman” loiveneminen
            float tilt = Mathf.Lerp(_tiltDeg, 0f, e);
            pv.transform.rotation = Quaternion.Euler(0f, 0f, tilt);

            // kevyt matkansisäinen squash: pienenee hiukan liikkeen loppua kohti
            float midSquash = Mathf.Lerp(squashAmt * 0.6f, 0f, e);
            pv.transform.localScale = new Vector3(
                _defaultScale.x * (scaleWhileDrag + midSquash),
                _defaultScale.y * (scaleWhileDrag - midSquash),
                1f
            );

            yield return null;
        }

        if (pv == null) yield break;

        // varmistus: päätepiste ja z
        pv.transform.position = new Vector3(end.x, end.y, -1f);

        // 2) Landing “plop” (nopea squash -> rebound -> normal)
        float land = 0f;
        float landDur = 0.06f; // lyhyt ja napakka
        while (land < landDur && pv != null)
        {
            land += Time.deltaTime;
            float u = Mathf.Clamp01(land / landDur);
            // nopea sisään (ease-out)
            float e = 1f - Mathf.Pow(1f - u, 3f);

            // ensin alas (squash), sitten pieneen “yli” palautukseen
            float s = Mathf.Lerp(0f, squashAmt, e);
            pv.transform.localScale = new Vector3(
                _defaultScale.x * (1f + s),
                _defaultScale.y * (1f - s),
                1f
            );
            yield return null;
        }

        // rebound
        float rb = 0f;
        float rbDur = 0.06f;
        while (rb < rbDur && pv != null)
        {
            rb += Time.deltaTime;
            float u = Mathf.Clamp01(rb / rbDur);
            float e = 1f - Mathf.Pow(1f - u, 3f);

            float s = Mathf.Lerp(squashAmt, -reboundAmt, e);
            pv.transform.localScale = new Vector3(
                _defaultScale.x * (1f + s),
                _defaultScale.y * (1f - s),
                1f
            );
            yield return null;
        }

        if (pv != null)
        {
            pv.transform.localScale = _defaultScale;
            pv.transform.rotation = Quaternion.identity;
        }
    }


    private IEnumerator RejectShake(PieceView pv, float dur, float amp)
    {
        if (pv == null) yield break;
        var basePos = pv.transform.position;
        float t = 0f;
        while (t < dur && pv != null)
        {
            t += Time.deltaTime;
            float u = t / dur;
            float offs = Mathf.Sin(u * Mathf.PI * 4f) * amp; // neljä värähdystä
            pv.transform.position = basePos + new Vector3(offs, 0f, 0f);
            yield return null;
        }
        if (pv != null) pv.transform.position = basePos;
    }

    private IEnumerator SnapBack(PieceView pv, Vector3 to, float dur)
    {
        if (pv == null) yield break;
        Vector3 from = pv.transform.position;
        float t = 0f;
        while (t < dur && pv != null)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dur);
            pv.transform.position = Vector3.Lerp(from, to, u);
            yield return null;
        }
        if (pv != null) pv.transform.position = new Vector3(to.x, to.y, -1f);
    }
}
