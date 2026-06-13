namespace DraconicWars.Tests.Logic.Battle.Vfx;

using DraconicWars.Game.Battle.Vfx;
using GdUnit4;
using static GdUnit4.Assertions;
using Cfg = DraconicWars.Game.Battle.Vfx.SignatureVfxResolver.ResolverConfig;

[TestSuite]
public class SignatureVfxResolverTest
{
    private static Cfg Sample()
    {
        return new Cfg(
            BaseLightEnergy: 0.8f,
            BaseAuraDensity: 6f,
            BaseEmissiveBoost: 1.4f,
            PerTierGain: 0.25f,
            MinTierForLight: 3);
    }

    [TestCase]
    public void Tier1IsTheUnscaledBaseline()
    {
        var c = Sample();
        var r = SignatureVfxResolver.Resolve(1, c);
        AssertThat(System.Math.Abs(r.LightEnergy - c.BaseLightEnergy) < 1e-4f).IsTrue();
        AssertThat(System.Math.Abs(r.AuraDensity - c.BaseAuraDensity) < 1e-4f).IsTrue();
        AssertThat(System.Math.Abs(r.EmissiveBoost - c.BaseEmissiveBoost) < 1e-4f).IsTrue();
    }

    [TestCase]
    public void IntensityScalesMonotonicallyWithTier()
    {
        var c = Sample();
        var t2 = SignatureVfxResolver.Resolve(2, c);
        var t4 = SignatureVfxResolver.Resolve(4, c);
        AssertThat(t4.LightEnergy > t2.LightEnergy).IsTrue();
        AssertThat(t4.AuraDensity > t2.AuraDensity).IsTrue();
        AssertThat(t4.EmissiveBoost > t2.EmissiveBoost).IsTrue();
    }

    [TestCase]
    public void LightSpawnsOnlyAtOrAboveTheThreshold()
    {
        var c = Sample(); // MinTierForLight = 3
        AssertThat(SignatureVfxResolver.Resolve(2, c).SpawnLight).IsFalse();
        AssertThat(SignatureVfxResolver.Resolve(3, c).SpawnLight).IsTrue();
        AssertThat(SignatureVfxResolver.Resolve(4, c).SpawnLight).IsTrue();
    }
}
