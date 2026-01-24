using System;
using System.Collections.Generic;
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
            var computed = def.GetComputedTags();
            Debug.Log($"[Inspect] {def.typeName} identity={def.identityTags} computed={computed} rules={(def.rules != null ? def.rules.Length : 0)}");


            if (def == null) return null;

            var tags = new List<string>(16);

            AddIdentityTags(def.identityTags, tags);
            AddPieceTags(def.GetComputedTags(), tags);

            // dedupe + siisti järjestys
            var tagArr = tags
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(s => s)
                .ToArray();

            return new InspectData
            {
                id = def.typeName,
                title = def.GetDisplayName(),
                portrait = def.GetPortrait(),
                tags = tagArr,
                lore = def.GetLore(),
                infoLines = Array.Empty<string>() // jos et käytä infolines
            };
        }

        private static void AddIdentityTags(IdentityTag value, List<string> outTags)
        {
            if (outTags == null) return;

            // Jos IdentityTag:ssä on None=0, skipataan se automaattisesti.
            foreach (IdentityTag flag in Enum.GetValues(typeof(IdentityTag)))
            {
                if (flag == 0) continue;
                if ((value & flag) != 0)
                    outTags.Add(flag.ToString());
            }
        }


        private static void AddPieceTags(PieceTag value, List<string> outTags)
        {
            if (outTags == null) return;

            foreach (PieceTag flag in Enum.GetValues(typeof(PieceTag)))
            {
                if (flag == 0) continue;
                if ((value & flag) != 0)
                    outTags.Add(flag.ToString());
            }
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
