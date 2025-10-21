using System.IO;
using UnityEngine;

public sealed class JsonDataStore : IDataStore
{
    readonly string _path = Path.Combine(Application.persistentDataPath, "player.json");

    public bool TryLoad(out PlayerData data)
    {
        if (!File.Exists(_path)) { data = null; return false; }
        var json = File.ReadAllText(_path);
        data = JsonUtility.FromJson<PlayerData>(json);
        return data != null;
    }

    public void Save(PlayerData data)
    {
        var json = JsonUtility.ToJson(data, prettyPrint: true);
        File.WriteAllText(_path, json);
#if UNITY_EDITOR
        Debug.Log($"[Save] {_path}\n{json}");
#endif
    }

    public void Wipe()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }
}
