namespace DraconicWars.Game.Battle.Vfx;

using System.Collections.Generic;
using DraconicWars.Sim.Units;
using Godot;
using Jmodot.Implementation.Shared;

/// <summary>Loads + caches the per-element signature VFX profile (.tres) by convention
/// path (<c>res://assets/vfx/signature_{element}.tres</c>). View-layer only.</summary>
public static class SignatureVfxProfiles
{
    private static readonly Dictionary<Element, SignatureVfxProfile?> Cache = new();

    public static SignatureVfxProfile? For(Element element)
    {
        if (Cache.TryGetValue(element, out var cached))
        {
            return cached;
        }

        var path = $"res://assets/vfx/signature_{element.ToString().ToLowerInvariant()}.tres";
        var profile = ResourceLoader.Exists(path) ? GD.Load<SignatureVfxProfile>(path) : null;
        if (profile is null)
        {
            JmoLogger.Warning(typeof(SignatureVfxProfiles), $"[SignatureVfx] No profile at {path}");
        }

        Cache[element] = profile;
        return profile;
    }
}
