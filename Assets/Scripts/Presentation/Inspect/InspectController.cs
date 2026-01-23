using System.Linq;
using UnityEngine;
using Shakki.Core;

namespace Shakki.Presentation.Inspect
{
    public sealed class InspectController : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private InspectPanelView view;
        [SerializeField] private BoardView boardView;          // jotta voidaan näyttää moves highlight
        [SerializeField] private GameCatalogSO catalog;        // UI-iconien inspectiin

        [Header("Behavior")]
        [SerializeField] private bool showMovesOnInspect = true;



        void OnEnable()
        {
            Debug.Log("[InspectController] OnEnable subscribe");
            InspectService.Changed += OnChanged;
        }

        void OnDisable()
        {
            Debug.Log("[InspectController] OnDisable unsubscribe");
            InspectService.Changed -= OnChanged;
        }

        private void OnChanged(InspectData data)
        {
            // 1) Täydennä data catalogista jos mahdollista (portrait/tags/info)
            data = EnrichIfNeeded(data);

            Debug.Log($"[InspectController] Changed id={data?.id} hasBoardCoord={data?.hasBoardCoord}");
            Debug.Log($"[InspectController] view={(view ? "OK" : "NULL")} boardView={(boardView ? "OK" : "NULL")} showMoves={showMovesOnInspect}");

            // 2) Päivitä UI
            if (view) view.Render(data);

            // 3) Moves-highlightit boardilta (vain jos selection tuli boardilta)
            if (!showMovesOnInspect || boardView == null) return;

            if (data != null && data.hasBoardCoord)
            {
                boardView.ExternalHighlightsLock = true;

                var moves = boardView.GenerateMovesFrom((data.boardX, data.boardY)).ToList();
                boardView.ShowHighlightsPublic(moves);
            }
        }

        private InspectData EnrichIfNeeded(InspectData data)
        {
            if (data == null) return null;

            // Jos data on jo "täysi", ei tehdä mitään
            bool needsPortrait = data.portrait == null;
            bool needsTags = data.tags == null || data.tags.Length == 0;
            bool needsInfo = data.infoLines == null || data.infoLines.Length == 0;

            if ((!needsPortrait && !needsTags && !needsInfo) || catalog == null)
                return data;

            var def = catalog.GetPieceById(data.id);
            if (def == null)
                return data;

            var full = BuildFromPieceDef(def);

            // Säilytä alkuperäiset (esim. shopDef.tags) jos niitä oli
            if (!needsTags && data.tags != null && data.tags.Length > 0)
                full.tags = data.tags;

            // Säilytä mahdolliset board-koordinaatit
            full.hasBoardCoord = data.hasBoardCoord;
            full.boardX = data.boardX;
            full.boardY = data.boardY;

            return full;
        }

        private InspectData BuildFromPieceDef(PieceDefSO def)
        {
            if (def == null) return null;

            // Tags: enum -> string listaksi
            string[] tags =
                def.tags == 0
                    ? System.Array.Empty<string>()
                    : def.tags.ToString()
                              .Split(',')
                              .Select(s => s.Trim())
                              .Where(s => !string.IsNullOrEmpty(s))
                              .ToArray();

            // Info: rules SO -nimet (parempi kuin ei mitään)
            string[] info =
                (def.rules == null || def.rules.Length == 0)
                    ? new[] { "No move rules" }
                    : def.rules
                        .Where(r => r != null)
                        .Select(r => r.name)
                        .Distinct()
                        .ToArray();

            return new InspectData
            {
                id = def.typeName,
                title = def.typeName,

                // ✅ ensisijaisesti erillinen portrait, fallback board-spriteen
                portrait = def.portraitSprite != null ? def.portraitSprite : def.whiteSprite,

                tags = tags,
                infoLines = info,
                lore = ""
            };

        }


        // Helperit muille kutsua (valinnainen)
        public void InspectPieceDef(PieceDefSO def)
        {
            if (def == null) return;
            var data = BuildFromPieceDef(def);
            InspectService.Select(data);
        }

        public void InspectBoardPiece(PieceView pv)
        {
            if (pv == null) return;

            PieceDefSO def = null;
            if (catalog != null) def = catalog.GetPieceById(pv.TypeName);

            InspectData data = def != null ? BuildFromPieceDef(def) : new InspectData
            {
                id = pv.TypeName,
                title = pv.TypeName,
                portrait = null,
                tags = null,
                infoLines = null,
                lore = ""
            };

            data.hasBoardCoord = true;
            data.boardX = pv.X;
            data.boardY = pv.Y;

            InspectService.Select(data);
        }
          
    }
}
