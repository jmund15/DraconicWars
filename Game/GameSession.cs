namespace DraconicWars.Game;

using DraconicWars.Meta;
using Godot;
using Jmodot.Implementation.Shared;

/// <summary>
/// Cross-scene session state: the loaded profile and the selected campaign level.
/// Static (no autoload line needed — no boot-order dependency). Profile writes are
/// atomic: tmp-write → delete → rename, with the .tmp BEFORE the extension.
/// </summary>
public static class GameSession
{
    private const string SavePath = "user://profile.json";
    private const string TmpPath = "user://profile.tmp.json";

    public static PlayerProfile Profile { get; private set; } = new();

    public static int SelectedLevelIndex { get; set; }

    private static bool _loaded;

    public static void EnsureProfileLoaded()
    {
        if (_loaded)
        {
            return;
        }
        _loaded = true;

        if (!FileAccess.FileExists(SavePath))
        {
            return;
        }
        try
        {
            Profile = PlayerProfile.FromJson(FileAccess.GetFileAsString(SavePath));
        }
        catch (System.Exception e)
        {
            JmoLogger.Warning(typeof(GameSession), $"[Session] Profile load failed ({e.Message}); starting fresh");
            Profile = new PlayerProfile();
        }
    }

    public static void SaveProfile()
    {
        using (var file = FileAccess.Open(TmpPath, FileAccess.ModeFlags.Write))
        {
            file.StoreString(Profile.ToJson());
        }
        if (FileAccess.FileExists(SavePath))
        {
            DirAccess.RemoveAbsolute(SavePath);
        }
        DirAccess.RenameAbsolute(TmpPath, SavePath);
    }

    #region Test Helpers
#if TOOLS
    internal static void _TestReset()
    {
        Profile = new PlayerProfile();
        SelectedLevelIndex = 0;
        _loaded = true;
    }
#endif
    #endregion
}
