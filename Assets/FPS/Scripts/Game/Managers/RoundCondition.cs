using System;

public enum BotRole
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
    public BotRole bot;
    public int latencyMs;
    public TimewarpMode timewarp;
    public float speed;
    public string weapon;
}
