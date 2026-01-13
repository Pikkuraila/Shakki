using System.Collections.Generic;
using UnityEngine;

namespace Shakki.Meta.Bestiary
{
    public sealed class BestiaryService
    {
        private readonly BestiaryProgressionRulesSO _rules;
        private readonly IBestiaryStore _store;

        private BestiarySaveData _data;
        private readonly Dictionary<string, BestiaryEntrySave> _index = new();

        public BestiaryService(BestiaryProgressionRulesSO rules, IBestiaryStore store)
        {
            _rules = rules;
            _store = store;
            Load();
        }

        public void HardReset()
        {
            _data = new BestiarySaveData();
            _index.Clear();
            Save();
        }

        public bool IsMoveKnown(string archetypeId)
        {
            archetypeId = Norm(archetypeId);

            var e = GetOrCreate(archetypeId);
            return e != null && (e.unlocks & BestiaryUnlock.MoveKnown) != 0;
        }


        public void Load()
        {
            _data = _store?.Load() ?? new BestiarySaveData();
            _index.Clear();

            if (_data.entries != null)
            {
                foreach (var e in _data.entries)
                {
                    if (e == null || string.IsNullOrEmpty(e.archetypeId)) continue;
                    _index[e.archetypeId] = e;

                    // ensure unlocks are consistent with counts
                    _rules?.ApplyProgress(e);
                }
            }

            Save(); // normalize once
        }

        public void Save()
        {
            _store?.Save(_data);
        }

        public BestiaryEntrySave GetOrCreate(string archetypeId)
        {
            if (string.IsNullOrEmpty(archetypeId))
                return null;

            if (_index.TryGetValue(archetypeId, out var existing) && existing != null)
                return existing;

            var created = new BestiaryEntrySave
            {
                archetypeId = archetypeId,
                seen = 0,
                kills = 0,
                unlocks = BestiaryUnlock.None
            };

            _data.entries ??= new List<BestiaryEntrySave>();
            _data.entries.Add(created);
            _index[archetypeId] = created;
            return created;
        }

        /// <summary>
        /// Call when an enemy individual is revealed/spawned/first observed.
        /// seen = 10 individuals seen (your spec)
        /// </summary>
        public void RecordSeen(string archetypeId, int amount = 1)
        {
            var e = GetOrCreate(archetypeId);
            if (e == null) return;

            int beforeSeen = e.seen;
            e.seen += Mathf.Max(0, amount);
            _rules?.ApplyProgress(e);

            Debug.Log($"[Bestiary] SEEN {archetypeId}: {beforeSeen} → {e.seen} | unlocks={e.unlocks}");

            Save();
        }

        public void RecordKill(string archetypeId, int amount = 1)
        {
            archetypeId = Norm(archetypeId);

            var e = GetOrCreate(archetypeId);
            if (e == null) return;

            int beforeKills = e.kills;
            e.kills += Mathf.Max(0, amount);
            _rules?.ApplyProgress(e);

            Debug.Log($"[Bestiary] KILL {archetypeId}: {beforeKills} → {e.kills} | unlocks={e.unlocks}");

            Save();
        }

        private static string Norm(string id)
        {
            if (string.IsNullOrEmpty(id)) return id;
            // Tavoite: "Pawn" "Rook" jne (PascalCase)
            // Jos haluat tiukemman mappingin myöhemmin, tee dict.
            return char.ToUpperInvariant(id[0]) + id.Substring(1);
        }


        public bool HasUnlock(string archetypeId, BestiaryUnlock unlock)
        {
            var e = GetOrCreate(archetypeId);
            if (e == null) return false;
            return (e.unlocks & unlock) != 0;
        }

        public BestiaryEntrySave GetEntry(string archetypeId)
        {
            if (string.IsNullOrEmpty(archetypeId)) return null;
            return _index.TryGetValue(archetypeId, out var e) ? e : null;
        }
    }


    
}

