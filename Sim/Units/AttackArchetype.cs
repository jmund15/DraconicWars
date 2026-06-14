namespace DraconicWars.Sim.Units;

/// <summary>Whether a unit attacks with a weapon already on its sprite, or conjures
/// a detached form.</summary>
public enum AttackClass { Physical, Magic }

/// <summary>How the body animates the attack. Physical poses move the held weapon;
/// magic poses (Cast/Channel) gesture, BodyStrike lunges weaponlessly.</summary>
public enum AttackPose { Swing, Thrust, Cast, Channel, BodyStrike }

/// <summary>The conjured form a Magic attack spawns. None = no form (physical, or a
/// pure gesture). Shapes are baked per element by the Element-Form-Core Generator.</summary>
public enum AttackForm { None, Shard, Ball, Chunk, Bolt }

/// <summary>
/// Decouples a unit's attack VISUAL from its body-plan: class (Physical|Magic) x pose
/// x form. The sim stays hitscan (damage lands at the foreswing tick); a Magic
/// attack with a non-None form additionally spawns a cosmetic view-layer element form
/// that flies caster-&gt;target. See arch-attack-archetypes.md §3.
/// </summary>
public sealed record AttackArchetype(AttackClass Class, AttackPose Pose, AttackForm Form)
{
    /// <summary>The physical-swing default — every legacy unit keeps today's behavior
    /// (held weapon, no spawned form) unless the catalog overrides it.</summary>
    public static readonly AttackArchetype MeleePhysical =
        new(AttackClass.Physical, AttackPose.Swing, AttackForm.None);

    /// <summary>Only Magic + a non-None form spawns a view form; everything else is
    /// pose-only (physical weapon swing, or a formless gesture).</summary>
    public bool ProducesForm => Class == AttackClass.Magic && Form != AttackForm.None;
}
