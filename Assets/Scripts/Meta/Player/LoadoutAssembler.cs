using UnityEngine;

public static class LoadoutAssembler
{
    public static EncounterSO BuildEncounterFromLoadout(PlayerData data, SlotMapSO slots, bool fillEnemyClassic)
    {
        var enc = ScriptableObject.CreateInstance<EncounterSO>();
        enc.relativeRanks = true;

        // King pakollinen
        enc.spawns.Add(new EncounterSO.Spawn { owner = "white", pieceId = "King", x = 4, y = 0 });

        // T‰yt‰ backline/pawnline data.loadoutin mukaan (kuten aiemmassa vastauksessa)
        // ... (voit k‰ytt‰‰ samaa koodia jonka annoin)

        if (fillEnemyClassic)
        {
            enc.spawns.Add(new EncounterSO.Spawn { owner = "black", pieceId = "King", x = 3, y = 7 });
            enc.fillBlackPawnsAtY = true; enc.blackPawnsY = 6;
        }
        return enc;
    }
}
