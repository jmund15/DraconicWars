namespace DraconicWars.Meta;

using System;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// The persistent account state. Pure CLR (System.Text.Json) so the Logic domain can
/// test it; the Godot layer owns file IO via Jmodot's atomic persistence.
/// </summary>
public sealed class PlayerProfile
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public int Gold { get; set; }

    public int Sigils { get; set; }

    /// <summary>Unlocked units: unit id → current level. Presence = unlocked.</summary>
    public Dictionary<string, int> UnitLevels { get; set; } = new();

    public int LevelsPurchased { get; set; }

    public List<string> ClearedLevelIds { get; set; } = new();

    public int CampaignFirstClears { get; set; }

    public int HeatRungClears { get; set; }

    public int AchievementsEarned { get; set; }

    public int GoldSpentOnLevels { get; set; }

    public string ToJson()
    {
        return JsonSerializer.Serialize(this);
    }

    public static PlayerProfile FromJson(string json)
    {
        var profile = JsonSerializer.Deserialize<PlayerProfile>(json)
            ?? throw new InvalidOperationException("Profile JSON deserialized to null");
        if (profile.SchemaVersion > CurrentSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Profile schema {profile.SchemaVersion} is newer than supported {CurrentSchemaVersion}");
        }
        return profile;
    }
}
