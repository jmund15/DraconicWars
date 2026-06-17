namespace DraconicWars.Game.Battle;

using System.IO;
using System.Linq;
using DraconicWars.Game.Content;
using DraconicWars.Sim.Battle;
using Godot;
using Jmodot.Core.Shared.Attributes;
using Jmodot.Implementation.Shared;

/// <summary>
/// Standalone screenshot harness: renders one unit sheet over the battle backdrop with the
/// live signature-VFX layer (emissive bloom + ember aura + boss halo), captures the viewport
/// to a PNG, and quits. View-only — constructs a SimUnit purely to drive UnitView.Bind; it
/// never runs the sim. Parameterized by env vars so one scene serves every A/B/C variant.
/// </summary>
public partial class UnitPreviewController : Node2D
{
    // ~1s of frames lets the ember CpuParticles2D fill and the glow settle. The documented
    // GetTexture() note warns the viewport image is black/outdated if read too early (e.g. in _Ready).
    private const int WarmupFrames = 60;

    [Export, RequiredExport] public Node2D UnitLayer { get; set; } = null!;

    public override async void _Ready()
    {
        this.ValidateRequiredExports();

        var defId = Env("PREVIEW_DEF", "pyraxis");
        var dir = Env("PREVIEW_DIR", "res://art_pipeline/output/units");
        var sheet = Env("PREVIEW_SHEET", "pyraxis");
        var outArg = Env("PREVIEW_OUT", $"res://art_pipeline/output/previews/{sheet}_preview.png");

        // Real pyraxis def carries Element=Fire + Tier=4 -> boss halo + correct timing identity.
        var def = UnitCatalog.FullRoster.FirstOrDefault(d => d.Id == defId);
        if (def is null)
        {
            JmoLogger.Error(this, $"[Preview] unknown def '{defId}'");
            GetTree().Quit();
            return;
        }

        // validateTiming:false — a screenshot harness judges art, not combat ticks; a future
        // re-bake that drifts foreswing/backswing must not silently null-out the capture.
        var frames = new UnitSpriteLibrary(dir).Load(def, sheet, validateTiming: false);
        if (frames is null)
        {
            JmoLogger.Error(this, $"[Preview] failed to load sheet '{sheet}' from {dir}");
            GetTree().Quit();
            return;
        }

        // Air stratum -> ToWorld(20, Air) = (320, 252): horizontally centered in the 640px
        // viewport, feet on the air line. Static X + full HP -> UnitView holds the idle pose.
        var unit = new SimUnit
        {
            InstanceId = 1,
            Def = def,
            Side = PlayerSide.Left,
            Hp = def.MaxHp,
            X = 20f,
        };

        var view = new UnitView();
        UnitLayer.AddChild(view);
        view.Bind(unit, frames); // builds AnimatedSprite2D, feet-anchors, ApplySignatureVfx(Fire, 4)

        for (var i = 0; i < WarmupFrames; i++)
        {
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        }

        var image = GetViewport().GetTexture().GetImage();
        // hdr_2d=true => the viewport texture is HDR linear-encoded. Convert to 8-bit and apply the
        // sRGB OETF so the PNG matches on-screen tone. Identical pass on all three variants keeps
        // the A/B/C comparison fair regardless of absolute-gamma quibbles.
        if (image.GetFormat() != Image.Format.Rgba8 && image.GetFormat() != Image.Format.Rgb8)
        {
            image.Convert(Image.Format.Rgba8);
            image.LinearToSrgb();
        }

        var outPath = ProjectSettings.GlobalizePath(outArg);
        var outDir = Path.GetDirectoryName(outPath);
        if (!string.IsNullOrEmpty(outDir))
        {
            Directory.CreateDirectory(outDir);
        }
        var err = image.SavePng(outPath);
        JmoLogger.Info(this, $"[Preview] {sheet}: SavePng({outPath}) -> {err}");

        GetTree().Quit();
    }

    private static string Env(string key, string fallback)
    {
        var value = OS.GetEnvironment(key);
        return string.IsNullOrEmpty(value) ? fallback : value;
    }
}
