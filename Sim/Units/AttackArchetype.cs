namespace DraconicWars.Sim.Units;

/// <summary>Whether a unit attacks with a weapon already on its sprite, or conjures
/// a detached form.</summary>
public enum AttackClass { Physical, Magic }

/// <summary>How the body animates the attack. Physical poses move the held weapon
/// (Swing/Thrust melee, Shoot looses a bow/crossbow); magic poses (Cast/Channel)
/// gesture, BodyStrike lunges weaponlessly.</summary>
public enum AttackPose { Swing, Thrust, Shoot, Cast, Channel, BodyStrike }

/// <summary>The form an attack spawns as it lands. None = no form (a melee swing or a
/// formless gesture). Magic forms (Shard/Ball/Chunk/Bolt) and the physical Arrow are
/// baked per element by the Element-Form-Core Generator.</summary>
public enum AttackForm { None, Shard, Ball, Chunk, Bolt, Arrow }

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

    /// <summary>Any non-None form spawns a view form: a magic conjuration (Ball/Shard/
    /// ...) OR a physical projectile (Arrow). A melee swing or formless gesture has
    /// Form=None and spawns nothing. The class drives the POSE, not whether a form
    /// flies — so physical archers visibly loose arrows.</summary>
    public bool ProducesForm => Form != AttackForm.None;
}
