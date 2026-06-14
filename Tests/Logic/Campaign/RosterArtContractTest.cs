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
    private static string ProjectRoot()
    {
        var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "project.godot")))
        {
            dir = dir.Parent;
        }
        AssertThat(dir is not null)
            .OverrideFailureMessage("project root (project.godot) not found above test bin").IsTrue();
        return dir!.FullName;
    }

    private static string UnitsDir() =>
        Path.Combine(ProjectRoot(), "art_pipeline", "output", "units");

    // distinctness report lives in output/reports/, NOT output/units/.
    private static string ReportsDir() =>
        Path.Combine(ProjectRoot(), "art_pipeline", "output", "reports");

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

    /// <summary>
    /// Silhouette shape-language gate: the roster's body silhouettes must be
    /// pairwise-distinct (no "same rig recolored"). The Python art pipeline
    /// (run_roster_batch.py) computes body-mask IoU + the dragon scale-gap +
    /// per-archetype head-to-body ratio and writes roster_distinctness.json;
    /// this asserts the verdict at gate time.
    /// </summary>
    [TestCase]
    public void RosterSilhouettesAreDistinct()
    {
        var reportPath = Path.Combine(ReportsDir(), "roster_distinctness.json");
        AssertThat(File.Exists(reportPath))
            .OverrideFailureMessage(
                $"missing {reportPath} — run `python art_pipeline/run_roster_batch.py`")
            .IsTrue();

        using var doc = JsonDocument.Parse(File.ReadAllText(reportPath));
        var root = doc.RootElement;

        // surface offenders from EVERY failing check (silhouette_distinctness,
        // cross_form_distinctness, head_to_body, ...) so any regression is
        // diagnosable from the gate output without re-running the pipeline. The
        // top-level `passed` is the AND over checks; enumerating only one check's
        // offenders would print a blank list for a different check's failure.
        var offenders = new System.Text.StringBuilder();
        foreach (var check in root.GetProperty("checks").EnumerateObject())
        {
            if (check.Value.GetProperty("passed").GetBoolean())
            {
                continue;
            }
            offenders.Append($"\n[{check.Name}]");
            if (!check.Value.TryGetProperty("offenders", out var offArr))
            {
                continue;
            }
            foreach (var off in offArr.EnumerateArray())
            {
                if (off.TryGetProperty("pair", out var pair))
                {
                    offenders.Append($"\n  {pair[0].GetString()} ~ {pair[1].GetString()}");
                    if (off.TryGetProperty("iou", out var iouEl))
                    {
                        offenders.Append($" iou={iouEl.GetDouble()}");
                    }
                }
                else if (off.TryGetProperty("name", out var nm))
                {
                    offenders.Append($"\n  {nm.GetString()}");
                }
            }
        }

        AssertThat(root.GetProperty("passed").GetBoolean())
            .OverrideFailureMessage(
                "roster distinctness failed (same shape, two names). Offending checks/pairs:"
                + offenders)
            .IsTrue();
    }
}
