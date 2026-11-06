using NUnit.Framework;
using Nexus.Core.Platform;
using UnityEngine;

namespace Nexus.Tests.EditMode
{
    public class PlatformTests
    {
        [Test]
        public void DesktopRuntime_IsClassifiedAsDesktop()
        {
            Assert.AreEqual(
                PlatformKind.WindowsDesktop,
                PlatformInfo.Classify(RuntimePlatform.WindowsPlayer, UnityEngine.DeviceType.Desktop, 24f));

            Assert.AreEqual(
                PlatformKind.MacDesktop,
                PlatformInfo.Classify(RuntimePlatform.OSXPlayer, UnityEngine.DeviceType.Desktop, 27f));

            Assert.AreEqual(
                PlatformKind.LinuxDesktop,
                PlatformInfo.Classify(RuntimePlatform.LinuxPlayer, UnityEngine.DeviceType.Desktop, 24f));
        }

        [Test]
        public void SmallAndroidDevice_IsClassifiedAsPhone()
        {
            PlatformKind kind = PlatformInfo.Classify(RuntimePlatform.Android, UnityEngine.DeviceType.Handheld, 2.7f);
            Assert.AreEqual(PlatformKind.AndroidPhone, kind);
        }

        [Test]
        public void LargeAndroidDevice_IsClassifiedAsTablet()
        {
            Assert.AreEqual(
                PlatformKind.AndroidTablet,
                PlatformInfo.Classify(RuntimePlatform.Android, UnityEngine.DeviceType.Handheld, 7.5f));

            Assert.AreEqual(
                PlatformKind.AndroidTablet,
                PlatformInfo.Classify(RuntimePlatform.Android, UnityEngine.DeviceType.Handheld, 8.4f));
        }

        [Test]
        public void IosDevice_IsClassifiedBySize()
        {
            Assert.AreEqual(
                PlatformKind.IosPhone,
                PlatformInfo.Classify(RuntimePlatform.IPhonePlayer, UnityEngine.DeviceType.Handheld, 2.8f));

            Assert.AreEqual(
                PlatformKind.IosTablet,
                PlatformInfo.Classify(RuntimePlatform.IPhonePlayer, UnityEngine.DeviceType.Handheld, 7.9f));
        }

        [Test]
        public void UnknownRuntime_IsUnknown()
        {
            Assert.AreEqual(
                PlatformKind.Unknown,
                PlatformInfo.Classify(RuntimePlatform.WebGLPlayer, UnityEngine.DeviceType.Unknown, 0f));
        }

        [Test]
        public void PlatformInfo_DerivesCapabilitiesFromKind()
        {
            PlatformInfo phone = new PlatformInfo(PlatformKind.AndroidPhone, 420f, 6.1f);
            Assert.IsTrue(phone.IsMobile);
            Assert.IsFalse(phone.IsTablet);
            Assert.IsFalse(phone.SupportsQuit);
            Assert.AreEqual(UiLayoutProfile.Compact, phone.Layout);

            PlatformInfo tablet = new PlatformInfo(PlatformKind.IosTablet, 264f, 10.2f);
            Assert.IsTrue(tablet.IsTablet);
            Assert.AreEqual(UiLayoutProfile.Regular, tablet.Layout);

            PlatformInfo desktop = new PlatformInfo(PlatformKind.WindowsDesktop, 96f, 24f);
            Assert.IsFalse(desktop.IsMobile);
            Assert.IsTrue(desktop.SupportsQuit);
            Assert.AreEqual(UiLayoutProfile.Expanded, desktop.Layout);
        }

        [Test]
        public void PlatformConfiguration_TargetsFrameRateAndQualityPerDevice()
        {
            PlatformConfiguration phone = PlatformConfiguration.For(new PlatformInfo(PlatformKind.AndroidPhone, 420f, 6f));
            Assert.AreEqual(60, phone.TargetFrameRate);
            Assert.AreEqual(0, phone.RecommendedQualityLevel);
            Assert.AreEqual(UiLayoutProfile.Compact, phone.Layout);

            PlatformConfiguration tablet = PlatformConfiguration.For(new PlatformInfo(PlatformKind.AndroidTablet, 264f, 10f));
            Assert.AreEqual(60, tablet.TargetFrameRate);
            Assert.AreEqual(0, tablet.RecommendedQualityLevel);

            PlatformConfiguration desktop = PlatformConfiguration.For(new PlatformInfo(PlatformKind.WindowsDesktop, 96f, 24f));
            Assert.AreEqual(120, desktop.TargetFrameRate);
            Assert.AreEqual(1, desktop.RecommendedQualityLevel);
            Assert.AreEqual(UiLayoutProfile.Expanded, desktop.Layout);
        }

        [Test]
        public void PlatformConfiguration_HandlesNullPlatform()
        {
            PlatformConfiguration configuration = PlatformConfiguration.For(null);

            Assert.IsNotNull(configuration);
            Assert.Greater(configuration.TargetFrameRate, 0);
        }
    }
}
