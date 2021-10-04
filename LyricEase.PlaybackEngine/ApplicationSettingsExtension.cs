using LyricEase.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace LyricEase
{
    internal static class ApplicationSettingsExtension
    {
        private static ApplicationDataContainer RootContainer => ApplicationData.Current.LocalSettings.CreateContainer("DoxPlayerSettings", ApplicationDataCreateDisposition.Always);

        #region Settings Items

        public static PlaybackMode PlaybackMode
        {
            get => (PlaybackMode)PlaybackModeProperty.GetValue();
            set => PlaybackModeProperty.SetValue((int)value);
        }
        private static readonly SettingProperty<int> PlaybackModeProperty = new(nameof(PlaybackMode), 0);

        public static double Volume
        {
            get => VolumeProperty.GetValue();
            set => VolumeProperty.SetValue(value);
        }
        private static readonly SettingProperty<double> VolumeProperty = new(nameof(Volume), 1.0d);
        
        public static SoundQuality SoundQuality
        {
            get => (SoundQuality)SoundQualityProperty.GetValue();
            set => SoundQualityProperty.SetValue((int)value);
        }
        private static readonly SettingProperty<int> SoundQualityProperty = new(nameof(SoundQuality), 2);
        
        public static bool IsDirectSwitchEnabled
        {
            get => IsDirectSwitchEnabledProperty.GetValue();
            set => IsDirectSwitchEnabledProperty.SetValue(value);
        }
        private static readonly SettingProperty<bool> IsDirectSwitchEnabledProperty = new(nameof(IsDirectSwitchEnabled), false);

        public static bool IsAudioCrossfadingEnabled
        {
            get => IsAudioCrossfadingEnabledProperty.GetValue();
            set => IsAudioCrossfadingEnabledProperty.SetValue(value);
        }
        private static readonly SettingProperty<bool> IsAudioCrossfadingEnabledProperty = new(nameof(IsAudioCrossfadingEnabled), true);
        
        public static int AudioCrossfadingLength
        {
            get => AudioCrossfadingLengthProperty.GetValue();
            set => AudioCrossfadingLengthProperty.SetValue(value);
        }
        private static readonly SettingProperty<int> AudioCrossfadingLengthProperty = new(nameof(AudioCrossfadingLength), 6);
        #endregion

        private sealed class SettingProperty<T>
        {
            private readonly string propertyName;
            private readonly T defaultValue;

            public SettingProperty(string PropertyName, T DefaultValue)
            {
                propertyName = PropertyName;
                defaultValue = DefaultValue;
            }

            public T GetValue()
            {
                if (!RootContainer.Values.ContainsKey(propertyName)) return defaultValue;
                return (T)RootContainer.Values[propertyName];
            }

            public void SetValue(T value) => RootContainer.Values[propertyName] = value;

            public void RemoveValue() => RootContainer.Values.Remove(propertyName);

            public T DefaultValue => defaultValue;
        }
    }
}
