using NUnit.Framework;
using TrumpLab;

namespace TrumpLab.Tests
{
    public sealed class DotNetHarnessTests
    {
        [Test]
        public void BuiltInRegistryIsLoadedByDotNetHarness()
        {
            Assert.That(BuiltInGames.Registry.All(), Has.Count.EqualTo(92));
        }
    }
}
