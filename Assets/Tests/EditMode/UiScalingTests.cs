using NUnit.Framework;
using Nexus.UI;

namespace Nexus.Tests.EditMode
{
    public class UiScalingTests
    {
        [Test]
        public void PortraitAspect_MatchesWidth()
        {
            Assert.AreEqual(0f, ResponsiveCanvas.ComputeMatchFactor(0.56f), 0.0001f);
            Assert.AreEqual(0f, ResponsiveCanvas.ComputeMatchFactor(0.75f), 0.0001f);
        }

        [Test]
        public void StandardLandscapeAspect_UsesBalancedMatch()
        {
            Assert.AreEqual(0.5f, ResponsiveCanvas.ComputeMatchFactor(16f / 9f), 0.0001f);
        }

        [Test]
        public void WideDesktopAspect_PrefersHeightReference()
        {
            Assert.AreEqual(1f, ResponsiveCanvas.ComputeMatchFactor(21f / 9f), 0.0001f);
            Assert.AreEqual(1f, ResponsiveCanvas.ComputeMatchFactor(32f / 9f), 0.0001f);
        }

        [Test]
        public void SquareAspect_UsesCompactMatch()
        {
            Assert.AreEqual(0.4f, ResponsiveCanvas.ComputeMatchFactor(1.2f), 0.0001f);
        }

        [Test]
        public void InvalidAspect_FallsBackToBalancedMatch()
        {
            Assert.AreEqual(0.5f, ResponsiveCanvas.ComputeMatchFactor(0f), 0.0001f);
            Assert.AreEqual(0.5f, ResponsiveCanvas.ComputeMatchFactor(-1f), 0.0001f);
        }

        [Test]
        public void ReferenceResolutions_MatchDesignTargets()
        {
            Assert.AreEqual(1920f, ResponsiveCanvas.LandscapeReference.x, 0.001f);
            Assert.AreEqual(1080f, ResponsiveCanvas.LandscapeReference.y, 0.001f);
            Assert.AreEqual(1080f, ResponsiveCanvas.PortraitReference.x, 0.001f);
            Assert.AreEqual(1920f, ResponsiveCanvas.PortraitReference.y, 0.001f);
        }
    }
}
