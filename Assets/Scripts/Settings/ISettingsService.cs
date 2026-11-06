namespace Nexus.Settings
{
    /// <summary>User facing, persisted settings (quality and audio in Phase 01).</summary>
    public interface ISettingsService
    {
        int QualityLevel { get; }

        int QualityLevelCount { get; }

        string QualityLevelName { get; }

        float MasterVolume { get; }

        void SetQualityLevel(int level);

        void CycleQuality(int direction);

        void SetMasterVolume(float volume);

        void AdjustMasterVolume(float delta);

        void ApplyAll();
    }
}
