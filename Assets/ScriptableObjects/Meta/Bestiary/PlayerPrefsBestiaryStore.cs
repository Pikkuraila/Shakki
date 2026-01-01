using UnityEngine;

namespace Shakki.Meta.Bestiary
{
    public sealed class PlayerPrefsBestiaryStore : IBestiaryStore
    {
        private const string Key = "shakki_bestiary_json";

        public BestiarySaveData Load()
        {
            var json = PlayerPrefs.GetString(Key, "");
            if (string.IsNullOrEmpty(json)) return new BestiarySaveData();
            try
            {
                return JsonUtility.FromJson<BestiarySaveData>(json) ?? new BestiarySaveData();
            }
            catch
            {
                return new BestiarySaveData();
            }
        }

        public void Save(BestiarySaveData data)
        {
            if (data == null) data = new BestiarySaveData();
            var json = JsonUtility.ToJson(data);
            PlayerPrefs.SetString(Key, json);
            PlayerPrefs.Save();
        }
    }
}
