namespace DraconicWars.Tests.Logic.Campaign;

using System.IO;
using System.Text.Json;
using DraconicWars.Game.Content;
using GdUnit4;
using static GdUnit4.Assertions;

/// <summary>
/// Art/catalog contract: every shipped unit has a generated sheet + manifest, and
/// the manifest's combat ticks match the catalog (the importer hard-rejects drift —
/// this catches it at gate time instead of first scene load).
/// </summary>
[TestSuite]
public class RosterArtContractTest
{
    private static string UnitsDir()
    {
        var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "project.godot")))
        {
            dir = dir.Parent;
        }
        AssertThat(dir is not null)
            .OverrideFailureMessage("project root (project.godot) not found above test bin").IsTrue();
        return Path.Combine(dir!.FullName, "art_pipeline", "output", "units");
    }

    [TestCase]
    public void EveryRosterUnitHasSheetAndTickFaithfulManifest()
    {
        var unitsDir = UnitsDir();
        foreach (var def in UnitCatalog.FullRoster)
        {
            var sheet = Path.Combine(unitsDir, $"{def.Id}_sheet.png");
            var manifestPath = Path.Combine(unitsDir, $"{def.Id}.manifest.json");
            AssertThat(File.Exists(sheet))
                .OverrideFailureMessage($"{def.Id}: missing sheet {sheet}").IsTrue();
            AssertThat(File.Exists(manifestPath))
                .OverrideFailureMessage($"{def.Id}: missing manifest").IsTrue();

            using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var root = manifest.RootElement;
            AssertThat(root.GetProperty("foreswing_ticks").GetInt32())
                .OverrideFailureMessage($"{def.Id}: manifest foreswing != catalog")
                .IsEqual(def.ForeswingTicks);
            AssertThat(root.GetProperty("backswing_ticks").GetInt32())
                .OverrideFailureMessage($"{def.Id}: manifest backswing != catalog")
                .IsEqual(def.BackswingTicks);
        }
    }
}
