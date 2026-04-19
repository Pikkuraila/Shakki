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
            archetypeId = BestiaryIds.Normalize(archetypeId);

            var e = GetOrCreate(archetypeId);
            return e != null && (e.unlocks & BestiaryUnlock.MoveKnown) != 0;
        }


        public void Load()
        {
            _data = _store?.Load() ?? new BestiarySaveData();
            _index.Clear();
            NormalizeLoadedEntries();

            Save(); // normalize once
        }

        public void Save()
        {
            _store?.Save(_data);
        }

        public BestiaryEntrySave GetOrCreate(string archetypeId)
        {
            archetypeId = BestiaryIds.Normalize(archetypeId);
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
            var e = GetOrCreate(archetypeId);
            if (e == null) return;

            int beforeKills = e.kills;
            e.kills += Mathf.Max(0, amount);
            _rules?.ApplyProgress(e);

            Debug.Log($"[Bestiary] KILL {archetypeId}: {beforeKills} → {e.kills} | unlocks={e.unlocks}");

            Save();
        }

        public bool HasUnlock(string archetypeId, BestiaryUnlock unlock)
        {
            archetypeId = BestiaryIds.Normalize(archetypeId);
            var e = GetOrCreate(archetypeId);
            if (e == null) return false;
            return (e.unlocks & unlock) != 0;
        }

        public BestiaryEntrySave GetEntry(string archetypeId)
        {
            archetypeId = BestiaryIds.Normalize(archetypeId);
            if (string.IsNullOrEmpty(archetypeId)) return null;
            return _index.TryGetValue(archetypeId, out var e) ? e : null;
        }

        private void NormalizeLoadedEntries()
        {
            var normalizedEntries = new List<BestiaryEntrySave>();
            if (_data.entries == null)
            {
                _data.entries = normalizedEntries;
                return;
            }

            foreach (var entry in _data.entries)
            {
                if (entry == null)
                    continue;

                var archetypeId = BestiaryIds.Normalize(entry.archetypeId);
                if (string.IsNullOrEmpty(archetypeId))
                    continue;

                if (_index.TryGetValue(archetypeId, out var existing) && existing != null)
                {
                    existing.seen += entry.seen;
                    existing.kills += entry.kills;
                    existing.unlocks |= entry.unlocks;
                    continue;
                }

                entry.archetypeId = archetypeId;
                _index[archetypeId] = entry;
                normalizedEntries.Add(entry);
            }

            foreach (var entry in normalizedEntries)
                _rules?.ApplyProgress(entry);

            _data.entries = normalizedEntries;
        }
    }


    
}

