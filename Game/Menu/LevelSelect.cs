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

    [Export, RequiredExport] public VBoxContainer UpgradeList { get; set; } = null!;

    public override void _Ready()
    {
        this.ValidateRequiredExports();
        GameSession.EnsureProfileLoaded();
        var profile = GameSession.Profile;
        EnsureStarterRoster(profile);

        RefreshHeader();
        BuildUpgradeRows();

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

    /// <summary>Fresh profiles own the FP roster at entry level (gift drip comes later).</summary>
    private static void EnsureStarterRoster(PlayerProfile profile)
    {
        foreach (var def in Content.UnitCatalog.FirstPlayable)
        {
            if (def.Tier < 4 && !profile.UnitLevels.ContainsKey(def.Id))
            {
                profile.UnitLevels[def.Id] = MetaProgression.EntryLevel(def.Tier);
            }
        }
    }

    private void RefreshHeader()
    {
        var profile = GameSession.Profile;
        GoldLabel.Text = $"Gold: {profile.Gold}";
        RankLabel.Text = $"Dragon Rank: {MetaProgression.DragonRank(profile)}"
            + $"  (cap Lv{MetaProgression.LevelCap(MetaProgression.DragonRank(profile))})";
    }

    private void BuildUpgradeRows()
    {
        var profile = GameSession.Profile;
        foreach (var def in Content.UnitCatalog.FirstPlayable)
        {
            if (def.Tier >= 4)
            {
                continue;
            }
            var row = new HBoxContainer();
            var label = new Label { CustomMinimumSize = new Vector2(170, 0) };
            var buy = new Button { CustomMinimumSize = new Vector2(80, 0) };
            row.AddChild(label);
            row.AddChild(buy);
            UpgradeList.AddChild(row);

            var defId = def.Id;
            void Refresh()
            {
                var unitLevel = profile.UnitLevels[defId];
                label.Text = $"{def.DisplayName}  Lv{unitLevel}";
                var atCap = unitLevel >= System.Math.Min(
                    MetaProgression.MaxLevel,
                    MetaProgression.LevelCap(MetaProgression.DragonRank(profile)));
                buy.Text = atCap ? "MAX" : $"+ {MetaProgression.CostForLevel(unitLevel + 1)}g";
                buy.Disabled = atCap
                    || profile.Gold < MetaProgression.CostForLevel(unitLevel + 1);
            }

            buy.Pressed += () =>
            {
                if (MetaProgression.TryBuyLevel(profile, defId))
                {
                    GameSession.SaveProfile();
                    RefreshHeader();
                    RefreshAllRows();
                }
            };
            _rowRefreshers.Add(Refresh);
            Refresh();
        }
    }

    private void RefreshAllRows()
    {
        foreach (var refresh in _rowRefreshers)
        {
            refresh();
        }
    }

    private readonly System.Collections.Generic.List<System.Action> _rowRefreshers = new();
}
