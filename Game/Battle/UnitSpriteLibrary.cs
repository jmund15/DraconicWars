namespace DraconicWars.Game.Battle;

using System.Collections.Generic;
using System.Text.Json;
using DraconicWars.Sim.Units;
using Godot;
using Jmodot.Implementation.Shared;

/// <summary>
/// Builds SpriteFrames at runtime from a generated sheet PNG + JSON manifest.
/// Enforces the sim-owns-timing contract (design.md §3): the manifest's echoed
/// foreswing/backswing ticks must match the UnitDef or loading FAILS loudly.
/// Runtime building (vs baked .tres) keeps the generate→play loop import-free.
/// </summary>
public sealed class UnitSpriteLibrary
{
    private readonly string _rootDir;
    private readonly Dictionary<string, SpriteFrames> _cache = new();

    public UnitSpriteLibrary(string rootDir)
    {
        _rootDir = rootDir;
    }

    public SpriteFrames? Load(UnitDef def, string sheetName)
    {
        if (_cache.TryGetValue(sheetName, out var cached))
        {
            return cached;
        }

        var sheetPath = $"{_rootDir}/{sheetName}_sheet.png";
        var manifestPath = $"{_rootDir}/{sheetName}.manifest.json";
        if (!FileAccess.FileExists(sheetPath) || !FileAccess.FileExists(manifestPath))
        {
            JmoLogger.Error(this, $"[UnitSprites] Missing sheet or manifest for '{sheetName}'");
            return null;
        }

        var manifest = JsonSerializer.Deserialize<SheetManifest>(
            FileAccess.GetFileAsString(manifestPath),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
        if (manifest is null || manifest.Animations.Count == 0)
        {
            JmoLogger.Error(this, $"[UnitSprites] Manifest for '{sheetName}' is empty");
            return null;
        }
        if (manifest.ForeswingTicks != def.ForeswingTicks
            || manifest.BackswingTicks != def.BackswingTicks)
        {
            JmoLogger.Error(this,
                $"[UnitSprites] Timing drift for '{sheetName}': manifest "
                + $"{manifest.ForeswingTicks}/{manifest.BackswingTicks} vs def "
                + $"{def.ForeswingTicks}/{def.BackswingTicks} — regenerate the sheet");
            return null;
        }

        var image = Image.LoadFromFile(sheetPath);
        var texture = ImageTexture.CreateFromImage(image);
        var frames = new SpriteFrames();
        foreach (var animation in manifest.Animations)
        {
            var animName = new StringName(animation.Name);
            frames.AddAnimation(animName);
            frames.SetAnimationSpeed(animName, animation.Fps);
            frames.SetAnimationLoop(animName, animation.Loop);
            for (var i = 0; i < animation.Frames; i++)
            {
                var atlas = new AtlasTexture
                {
                    Atlas = texture,
                    Region = new Rect2(
                        i * manifest.FrameW, animation.Row * manifest.FrameH,
                        manifest.FrameW, manifest.FrameH),
                };
                frames.AddFrame(animName, atlas);
            }
        }
        frames.RemoveAnimation("default");

        _cache[sheetName] = frames;
        return frames;
    }

    public sealed class SheetManifest
    {
        public int FrameW { get; set; }

        public int FrameH { get; set; }

        public int ForeswingTicks { get; set; }

        public int BackswingTicks { get; set; }

        public List<ManifestAnimation> Animations { get; set; } = new();
    }

    public sealed class ManifestAnimation
    {
        public string Name { get; set; } = string.Empty;

        public int Row { get; set; }

        public int Frames { get; set; }

        public float Fps { get; set; }

        public bool Loop { get; set; }

        public int? ContactFrame { get; set; }
    }
}
