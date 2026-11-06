using Nexus.Core.Events;
using Nexus.Core.Logging;
using Nexus.Core.Platform;
using UnityEngine;

namespace Nexus.Settings
{
    /// <summary>
    /// Persists user preferences with PlayerPrefs and applies them to the engine.
    /// The service owns all quality/audio mutations so no other system touches
    /// QualitySettings directly.
    /// </summary>
    public sealed class SettingsService : ISettingsService
    {
        private const string QualityKey = "nexus.settings.quality";
        private const string VolumeKey = "nexus.settings.masterVolume";
        private const float DefaultVolume = 0.8f;

        private readonly IEventBus _eventBus;
        private readonly int _defaultQualityLevel;

        public int QualityLevel { get; private set; }

        public int QualityLevelCount
        {
            get { return Mathf.Max(1, QualitySettings.names.Length); }
        }

        public string QualityLevelName
        {
            get
            {
                string[] names = QualitySettings.names;
                if (names == null || names.Length == 0)
                {
                    return "Default";
                }

                int index = Mathf.Clamp(QualityLevel, 0, names.Length - 1);
                return names[index];
            }
        }

        public float MasterVolume { get; private set; }

        public SettingsService(IEventBus eventBus, PlatformConfiguration platformConfiguration)
        {
            _eventBus = eventBus;
            _defaultQualityLevel = platformConfiguration != null ? platformConfiguration.RecommendedQualityLevel : 0;

            QualityLevel = ClampQuality(PlayerPrefs.GetInt(QualityKey, _defaultQualityLevel));
            MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, DefaultVolume));
        }

        public void SetQualityLevel(int level)
        {
            QualityLevel = ClampQuality(level);
            QualitySettings.SetQualityLevel(QualityLevel, true);
            PlayerPrefs.SetInt(QualityKey, QualityLevel);
            PublishChanged();
        }

        public void CycleQuality(int direction)
        {
            int count = QualityLevelCount;
            if (count <= 1)
            {
                return;
            }

            int next = (QualityLevel + (direction >= 0 ? 1 : -1) + count) % count;
            SetQualityLevel(next);
        }

        public void SetMasterVolume(float volume)
        {
            MasterVolume = Mathf.Clamp01(volume);
            AudioListener.volume = MasterVolume;
            PlayerPrefs.SetFloat(VolumeKey, MasterVolume);
            PublishChanged();
        }

        public void AdjustMasterVolume(float delta)
        {
            SetMasterVolume(MasterVolume + delta);
        }

        public void ApplyAll()
        {
            QualitySettings.SetQualityLevel(ClampQuality(QualityLevel), true);
            AudioListener.volume = MasterVolume;
            PlayerPrefs.Save();
            GameLog.Info("Settings applied: quality '" + QualityLevelName + "', volume " + MasterVolume.ToString("0.00") + ".");
        }

        private int ClampQuality(int level)
        {
            return Mathf.Clamp(level, 0, QualityLevelCount - 1);
        }

        private void PublishChanged()
        {
            PlayerPrefs.Save();

            if (_eventBus != null)
            {
                _eventBus.Publish(new SettingsChangedEvent(QualityLevel, MasterVolume));
            }
        }
    }
}
