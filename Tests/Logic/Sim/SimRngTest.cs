namespace DraconicWars.Tests.Logic.Sim;

using DraconicWars.Sim.Core;
using GdUnit4;
using static GdUnit4.Assertions;

[TestSuite]
public class SimRngTest
{
    [TestCase]
    public void SameSeed_ProducesIdenticalSequence()
    {
        var a = new SimRng(12345UL);
        var b = new SimRng(12345UL);
        for (var i = 0; i < 100; i++)
        {
            AssertThat(a.NextULong()).IsEqual(b.NextULong());
        }
    }

    [TestCase]
    public void DifferentSeeds_Diverge()
    {
        var a = new SimRng(1UL);
        var b = new SimRng(2UL);
        var anyDifferent = false;
        for (var i = 0; i < 10; i++)
        {
            if (a.NextULong() != b.NextULong())
            {
                anyDifferent = true;
            }
        }
        AssertThat(anyDifferent).IsTrue();
    }

    [TestCase]
    public void ZeroSeed_StillProducesNonDegenerateSequence()
    {
        var rng = new SimRng(0UL);
        var first = rng.NextULong();
        var second = rng.NextULong();
        AssertThat(first == 0UL && second == 0UL).IsFalse();
    }

    [TestCase]
    public void NextFloat_AlwaysInZeroToOneExclusive()
    {
        var rng = new SimRng(99UL);
        for (var i = 0; i < 1000; i++)
        {
            var f = rng.NextFloat();
            AssertThat(f >= 0f && f < 1f).IsTrue();
        }
    }

    [TestCase]
    public void NextInt_RespectsExclusiveUpperBound()
    {
        var rng = new SimRng(7UL);
        for (var i = 0; i < 1000; i++)
        {
            var v = rng.NextInt(5);
            AssertThat(v >= 0 && v < 5).IsTrue();
        }
    }

    [TestCase]
    public void DeriveChild_IsDeterministicPerStreamName()
    {
        var childA1 = new SimRng(42UL).DeriveChild("waves").NextULong();
        var childA2 = new SimRng(42UL).DeriveChild("waves").NextULong();
        var childB = new SimRng(42UL).DeriveChild("augments").NextULong();

        AssertThat(childA1).IsEqual(childA2);
        AssertThat(childA1 == childB).IsFalse();
    }

    [TestCase]
    public void DeriveChild_UnaffectedByParentConsumption()
    {
        var parentFresh = new SimRng(42UL);
        var childFromFresh = parentFresh.DeriveChild("waves").NextULong();

        var parentConsumed = new SimRng(42UL);
        parentConsumed.NextULong();
        parentConsumed.NextULong();
        var childFromConsumed = parentConsumed.DeriveChild("waves").NextULong();

        AssertThat(childFromFresh).IsEqual(childFromConsumed);
    }
}
