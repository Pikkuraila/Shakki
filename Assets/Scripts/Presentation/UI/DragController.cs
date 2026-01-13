// DragController.cs
// --- Puhdas pelilaudan drag & drop -kontrolleri ---
// Ei käsittele UI-dragia, haamuja tai shop-logiikkaa.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Shakki.Core;

// --- Pienet rajapinnat loadout/shop-yhteensopivuuteen ---
public interface ILoadoutAccess
{
    string GetTypeAt(int index);
    void SetTypeAt(int index, string type);
    void RefreshAll();
}

public sealed class NullLoadoutAccess : ILoadoutAccess
{
    public string GetTypeAt(int index) => null;
    public void SetTypeAt(int index, string type) { }
    public void RefreshAll() { }
}

public interface IShopEconomy
{
    bool CanAfford(string typeName);
    bool TryBuy(string typeName);
}

public sealed class NullShopEconomy : IShopEconomy
{
    public bool CanAfford(string typeName) => true;
    public bool TryBuy(string typeName) => true;
}

// --- Varsinainen kontrolleri ---
public class DragController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private BoardView board;   // Vedä Inspectorissa
    [SerializeField] private Camera cam;        // Jos null → Camera.main

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
    [SerializeField] private float snapToDur = 0.12f;
    [SerializeField] private float landSquash = 0.06f;
    [SerializeField] private float landRebound = 0.04f;

    private PieceView _dragPV;
    private Vector3 _dragStartWorld;
    private (int x, int y) _dragStartBoard;
    private List<Move> _legals = new();
    private SpriteRenderer _dragSR;
    private int _prevSortingOrder;
    private bool _dropping;

    private Vector3 _renderPos;
    private Vector3 _lastPos;
    private Vector3 _defaultScale = Vector3.one;
    private float _tiltDeg;

    private Camera Cam => cam != null ? cam : Camera.main;

    public event Action<PieceView, Vector2> Dropped;


    // --- Pelilauta-drag alkaa ---
    public void BeginDrag(PieceView pv)
    {
        if (pv == null || board == null || Cam == null) return;
        if (!board.CanHumanMove(pv)) return;

        _dragPV = pv;
        _dragStartWorld = pv.transform.position;
        _dragStartBoard = (pv.X, pv.Y);
        _renderPos = _dragStartWorld;

        _legals = board.GenerateMovesFrom(_dragStartBoard).ToList();

        // Näytä drag-highlightit vain jos liikutat omaa. Vihollisen tiedot vain hoverilla.
        if (pv.Owner == board.State.CurrentPlayer)
            board.ShowHighlightsPublic(_legals);
        else
            board.ClearHighlightsPublic();

        _dragSR = pv.GetComponentInChildren<SpriteRenderer>(true);
        _prevSortingOrder = _dragSR ? _dragSR.sortingOrder : 0;
        if (_dragSR) _dragSR.sortingOrder = dragSortingOrder;

        pv.transform.position = new Vector3(pv.transform.position.x, pv.transform.position.y, liftZ);
        _lastPos = pv.transform.position;
        _defaultScale = pv.transform.localScale;
        _tiltDeg = 0f;
        pv.transform.localScale = _defaultScale * scaleWhileDrag;
        pv.transform.rotation = Quaternion.identity;
    }

    void Update()
    {
        if (_dragPV == null) return;

        // Jos hiiren nappi ei ole pohjassa → päätä drag
        if (!Input.GetMouseButton(0))
        {
            if (!_dropping) StartCoroutine(FinishDrag());
            return;
        }

        // Seuraa hiirtä
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

        var mouseW = board.MouseWorldPublic(Cam);
        var to = board.WorldToBoardPublic(mouseW);

        // Smart drop: valitse lähin laillinen siirto
        float snapRadius = 0.6f;
        if (_legals != null && _legals.Count > 0)
        {
            var best = _legals.OrderBy(m =>
                Vector2.SqrMagnitude(new Vector2(mouseW.x, mouseW.y) - new Vector2(m.To.X, m.To.Y))).First();

            if (Vector2.Distance(new Vector2(mouseW.x, mouseW.y), new Vector2(best.To.X, best.To.Y)) <= snapRadius)
                to = (best.To.X, best.To.Y);
        }

        if (to.x == _dragStartBoard.x && to.y == _dragStartBoard.y)
        {
            yield return SnapBack(_dragPV, _dragStartWorld, snapBackDur);
        }
        else
        {
            bool ok = board.TryDropPublic(_dragPV, _dragStartBoard, to, _legals);

#if UNITY_EDITOR
            if (!ok)
                Debug.Log($"[Drag] Drop rejected at {to.x},{to.y}");
#endif

            if (!ok)
            {
                yield return RejectShake(_dragPV, illegalShakeDur, illegalShakeAmp);
                yield return SnapBack(_dragPV, _dragStartWorld, snapBackDur);
            }
            else
            {
                yield return SnapTo(_dragPV, _dragPV.transform.position, snapToDur, landSquash, landRebound);
            }
        }

        if (_dragPV != null)
        {
            try { Dropped?.Invoke(_dragPV, _dragPV.transform.position); }
            catch (Exception ex) { Debug.LogException(ex); }
        }

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
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dur);
            float e = 1f - Mathf.Pow(1f - u, 3f);
            pv.transform.position = Vector3.Lerp(startPos, end, e);
            yield return null;
        }
        yield return null;
    }

    private IEnumerator RejectShake(PieceView pv, float dur, float amp)
    {
        if (pv == null) yield break;
        var basePos = pv.transform.position;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float u = t / dur;
            float offs = Mathf.Sin(u * Mathf.PI * 4f) * amp;
            pv.transform.position = basePos + new Vector3(offs, 0f, 0f);
            yield return null;
        }
        pv.transform.position = basePos;
    }

    private IEnumerator SnapBack(PieceView pv, Vector3 to, float dur)
    {
        if (pv == null) yield break;
        Vector3 from = pv.transform.position;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dur);
            pv.transform.position = Vector3.Lerp(from, to, u);
            yield return null;
        }
        pv.transform.position = new Vector3(to.x, to.y, -1f);
    }

    // --- UI-yhteensopivuus (tyhjät stubit) ---
    public void HandleDropToLoadout(DropSlot target, UIDraggablePiece drag)
    {
        if (target?.loadoutView != null)
            target.loadoutView.HandleDropToLoadout(target, drag);
    }

    public void StartExternalUIDrag(SpriteRenderer sr, Vector3 worldStart) { }
    public void UpdateExternalUIDrag(Vector3 worldTarget) { }
    public IEnumerator FinishExternalUIDrag(bool accepted) { yield break; }
    public IEnumerator RejectExternalUIDrag(Vector3 worldBackPos) { yield break; }
}
