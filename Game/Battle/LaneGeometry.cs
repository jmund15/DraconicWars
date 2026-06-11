namespace DraconicWars.Game.Battle;

using Godot;

/// <summary>
/// Maps sim lane coordinates (meters, x along the lane) to world pixels.
/// The sim is unit-agnostic; only the view knows about pixels.
/// </summary>
public static class LaneGeometry
{
    /// <summary>Pixels per sim lane meter. 38m lane ≈ 608px — one 640px screen wide.</summary>
    public const float PixelsPerMeter = 16f;

    /// <summary>Y of the ground line in world pixels (battle scene local space).</summary>
    public const float GroundY = 300f;

    /// <summary>Y of the air stratum in world pixels.</summary>
    public const float AirY = 252f;

    public static Vector2 ToWorld(float laneX, DraconicWars.Sim.Units.Stratum stratum)
    {
        var y = stratum == DraconicWars.Sim.Units.Stratum.Air ? AirY : GroundY;
        return new Vector2(laneX * PixelsPerMeter, y);
    }
}
