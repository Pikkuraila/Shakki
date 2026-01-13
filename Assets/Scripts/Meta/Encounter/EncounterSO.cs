using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Shakki/Encounter", fileName = "Encounter")]
public sealed class EncounterSO : ScriptableObject
{

    // EncounterSO.cs
    [Header("Win condition")]
    public bool requireWhiteKing = true;
    public bool requireBlackKing = true;


    [Serializable]
    public struct Spawn
    {
        public string owner;   // "white" tai "black"
        public string pieceId; // esim "King", "Queen", "Rook" (PieceDefSO.id tai typeName)
        public int x;
        public int y;
    }

    [Tooltip("Nappuloiden suorat spawnauskohdat.")]
    public List<Spawn> spawns = new();

    [Header("Pikakomennot")]
    public bool fillWhitePawnsAtY;
    public int whitePawnsY = 1;

    public bool fillBlackPawnsAtY;
    public int blackPawnsY = 6;

    [Tooltip("Jos true, y-rivit tulkitaan suhteessa laudan korkeuteen (esim. white back rank = 0, black back rank = Height-1).")]
    public bool relativeRanks = true;
}
