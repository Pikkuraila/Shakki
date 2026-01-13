using System;
using System.Collections.Generic;
using UnityEngine;
using Shakki.Core;

namespace Shakki.Meta.Bestiary
{
    /// <summary>
    /// Attach to a running match (GameState) and record:
    /// - Seen: individuals present at battle start (scan board once)
    /// - Kills: via GameState.OnCaptured
    /// 
    /// This keeps Core clean: Core only raises events; Meta subscribes.
    /// </summary>
    public sealed class BestiaryMatchHooks : IDisposable
    {
        private readonly BestiaryService _bestiary;
        private readonly string _enemyOwner; // e.g. "black" (later: faction/team id)

        private GameState _gs;

        // optional: prevent double-counting "seen" if you call ScanSeen multiple times
        private readonly HashSet<string> _seenInstanceKeys = new();

        public BestiaryMatchHooks(BestiaryService bestiary, string enemyOwner = "black")
        {
            _bestiary = bestiary;
            _enemyOwner = enemyOwner;
        }

        public void Attach(GameState gs)
        {
            Detach();

            _gs = gs;
            if (_gs == null) return;

            _gs.OnCaptured += HandleCaptured;

            // IMPORTANT: call this ONCE when battle is ready (after pieces are placed)
            // You can call manually from controller, or call here if Attach happens late enough.
        }

        public void Detach()
        {
            if (_gs != null)
                _gs.OnCaptured -= HandleCaptured;

            _gs = null;
            _seenInstanceKeys.Clear();
        }

        public void Dispose() => Detach();

        /// <summary>
        /// Seen = "10 individuals seen". So scan the board and count each enemy piece instance once.
        /// Call once right after the encounter/position has been set.
        /// </summary>
        public void ScanInitialSeen()
        {
            if (_gs == null || _bestiary == null)
            {
                Debug.LogWarning("[Bestiary] ScanInitialSeen aborted: gs or bestiary is null");
                return;
            }

            Debug.Log("[Bestiary] ScanInitialSeen START");

            int total = 0;
            int enemies = 0;
            int newlySeen = 0;

            foreach (var c in _gs.AllCoords())
            {
                total++;

                var p = _gs.Get(c);
                if (p == null) continue;

                // Logataan kaikki nappulat kerran, jotta nähdään omistajat ja tyypit
                Debug.Log($"[Bestiary] Found piece at {c}: owner={p.Owner}, type={p.TypeName}");

                if (p.Owner != _enemyOwner) continue;

                enemies++;

                var key = $"{c.X},{c.Y}:{p.Owner}:{p.TypeName}";
                if (_seenInstanceKeys.Add(key))
                {
                    newlySeen++;
                    Debug.Log($"[Bestiary] SEEN +1 → {p.TypeName} at {c}");
                    _bestiary.RecordSeen(NormalizeArchetypeId(p.TypeName), 1);
                }
                else
                {
                    Debug.Log($"[Bestiary] SKIP duplicate → {p.TypeName} at {c}");
                }
            }

            Debug.Log($"[Bestiary] ScanInitialSeen END | totalCoords={total}, enemiesFound={enemies}, newlySeen={newlySeen}");
        }


        private void HandleCaptured(Coord at, Piece captured)
        {
            if (_bestiary == null || captured == null) return;

            // Only count enemy deaths (player kills)
            if (captured.Owner == _enemyOwner)
                _bestiary.RecordKill(NormalizeArchetypeId(captured.TypeName), 1);
        }

        private static string NormalizeArchetypeId(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                return "";

            typeName = typeName.Trim();

            // Canonical: PascalCase ("Pawn", "Rook", "Amazon", ...)
            if (typeName.Length == 1)
                return typeName.ToUpperInvariant();

            return char.ToUpperInvariant(typeName[0]) + typeName.Substring(1);
        }

    }
}
