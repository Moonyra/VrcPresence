namespace VrcPresence;

/// <summary>Aktueller Zustand aus der VRChat-Logdatei.</summary>
sealed class WorldState
{
    public string? WorldId;
    public string? InstanceId;
    public string? WorldName;
    public string? InstanceType;
    public int Players;
    public DateTime? Since;
    public DateTime? PlayStartedAt;

    public void Reset()
    {
        ResetWorld();
        PlayStartedAt = null;
    }

    public void ResetWorld()
    {
        WorldId = null;
        InstanceId = null;
        WorldName = null;
        InstanceType = null;
        Players = 0;
        Since = null;
    }
}

sealed record WorldInfo(string? ImageUrl, int? Capacity);
