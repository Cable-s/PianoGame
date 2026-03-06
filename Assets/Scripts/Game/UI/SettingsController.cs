using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PianoGame.Game;
using PianoGame.MIDI;

namespace PianoGame.UI
{
  /// <summary>
  /// Controller for the Settings scene.
  /// Handles MIDI device selection and volume adjustment.
  /// 
  /// Setup in Unity:
  /// 1. Create a Canvas with:
  ///    - A TMP_Dropdown for MIDI device selection
  ///    - A Slider for master volume
  ///    - A Back button to return to main menu
  ///    - Optional: Refresh button to re-scan MIDI devices
  /// 2. Attach this script to an empty GameObject
  /// 3. Assign the UI elements in the inspector
  /// </summary>
  public class SettingsController : MonoBehaviour
  {
    [Header("MIDI Device Selection")]
    [SerializeField] private TMP_Dropdown midiDeviceDropdown;
    [SerializeField] private Button refreshDevicesButton;
    [SerializeField] private TMP_Text midiStatusText;

    [Header("Volume Settings")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private TMP_Text masterVolumeLabel;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private TMP_Text sfxVolumeLabel;

    [Header("Navigation")]
    [SerializeField] private Button backButton;
    [SerializeField] private Button resetDefaultsButton;

    // Cached list of available MIDI devices
    private IReadOnlyList<string> _availableDevices;
    private RtMidiDeviceFactory _midiFactory;

    private void Start()
    {
      _midiFactory = new RtMidiDeviceFactory();

      SetupMidiDeviceSelector();
      SetupVolumeSliders();
      SetupNavigationButtons();

      RefreshMidiDevices();
    }

    private void SetupMidiDeviceSelector()
    {
      if (midiDeviceDropdown != null)
      {
        midiDeviceDropdown.onValueChanged.AddListener(OnMidiDeviceSelected);
      }

      if (refreshDevicesButton != null)
      {
        refreshDevicesButton.onClick.AddListener(RefreshMidiDevices);
      }
    }

    private void SetupVolumeSliders()
    {
      // Master Volume
      if (masterVolumeSlider != null)
      {
        masterVolumeSlider.minValue = 0f;
        masterVolumeSlider.maxValue = 1f;
        masterVolumeSlider.value = GameSettings.MasterVolume;
        masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        UpdateVolumeLabel(masterVolumeLabel, GameSettings.MasterVolume);
      }

      // SFX Volume
      if (sfxVolumeSlider != null)
      {
        sfxVolumeSlider.minValue = 0f;
        sfxVolumeSlider.maxValue = 1f;
        sfxVolumeSlider.value = GameSettings.SfxVolume;
        sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
        UpdateVolumeLabel(sfxVolumeLabel, GameSettings.SfxVolume);
      }
    }

    private void SetupNavigationButtons()
    {
      if (backButton != null)
      {
        backButton.onClick.AddListener(OnBackClicked);
      }

      if (resetDefaultsButton != null)
      {
        resetDefaultsButton.onClick.AddListener(OnResetDefaultsClicked);
      }
    }

    /// <summary>
    /// Scans for available MIDI devices and populates the dropdown.
    /// </summary>
    public void RefreshMidiDevices()
    {
      if (midiDeviceDropdown == null) return;

      _availableDevices = _midiFactory.GetAvailableDevices();

      midiDeviceDropdown.ClearOptions();

      var options = new List<string>();

      // Add "Auto (first available)" option
      options.Add("Auto (first available)");

      // Add each detected device
      for (int i = 0; i < _availableDevices.Count; i++)
      {
        options.Add($"{i}: {_availableDevices[i]}");
      }

      midiDeviceDropdown.AddOptions(options);

      // Set current selection
      int savedIndex = GameSettings.MidiDeviceIndex;
      if (savedIndex < 0 || savedIndex >= _availableDevices.Count)
      {
        midiDeviceDropdown.value = 0; // Auto
      }
      else
      {
        midiDeviceDropdown.value = savedIndex + 1; // +1 because of "Auto" option
      }

      UpdateMidiStatusText();

      Debug.Log($"[Settings] Found {_availableDevices.Count} MIDI device(s)");
    }

    private void OnMidiDeviceSelected(int dropdownIndex)
    {
      // Index 0 = Auto (-1), Index 1+ = specific device (0+)
      int deviceIndex = dropdownIndex - 1;

      GameSettings.MidiDeviceIndex = deviceIndex;

      UpdateMidiStatusText();

      Debug.Log($"[Settings] Selected MIDI device index: {deviceIndex}" +
               (deviceIndex < 0 ? " (Auto)" : $" ({_availableDevices[deviceIndex]})"));
    }

    private void UpdateMidiStatusText()
    {
      if (midiStatusText == null) return;

      if (_availableDevices == null || _availableDevices.Count == 0)
      {
        midiStatusText.text = "<color=red>No MIDI devices detected</color>";
        midiStatusText.color = Color.red;
      }
      else
      {
        int selectedIndex = GameSettings.MidiDeviceIndex;
        if (selectedIndex < 0)
        {
          midiStatusText.text = $"<color=green>Auto mode: Will use first available device</color>";
        }
        else if (selectedIndex < _availableDevices.Count)
        {
          midiStatusText.text = $"<color=green>Selected: {_availableDevices[selectedIndex]}</color>";
        }
        else
        {
          midiStatusText.text = $"<color=yellow>Warning: Saved device not found</color>";
        }
      }
    }

    private void OnMasterVolumeChanged(float value)
    {
      GameSettings.MasterVolume = value;
      UpdateVolumeLabel(masterVolumeLabel, value);
    }

    private void OnSfxVolumeChanged(float value)
    {
      GameSettings.SfxVolume = value;
      UpdateVolumeLabel(sfxVolumeLabel, value);
    }

    private void UpdateVolumeLabel(TMP_Text label, float value)
    {
      if (label != null)
      {
        label.text = $"{Mathf.RoundToInt(value * 100)}%";
      }
    }

    private void OnBackClicked()
    {
      Debug.Log("[Settings] Back button clicked - returning to Main Menu");
      SceneNavigator.GoToMainMenu();
    }

    private void OnResetDefaultsClicked()
    {
      Debug.Log("[Settings] Resetting to defaults");
      GameSettings.ResetToDefaults();

      // Update UI to reflect defaults
      if (masterVolumeSlider != null)
      {
        masterVolumeSlider.value = GameSettings.MasterVolume;
      }

      if (sfxVolumeSlider != null)
      {
        sfxVolumeSlider.value = GameSettings.SfxVolume;
      }

      RefreshMidiDevices();
    }

    private void OnDestroy()
    {
      // Cleanup listeners
      if (midiDeviceDropdown != null) midiDeviceDropdown.onValueChanged.RemoveListener(OnMidiDeviceSelected);
      if (refreshDevicesButton != null) refreshDevicesButton.onClick.RemoveListener(RefreshMidiDevices);
      if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.RemoveListener(OnMasterVolumeChanged);
      if (sfxVolumeSlider != null) sfxVolumeSlider.onValueChanged.RemoveListener(OnSfxVolumeChanged);
      if (backButton != null) backButton.onClick.RemoveListener(OnBackClicked);
      if (resetDefaultsButton != null) resetDefaultsButton.onClick.RemoveListener(OnResetDefaultsClicked);
    }
  }
}
