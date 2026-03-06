using UnityEngine;

namespace PianoGame.Game
{
  /// <summary>
  /// Persistent game settings stored in PlayerPrefs.
  /// Handles MIDI device selection, volume, and other user preferences.
  /// </summary>
  public static class GameSettings
  {
    // PlayerPrefs keys
    private const string KEY_MIDI_DEVICE_INDEX = "MidiDeviceIndex";
    private const string KEY_MASTER_VOLUME = "MasterVolume";
    private const string KEY_SFX_VOLUME = "SfxVolume";
    private const string KEY_MUSIC_VOLUME = "MusicVolume";

    /// <summary>
    /// Selected MIDI device index. -1 means auto-select first available.
    /// </summary>
    public static int MidiDeviceIndex
    {
      get => PlayerPrefs.GetInt(KEY_MIDI_DEVICE_INDEX, -1);
      set
      {
        PlayerPrefs.SetInt(KEY_MIDI_DEVICE_INDEX, value);
        PlayerPrefs.Save();
      }
    }

    /// <summary>
    /// Master volume (0.0 - 1.0).
    /// </summary>
    public static float MasterVolume
    {
      get => PlayerPrefs.GetFloat(KEY_MASTER_VOLUME, 1.0f);
      set
      {
        PlayerPrefs.SetFloat(KEY_MASTER_VOLUME, Mathf.Clamp01(value));
        PlayerPrefs.Save();
        ApplyVolume();
      }
    }

    /// <summary>
    /// Sound effects volume (0.0 - 1.0).
    /// </summary>
    public static float SfxVolume
    {
      get => PlayerPrefs.GetFloat(KEY_SFX_VOLUME, 1.0f);
      set
      {
        PlayerPrefs.SetFloat(KEY_SFX_VOLUME, Mathf.Clamp01(value));
        PlayerPrefs.Save();
      }
    }

    /// <summary>
    /// Music/background volume (0.0 - 1.0).
    /// </summary>
    public static float MusicVolume
    {
      get => PlayerPrefs.GetFloat(KEY_MUSIC_VOLUME, 1.0f);
      set
      {
        PlayerPrefs.SetFloat(KEY_MUSIC_VOLUME, Mathf.Clamp01(value));
        PlayerPrefs.Save();
      }
    }

    /// <summary>
    /// Applies the current volume settings to AudioListener.
    /// </summary>
    public static void ApplyVolume()
    {
      AudioListener.volume = MasterVolume;
    }

    /// <summary>
    /// Resets all settings to default values.
    /// </summary>
    public static void ResetToDefaults()
    {
      MidiDeviceIndex = -1;
      MasterVolume = 1.0f;
      SfxVolume = 1.0f;
      MusicVolume = 1.0f;
    }

    /// <summary>
    /// Initializes settings on game start.
    /// Call this from a startup script.
    /// </summary>
    public static void Initialize()
    {
      ApplyVolume();
    }
  }
}
