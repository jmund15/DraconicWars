namespace DraconicWars.Game.Menu;

using Godot;
using Jmodot.Core.Shared.Attributes;

public partial class MainMenu : Control
{
    [Export, RequiredExport] public Button PlayButton { get; set; } = null!;

    [Export, RequiredExport] public Button PvpButton { get; set; } = null!;

    [Export, RequiredExport] public Button QuitButton { get; set; } = null!;

    public override void _Ready()
    {
        this.ValidateRequiredExports();
        GameSession.EnsureProfileLoaded();
        GameSession.LocalPvp = false;
        PlayButton.Pressed += () =>
            GetTree().ChangeSceneToFile("res://scenes/menu/level_select.tscn");
        PvpButton.Pressed += () =>
        {
            GameSession.LocalPvp = true;
            GetTree().ChangeSceneToFile("res://scenes/battle/battle_scene.tscn");
        };
        QuitButton.Pressed += () => GetTree().Quit();
    }
}
