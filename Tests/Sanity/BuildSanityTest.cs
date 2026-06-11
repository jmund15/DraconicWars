namespace DraconicWars.Tests.Sanity;

using GdUnit4;
using static GdUnit4.Assertions;

[TestSuite]
public class BuildSanityTest
{
    [TestCase]
    public void JmodotCompilesIntoGameAssembly()
    {
        AssertThat(typeof(Jmodot.Implementation.Shared.JmoLogger).Assembly.GetName().Name)
            .IsEqual("DraconicWars");
    }
}
