using System;

public enum PlayerRole
{
    Hider,
    Seeker
}

public enum TimewarpMode
{
    None,
    Normal,
    Conditional
}

[Serializable]
public class RoundCondition
{
    public int id;
    public PlayerRole player;
    public int latencyMs;
    public TimewarpMode timewarp;
}
