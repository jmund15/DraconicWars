namespace DraconicWars.Sim.Units;

/// <summary>
/// Immutable unit definition — the sim's single source of truth for stats AND combat
/// timing (art manifests are validated against ForeswingTicks/BackswingTicks at import).
/// </summary>
public sealed record UnitDef(
    string Id,
    string DisplayName,
    int Tier,
    TypeClass TypeClass,
    Element Element,
    int MaxHp,
    int Damage,
    int ForeswingTicks,
    int BackswingTicks,
    float Range,
    float RangeMin,
    bool IsArea,
    float MoveSpeed,
    int KnockbackCount,
    int DeployCost,
    int DeployCooldownTicks,
    Stratum Stratum,
    bool CanTargetGround,
    bool CanTargetAir)
{
    /// <summary>Collection rarity (decoupled from <see cref="Tier"/>): drives
    /// units-per-form, kit uniqueness, Sigil cost, and visual flair — never raw power or
    /// mana. Defaults to Common; Draconic is dragons only.</summary>
    public Rarity Rarity { get; init; } = Rarity.Common;

    /// <summary>Set when this def is an in-battle Rebreathing variant: the element the
    /// company was originally sworn to. Null means fighting under its native Breath.</summary>
    public Element? NativeElement { get; init; }

    /// <summary>Attack visual archetype: class (Physical|Magic) x pose x form. Defaults
    /// to physical-swing; the catalog overrides casters to Magic. The sim ignores this
    /// (hitscan); the view reads it to spawn a cosmetic element form. See
    /// arch-attack-archetypes.md §3.</summary>
    public AttackArchetype Attack { get; init; } = AttackArchetype.MeleePhysical;

    // Signature kits (Full Roster Batch): ONE data-driven mechanic per unit, all
    // defaulting off. The sim reads these; no per-unit code exists anywhere.

    /// <summary>Lobbed shots strike the FARTHEST enemy in band, arcing over screens.</summary>
    public bool PrefersFarthestTarget { get; init; }

    /// <summary>Anti-air interceptor: strikes the nearest AIR-stratum enemy in band when one
    /// is present, falling back to the normal nearest target otherwise (glide_manta).</summary>
    public bool PrefersAirTarget { get; init; }

    /// <summary>&gt;0: the unit cycles through this many ticks, going untargetable (phased,
    /// immune) for the last PhaseDurationTicks of every period — an evasive i-frame window
    /// it still acts through (spore_wisp). Begins solid on deploy.</summary>
    public int PhaseCadenceTicks { get; init; }

    public int PhaseDurationTicks { get; init; }

    /// <summary>&gt;0: rises once per life at this fraction of MaxHp. Every death —
    /// including the one it stands back up from — pays the killer in full.</summary>
    public float ReviveHpPct { get; init; }

    public int OnDeathBlastDamage { get; init; }

    public float OnDeathBlastRadius { get; init; }

    /// <summary>Per-tick damage to every enemy (both strata) within AuraRadius.</summary>
    public int AuraDamagePerTick { get; init; }

    public float AuraRadius { get; init; }

    /// <summary>Damage reduction accrued per second while in an attack phase; any
    /// forced displacement (knockback, shove, Wrath) wipes the accrual.</summary>
    public float VigilDrPerSecond { get; init; }

    public float VigilDrMaxPct { get; init; }

    /// <summary>&gt;0: contacts seed a lingering lane zone at the impact point.</summary>
    public float ZoneRadius { get; init; }

    public float ZoneSlowPct { get; init; }

    public int ZoneDurationTicks { get; init; }

    public int ZoneDamagePerTick { get; init; }

    /// <summary>&gt;0: enemies that accumulate this many ticks of dwell in / overlap with this
    /// unit's seeded zones freeze for ZoneFreezeTicks (Glacereth — "Stay a While").</summary>
    public int ZoneFreezeDwellTicks { get; init; }

    public int ZoneFreezeTicks { get; init; }

    /// <summary>&gt;0: each contact force-shoves all non-Unstaggerable enemies within
    /// ShoveRadius ahead of it backward (Wrath-style: no i-frames, interrupts).</summary>
    public float ShoveDistance { get; init; }

    public float ShoveRadius { get; init; }

    /// <summary>Heals for this fraction of damage dealt, capped at the victim's
    /// remaining HP (overkill pays nothing).</summary>
    public float LifestealPct { get; init; }

    /// <summary>Each resolved contact rings the next one this much harder (additive,
    /// capped at TollRampCap); a threshold knockback resets the accrual.</summary>
    public float TollRampPct { get; init; }

    public float TollRampCap { get; init; }

    /// <summary>Bonus damage fraction against targets at or above HighHpThreshold.</summary>
    public float BonusVsHighHpPct { get; init; }

    public int HighHpThreshold { get; init; }

    /// <summary>Combat knockback never moves or interrupts it; Wrath still does.</summary>
    public bool Unstaggerable { get; init; }

    /// <summary>&gt;0: allies within ShelterRadius take this fraction less incoming damage
    /// (a defensive lee). 0..1. frostbarge_cloudwhale's shelter aura.</summary>
    public float ShelterDrPct { get; init; }

    /// <summary>&gt;0: allies within ShelterRadius regenerate this much HP per tick.</summary>
    public int ShelterRegenPerTick { get; init; }

    public float ShelterRadius { get; init; }

    /// <summary>&gt;0: when this unit's Vigil DR-ramp reaches its cap, the charge releases as a
    /// lane shockwave dealing this much damage to every enemy within ShockwaveRange, then the
    /// ramp resets to charge again (Terravossk — "the wall hits back").</summary>
    public int ShockwaveDamage { get; init; }

    public float ShockwaveRange { get; init; }

    /// <summary>First contact per life skips the wind-up and hits this much harder.</summary>
    public float FirstStrikeBonusPct { get; init; }

    /// <summary>Translates this far PAST its target when an attack cycle completes.</summary>
    public float StrafeDistance { get; init; }

    /// <summary>&gt;0: each contact seizes the FRONTMOST non-Unstaggerable enemy in band
    /// (the one most advanced toward this unit's spire) and teleports it this far back
    /// toward its OWN spire, self-stunning it for GrabStunTicks (crag_tyrant's Roc grab —
    /// teleport + self-stun, no ownership link).</summary>
    public float GrabThrowDistance { get; init; }

    public int GrabStunTicks { get; init; }

    /// <summary>&gt;0: while alive this unit feeds its owner's wallet this much mana per
    /// second (tempest_choir's living conduit). Only ConduitContributeCap of them count.</summary>
    public float ConduitManaPerSecond { get; init; }

    public int ConduitContributeCap { get; init; }

    /// <summary>&gt;0: allies within HasteHaloRadius gain this attack-speed fraction (fed into
    /// the shared ScaledTicks channel — non-stacking, best halo wins). tempest_choir's halo.</summary>
    public float HasteHaloSpeedPct { get; init; }

    public float HasteHaloRadius { get; init; }

    /// <summary>&gt;0: attacks loose a real traveling projectile at this speed (units/tick)
    /// instead of resolving hitscan — body-blockable + dodgeable. Damage/element snapshot
    /// at spawn. See roster-expansion-40.md §5.</summary>
    public float ProjectileSpeed { get; init; }

    /// <summary>Projectile passes through and damages every enemy body in its path instead
    /// of stopping at the first.</summary>
    public bool ProjectilePierces { get; init; }

    /// <summary>&gt;0: a non-pierce projectile applies an AoE of this radius at its impact.</summary>
    public float ProjectileSplashRadius { get; init; }

    /// <summary>&gt;0: each enemy this unit's projectile KILLS refunds this much mana to its
    /// owner, capped at ManaRefundCapPerShot per shot (Voltherax — so a deep pierce can't
    /// refund unbounded). Snapshotted onto the projectile at spawn.</summary>
    public int ManaRefundPerKill { get; init; }

    public int ManaRefundCapPerShot { get; init; }

    /// <summary>&gt;0: each contact drains this much mana from the enemy player's wallet
    /// (the_tithe's siphon). See roster-expansion-40.md §5.</summary>
    public int DrainManaOnContact { get; init; }

    /// <summary>&gt;0: each contact sets the enemy's escrow-stall timer to at least this
    /// many ticks, freezing their Dragon-summoning channel (the_tithe).</summary>
    public int EscrowStallOnContact { get; init; }

    /// <summary>Non-null: this unit births SpawnDefId chaff every SpawnCadenceTicks at its
    /// own position, capped at SpawnCap live spawns on its side (sporekeep, Sythraal). Chaff
    /// carries no SpawnDefId, so it never recurses.</summary>
    public string? SpawnDefId { get; init; }

    public int SpawnCadenceTicks { get; init; }

    public int SpawnCap { get; init; }

    /// <summary>Counter declarations (roster-expansion-40.md §5): when this unit ATTACKS a
    /// defender whose effective element matches, MassiveVsElement deals x3 and StrongVsElement
    /// x1.5. ResistantVsElement is defensive: incoming damage of that element is x0.25.</summary>
    public Element? StrongVsElement { get; init; }

    public Element? MassiveVsElement { get; init; }

    public Element? ResistantVsElement { get; init; }

    /// <summary>Non-null: each contact overrides the target's DEFENSIVE element to this for
    /// OverrideTargetTicks, so allied counters land (mossmite's counter-flip).</summary>
    public Element? OverrideTargetElement { get; init; }

    public int OverrideTargetTicks { get; init; }

    /// <summary>&gt;0: each hit poisons the target (a damage-over-time for this many ticks at
    /// PoisonDamagePerTick). When a poisoned enemy dies it spreads to fresh neighbours within
    /// ContagionRadius (the contagion chain), each generation dealing
    /// 1 + depth*ContagionDepthBonus the base poison (Sythraal — "There's Always More").</summary>
    public int PoisonOnHitTicks { get; init; }

    public int PoisonDamagePerTick { get; init; }

    public float ContagionRadius { get; init; }

    public float ContagionDepthBonus { get; init; }
}
