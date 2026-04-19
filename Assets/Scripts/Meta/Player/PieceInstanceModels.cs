using System;
using System.Collections.Generic;

public enum PieceInstanceKind
{
    Standard = 0,
    Amalgam = 1,
}

[Serializable]
public sealed class PersistentPieceStatusData
{
    public string statusId;
    public int stacks = 1;
    public int duration = -1;
}

[Serializable]
public sealed class AmalgamPieceData
{
    public string baseDefId = "amalgam";
    public string runtimePieceDefId;

    public string sourceAInstanceId;
    public string sourceBInstanceId;

    public string sourceAPieceDefId;
    public string sourceBPieceDefId;

    public List<string> mergedRuleIds = new();
}

[Serializable]
public sealed class PieceInstanceData
{
    public string instanceId;
    public PieceInstanceKind kind = PieceInstanceKind.Standard;
    public string pieceDefId;

    public string customName;
    public bool isDead;
    public int xp;

    public List<PersistentPieceStatusData> statuses = new();
    public List<string> attachedPowerupIds = new();

    public AmalgamPieceData amalgam;
}

[Serializable]
public sealed class LoadoutSlotInstanceData
{
    public int slotIndex;
    public string pieceInstanceId;
}
