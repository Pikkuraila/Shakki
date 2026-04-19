using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Shakki.Core;

public sealed class AlchemistEncounterView : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private LoadoutGridView loadoutView;
    [SerializeField] private GameCatalogSO catalog;

    [Header("Slots (UI)")]
    [SerializeField] private Transform inputSlotA;     // tyhjä GO, jossa DropSlot + ChessSlotSkin
    [SerializeField] private Transform inputSlotB;     // tyhjä GO, jossa DropSlot + ChessSlotSkin
    [SerializeField] private Transform outputSlot;     // tyhjä GO, jossa DropSlot + ChessSlotSkin

    [Header("Output Icon")]
    [SerializeField] private GameObject pieceIconPrefab; // (valinnainen) jos haluat käyttää PieceIcon-prefabia
    [SerializeField] private PieceDefSO baseAmalgamDef;  // sun Amalgam.asset (typeName="Amalgam")

    [Header("State (debug)")]
    [SerializeField] private string inputA;
    [SerializeField] private string inputB;

    [SerializeField] private Transform inputAAnchor;
    [SerializeField] private Transform inputBAnchor;

    private LoadoutGridView _loadout;
    private GameCatalogSO _catalog;

    private int _srcIndexA = -1;
    private int _srcIndexB = -1;
    private string _pieceA = "";
    private string _pieceB = "";
    private string _instanceIdA = "";
    private string _instanceIdB = "";

    private bool _outputReady;

    private Shakki.Core.IRuntimeRulesRegistry _runtimeRegistry;


    private bool _completedThisEncounter;
    private int _amalgamIndexInCatalog = -1;
    private PieceDefSO _originalAmalgamInCatalog;

    public void Setup(LoadoutGridView loadout, GameCatalogSO cat, PieceDefSO amalgamDef,
                  Shakki.Core.IRuntimeRulesRegistry runtimeRegistry)
    {
        loadoutView = loadout;
        catalog = cat;
        _loadout = loadout;
        _catalog = cat;
        baseAmalgamDef = amalgamDef != null ? amalgamDef : baseAmalgamDef;
        _runtimeRegistry = runtimeRegistry;

        Debug.Log($"[Alchemist] Setup registry={(_runtimeRegistry != null ? "OK" : "NULL")}");

        BindDropSlots();
        ClearAll();
    }

    private void OnDisable()
    {
        // Palauta catalog jos korvattiin se runtime-defillä
        RestoreCatalogAmalgam();
    }

    private void BindDropSlots()
    {
        if (inputSlotA == null) inputSlotA = transform.Find("InputSlotA");
        if (inputSlotB == null) inputSlotB = transform.Find("InputSlotB");
        if (outputSlot == null) outputSlot = transform.Find("OutputSlot"); // <-- puuttui

        Debug.Log($"[Alchemist] BindDropSlots A={(inputSlotA ? inputSlotA.name : "NULL")} B={(inputSlotB ? inputSlotB.name : "NULL")} O={(outputSlot ? outputSlot.name : "NULL")} root={transform.name}");

        void Bind(Transform t, SlotKind kind, int idx)
        {
            if (t == null) { Debug.LogError($"[Alchemist] Missing slot for {kind} idx={idx}"); return; }

            foreach (var childBad in t.GetComponentsInChildren<UIDraggablePiece>(true))
                Destroy(childBad);

            var ds = t.GetComponent<DropSlot>() ?? t.gameObject.AddComponent<DropSlot>();
            ds.kind = kind;
            ds.index = idx;
            ds.alchemistView = this;

            var img = t.GetComponent<Image>() ?? t.gameObject.AddComponent<Image>();
            img.raycastTarget = true;
            if (img.color.a <= 0f) img.color = new Color(1f, 1f, 1f, 0.02f);
        }

        Bind(inputSlotA, SlotKind.AlchemistInput, 0);
        Bind(inputSlotB, SlotKind.AlchemistInput, 1);
        Bind(outputSlot, SlotKind.AlchemistOutput, 0);
    }

    private bool IsLiving(PieceDefSO def)
    {
        if (def == null) return false;
        return (def.identityTags & IdentityTag.Living) != 0;
    }


    private bool IsLivingId(string pieceId)
    {
        if (catalog == null || string.IsNullOrEmpty(pieceId)) return false;
        var def = catalog.GetPieceById(pieceId);
        return IsLiving(def);
    }

    private bool CanAcceptIntoFusionInputs(UIDraggablePiece drag, out string reason)
    {
        reason = null;

        if (drag == null) { reason = "drag null"; return false; }

        // hyväksy vain loadoutista tuleva piece
        if (drag.originKind != SlotKind.Loadout) { reason = "not from loadout"; return false; }
        if (drag.payloadKind != DragPayloadKind.Piece) { reason = "not a piece"; return false; }

        // payloadId on se mitä halutaan käyttää (loadoutissa se on typeName)
        var pieceId = drag.payloadId;
        if (string.IsNullOrEmpty(pieceId))
        {
            // fallback jos jostain syystä payloadId puuttuu
            pieceId = drag.typeName;
        }

        if (string.IsNullOrEmpty(pieceId)) { reason = "missing piece id"; return false; }

        if (!IsLivingId(pieceId))
        {
            reason = "only Living pieces can be placed into fusion inputs";
            return false;
        }

        return true;
    }

    public void ClearAll()
    {
        inputA = "";
        inputB = "";
        _pieceA = "";
        _pieceB = "";
        _instanceIdA = "";
        _instanceIdB = "";
        _srcIndexA = -1;
        _srcIndexB = -1;
        _completedThisEncounter = false;

        ClearChildren(inputSlotA);
        ClearChildren(inputSlotB);
        ClearChildren(outputSlot);

        RestoreCatalogAmalgam();

        if (loadoutView != null)
        {
            loadoutView.BuildIfNeeded();
            loadoutView.RefreshAll();
        }

        _outputReady = false;

    }

    // DropSlot kutsuu tätä, kun tiputetaan input-ruutuun
    public void HandleDropToInput(DropSlot slot, UIDraggablePiece drag)
    {
        if (_completedThisEncounter) return;
        if (drag == null || loadoutView == null || slot == null) return;

        // hyväksy vain loadoutista tuleva Piece
        if (drag.originKind != SlotKind.Loadout || drag.payloadKind != DragPayloadKind.Piece)
            return;

        // ✅ vain Living-pieceet saa mennä inputteihin
        var pieceId = !string.IsNullOrEmpty(drag.payloadId) ? drag.payloadId : drag.typeName;
        if (string.IsNullOrEmpty(pieceId) || !IsLivingId(pieceId))
        {
            Debug.Log($"[Alchemist] Reject input drop: requires Living. id={pieceId}");
            return;
        }

        // slotin pitää olla A(0) tai B(1)
        if (slot.index != 0 && slot.index != 1) return;

        // jos inputissa jo jotain, älä hyväksy (ei jumitusta, koska ei syödä mitään)
        if (slot.index == 0 && !string.IsNullOrEmpty(inputA)) return;
        if (slot.index == 1 && !string.IsNullOrEmpty(inputB)) return;

        int srcUi = drag.originIndex;
        int srcData = loadoutView.UiToDataIndex(srcUi);

        var player = loadoutView.playerService != null ? loadoutView.playerService : PlayerService.Instance;
        if (player == null) return;

        var previewInstance = player.GetLoadoutInstanceAtSlot(srcData);
        var previewId = PlayerInstanceSync.GetLegacyPieceId(previewInstance);
        if (previewInstance == null || string.IsNullOrEmpty(previewId)) return;

        if (!IsLivingId(previewId))
        {
            Debug.Log($"[Alchemist] Reject input drop (non-living by data id): {previewId}");
            return;
        }

        var instance = player.TakeLoadoutPieceInstanceAtSlot(srcData);
        var id = PlayerInstanceSync.GetLegacyPieceId(instance);
        Debug.Log($"[Alchemist] HandleDropToInput slotIdx={slot.index} srcUi={srcUi} srcData={srcData} id={id} payloadId={drag.payloadId} instanceId={instance?.instanceId}");

        if (instance == null || string.IsNullOrEmpty(id)) return;

        // aseta input-state
        if (slot.index == 0)
        {
            inputA = id;
            _pieceA = id;
            _instanceIdA = instance.instanceId;
            _srcIndexA = srcData;
        }
        else
        {
            inputB = id;
            _pieceB = id;
            _instanceIdB = instance.instanceId;
            _srcIndexB = srcData;
        }

        // piirrä inputit ja yritä fusion
        RedrawInputs();
        TryActivateFusion();

        // kuluta drag-kopio
        drag.MarkConsumed(-1);

        // päivitä loadout
        loadoutView.RefreshAll();
    }







    // DropSlot kutsuu tätä, kun tiputetaan output-ruutuun (käytännössä ei tarvita)
    public void HandleDropToOutput(DropSlot target, UIDraggablePiece drag)
    {
        // Outputiin ei tiputeta mitään; outputista tiputetaan pois loadouttiin.
    }

    // LoadoutGridView kutsuu tätä, kun output-amalgam tiputetaan loadout-slotille
    public bool ConsumeOutputToLoadout(int targetLoadoutIndex, UIDraggablePiece drag)
    {
        if (_completedThisEncounter) return false;
        if (loadoutView == null) return false;
        if (drag == null) return false;

        var player = loadoutView.playerService != null ? loadoutView.playerService : PlayerService.Instance;
        if (player == null) return false;
        if (player.GetLoadoutInstanceAtSlot(targetLoadoutIndex) != null) return false;
        if (!player.TryAssignNewPieceToLoadoutSlot(targetLoadoutIndex, drag.payloadId)) return false;

        _completedThisEncounter = true;

        // siivoa UI
        inputA = "";
        inputB = "";
        _pieceA = "";
        _pieceB = "";
        _instanceIdA = "";
        _instanceIdB = "";
        ClearChildren(inputSlotA);
        ClearChildren(inputSlotB);
        ClearChildren(outputSlot);

        loadoutView.RefreshAll();
        return true;
    }




    private IEnumerator CoRefreshLoadoutAfterDrag()
    {
        yield return null;
        if (loadoutView != null)
            loadoutView.RefreshAll();
    }

    private void RedrawInputs()
    {
        if (catalog == null) return;
        if (inputSlotA == null || inputSlotB == null)
        {
            Debug.LogError("[Alchemist] input slots missing (inputSlotA/B). Check bindings.");
            return;
        }

        ClearChildren(inputSlotA);
        ClearChildren(inputSlotB);

        if (!string.IsNullOrEmpty(inputA)) CreateStaticIcon(inputSlotA, inputA);
        if (!string.IsNullOrEmpty(inputB)) CreateStaticIcon(inputSlotB, inputB);
    }


    private void TryActivateFusion()
    {
        Debug.Log($"[Alchemist] TryActivateFusion A={inputA} B={inputB}");

        if (catalog == null || baseAmalgamDef == null) return;
        if (string.IsNullOrEmpty(inputA) || string.IsNullOrEmpty(inputB)) return;

        var aDef = catalog.GetPieceById(inputA);
        var bDef = catalog.GetPieceById(inputB);
        if (aDef == null || bDef == null) { Debug.LogWarning("[Alchemist] aDef/bDef null"); return; }

        // ✅ safety: myös tässä varmistus (vaikka drop jo estää)
        if (!IsLiving(aDef) || !IsLiving(bDef))
        {
            Debug.Log("[Alchemist] Fusion blocked: ingredients must be Living.");
            return;
        }

        // 1) Uniikki ID (run-aikainen)
        var runtimeId = $"Amalgam_{aDef.typeName}_{bDef.typeName}_{System.Guid.NewGuid():N}";

        // 2) Runtime def base-assetista
        var runtime = Instantiate(baseAmalgamDef);
        runtime.name = runtimeId;
        runtime.typeName = runtimeId;

        // ✅ identity is ALWAYS Amalgam (ei peri rotuja)
        runtime.identityTags = IdentityTag.Amalgam | IdentityTag.Living;

        // 3) Spritet
        runtime.whiteSprite = baseAmalgamDef.whiteSprite;
        runtime.blackSprite = baseAmalgamDef.blackSprite;

        // 4) Rules merge (sis. baseAmalgamDef.rules jos haluat “amalgam tag rule” tms)
        var baseRules = baseAmalgamDef.rules ?? System.Array.Empty<MoveRuleSO>();
        var rulesA = aDef.rules ?? System.Array.Empty<MoveRuleSO>();
        var rulesB = bDef.rules ?? System.Array.Empty<MoveRuleSO>();

        runtime.rules = baseRules
            .Concat(rulesA)
            .Concat(rulesB)
            .Where(r => r != null)
            .Distinct()
            .ToArray();

        Debug.Log($"[Alchemist] Runtime rules merged: id={runtimeId} rules={runtime.rules.Length} (base={baseRules.Length} A={rulesA.Length}, B={rulesB.Length})");

        // ✅ rekisteröi runtime def rules-resolveriin
        if (_runtimeRegistry != null)
        {
            _runtimeRegistry.RegisterOrReplace(runtime);
            Debug.Log($"[Alchemist] Runtime rules registered into resolver for {runtime.typeName}");
        }
        else
        {
            Debug.LogWarning("[Alchemist] _runtimeRegistry is NULL – runtime rules not registered (expect rules=0 / moves=0).");
        }

        // 5) Rekisteröi runtime def catalogiin
        catalog.RegisterRuntimePiece(runtime);

        // 6) Piirrä output draggable
        ClearChildren(outputSlot);
        CreateDraggableOutputIcon(outputSlot, runtime);

        _outputReady = true;
    }





    private void OverrideCatalogAmalgam(PieceDefSO runtime)
    {
        if (catalog?.pieces == null) return;

        if (_amalgamIndexInCatalog < 0)
        {
            _amalgamIndexInCatalog = catalog.pieces.FindIndex(p => p != null && p.typeName == "Amalgam");
            if (_amalgamIndexInCatalog >= 0)
                _originalAmalgamInCatalog = catalog.pieces[_amalgamIndexInCatalog];
        }

        if (_amalgamIndexInCatalog >= 0)
            catalog.pieces[_amalgamIndexInCatalog] = runtime;
    }

    private void RestoreCatalogAmalgam()
    {
        if (catalog?.pieces == null) return;
        if (_amalgamIndexInCatalog >= 0 && _originalAmalgamInCatalog != null)
            catalog.pieces[_amalgamIndexInCatalog] = _originalAmalgamInCatalog;

        _amalgamIndexInCatalog = -1;
        _originalAmalgamInCatalog = null;
    }

    private void CreateStaticIcon(Transform parent, string pieceId)
    {
        var def = catalog.GetPieceById(pieceId);
        var sprite = def != null ? def.whiteSprite : null;

        var go = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        go.transform.SetParent(parent, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.enabled = (sprite != null);
        img.preserveAspect = true;

        // Ei draggable – inputit on “lukittuja”
        var cg = go.GetComponent<CanvasGroup>();
        cg.blocksRaycasts = false;

        Debug.Log($"[AlchemistUI] Created icon '{go.name}' parent='{parent.name}' " +
          $"parentPath='{GetPath(parent)}' iconPath='{GetPath(go.transform)}' " +
          $"root='{go.transform.root.name}'");


    }

    private void CreateDraggableOutputIcon(Transform parent, PieceDefSO runtimeAmalgamDef)
    {
        GameObject iconGO;

        if (pieceIconPrefab != null)
        {
            iconGO = Instantiate(pieceIconPrefab, parent);
        }
        else
        {
            iconGO = new GameObject("OutputAmalgam",
                typeof(RectTransform),
                typeof(Image),
                typeof(CanvasGroup),
                typeof(LayoutElement));
            iconGO.transform.SetParent(parent, false);
        }

        // venytä
        var rt = iconGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // --- TÄRKEIN: varmista että RAYCAST osuu tähän GO:hon ---
        // 1) Etsi kuvakomponentti (prefabissa voi olla childissä)
        var img = iconGO.GetComponent<Image>();
        if (img == null) img = iconGO.GetComponentInChildren<Image>(true);

        // 2) Jos Image ei ole iconGO:ssa, siirrä UIDraggablePiece siihen samaan objektiin
        GameObject dragHost = (img != null) ? img.gameObject : iconGO;

        // Sprite
        var sprite = runtimeAmalgamDef != null ? runtimeAmalgamDef.whiteSprite : null;
        if (img != null)
        {
            img.sprite = sprite;
            img.enabled = (sprite != null);
            img.preserveAspect = true;
            img.raycastTarget = true; // <-- pakota
        }

        // CanvasGroup (prefabissa voi olla eri tasolla)
        var cg = dragHost.GetComponent<CanvasGroup>();
        if (cg == null) cg = dragHost.AddComponent<CanvasGroup>();

        // LEVOSSA: blokkaa raycastit, että tätä voi klikata/raahata
        cg.alpha = 1f;
        cg.interactable = true;
        cg.blocksRaycasts = true;
        cg.ignoreParentGroups = false;

        // UIDraggablePiece pitää olla samassa GO:ssa kuin raycastattu Image
        var drag = dragHost.GetComponent<UIDraggablePiece>() ?? dragHost.AddComponent<UIDraggablePiece>();
        drag.useDragLayer = true;
        drag.originKind = SlotKind.AlchemistOutput;
        drag.originIndex = 0;
        drag.payloadKind = DragPayloadKind.Piece;
        drag.payloadId = runtimeAmalgamDef.typeName; // <-- runtimeId
        drag.typeName = runtimeAmalgamDef.typeName; 
        drag.loadoutView = loadoutView;
        drag.alchemistView = this;

        if (loadoutView != null) drag.dragLayer = loadoutView.dragLayer;

        // Debug: missä tää oikeasti on ja löytyykö canvas?
        var canvas = dragHost.GetComponentInParent<Canvas>();
        Debug.Log($"[Alchemist] Output created host='{dragHost.name}' parent='{parent.name}' canvas={(canvas ? canvas.name : "NULL")} sprite={(sprite ? sprite.name : "NULL")}");
        Debug.Log($"[Alchemist] Output payloadId={drag.payloadId} typeName={drag.typeName} sprite={(sprite ? sprite.name : "NULL")}");

    }


    private static void ClearChildren(Transform t)
    {
        if (t == null) return;
        for (int i = t.childCount - 1; i >= 0; i--)
            Destroy(t.GetChild(i).gameObject);
    }

    public void HandleDrop(AlchemistSlotKind slot, UIDraggablePiece drag)
    {
        if (drag == null) return;
        if (_loadout == null || _catalog == null) return;

        // hyväksytään vain loadoutista tuleva piece
        if (drag.originKind != SlotKind.Loadout || drag.payloadKind != DragPayloadKind.Piece)
            return;

        switch (slot)
        {
            case AlchemistSlotKind.InputA:
                HandleDropToInput(inputSlotA != null ? inputSlotA.GetComponent<DropSlot>() : null, drag);
                break;
            case AlchemistSlotKind.InputB:
                HandleDropToInput(inputSlotB != null ? inputSlotB.GetComponent<DropSlot>() : null, drag);
                break;
            default: break;
        }
    }
    
    static string GetPath(Transform t)
    {
        if (t == null) return "<null>";
        string p = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            p = t.name + "/" + p;
        }
        return p;
    }
}
