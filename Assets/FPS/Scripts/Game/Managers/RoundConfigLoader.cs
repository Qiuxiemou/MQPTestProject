using System;
using System.Collections.Generic;
using UnityEngine;

public static class RoundConfigLoader
{
    public static List<RoundCondition> Load(TextAsset confFile)
    {
        var conditions = new List<RoundCondition>();

        if (confFile == null)
        {
            Debug.LogError("[RoundConfigLoader] Config file is null");
            return conditions;
        }

        var lines = confFile.text.Split('\n');

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();

            // Skip comments and empty lines
            if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                continue;

            // Expect: id | player | latency | timewarp
            var tokens = line.Split('|');

            if (tokens.Length != 6)
            {
                Debug.LogError($"[RoundConfigLoader] Invalid line: {line}");
                continue;
            }

            try
            {
                var condition = new RoundCondition
                {
                    id = int.Parse(tokens[0].Trim()),
                    bot = Enum.Parse<BotRole>(tokens[1].Trim(), true),
                    latencyMs = int.Parse(tokens[2].Trim()),
                    timewarp = Enum.Parse<TimewarpMode>(tokens[3].Trim(), true),
                    speed = float.Parse(tokens[4].Trim()),
                    weapon = int.Parse(tokens[5].Trim())
                };

                conditions.Add(condition);
            }
            catch (Exception e)
            {
                Debug.LogError($"[RoundConfigLoader] Failed to parse line:\n{line}\n{e}");
            }
        }

        return conditions;
    }
}
