namespace DraconicWars.Tests.Sanity.Battle;

using DraconicWars.Game.Battle.Vfx;
using DraconicWars.Sim.Units;
using GdUnit4;
using static GdUnit4.Assertions;

[TestSuite]
[RequireGodotRuntime]
public class SignatureVfxProfileSanityTest
{
    [TestCase]
    public void EveryElementHasALoadableProfileWithEmissiveColors()
    {
        foreach (Element element in System.Enum.GetValues<Element>())
        {
            var profile = SignatureVfxProfiles.For(element);
            AssertThat(profile)
                .OverrideFailureMessage($"{element} has no signature VFX profile (.tres missing or broken)")
                .IsNotNull();
            AssertThat(profile!.EmissiveColors.Length > 0)
                .OverrideFailureMessage($"{element} profile has no emissive colors")
                .IsTrue();
        }
    }
}
