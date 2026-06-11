namespace DraconicWars.Game.Menu;

using DraconicWars.Game.Campaign;
using DraconicWars.Meta;
using Godot;
using Jmodot.Core.Shared.Attributes;

public partial class LevelSelect : Control
{
    [Export, RequiredExport] public VBoxContainer LevelList { get; set; } = null!;

    [Export, RequiredExport] public Label GoldLabel { get; set; } = null!;

    [Export, RequiredExport] public Label RankLabel { get; set; } = null!;

    [Export, RequiredExport] public Button BackButton { get; set; } = null!;

    public override void _Ready()
    {
        this.ValidateRequiredExports();
        GameSession.EnsureProfileLoaded();
        var profile = GameSession.Profile;

        GoldLabel.Text = $"Gold: {profile.Gold}";
        RankLabel.Text = $"Dragon Rank: {MetaProgression.DragonRank(profile)}";

        for (var i = 0; i < CampaignCatalog.Levels.Count; i++)
        {
            var level = CampaignCatalog.Levels[i];
            var unlocked = CampaignProgress.IsUnlocked(profile, i);
            var cleared = profile.ClearedLevelIds.Contains(level.Id);
            var button = new Button
            {
                Text = $"{i + 1}. {level.DisplayName}{(cleared ? "  ✓" : string.Empty)}",
                Disabled = !unlocked,
                CustomMinimumSize = new Vector2(220, 28),
            };
            var levelIndex = i;
            button.Pressed += () =>
            {
                GameSession.SelectedLevelIndex = levelIndex;
                GetTree().ChangeSceneToFile("res://scenes/battle/battle_scene.tscn");
            };
            LevelList.AddChild(button);
        }

        BackButton.Pressed += () =>
            GetTree().ChangeSceneToFile("res://scenes/menu/main_menu.tscn");
    }
}
