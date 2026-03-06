using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PianoGame.Game;
using PianoGame.MusicXML;

namespace PianoGame.UI
{
  /// <summary>
  /// Controller for the Level Selection scene.
  /// Allows cycling through songs, configuring playback options, and starting the game.
  /// 
  /// Setup in Unity:
  /// 1. Create a Canvas with:
  ///    - Song name display (TMP_Text)
  ///    - Previous/Next buttons. to cycle songs
  ///    - BPM input field (TMP_InputField)
  ///    - Left hand toggle (Toggle)
  ///    - Practice mode toggle (Toggle)
  ///    - Back button (to Main Menu)
  ///    - Start button (to begin playing)
  /// 2. Attach this script to an empty GameObject
  /// 3. Assign the UI elements in the inspector
  /// </summary>
  public class LevelSelectionController : MonoBehaviour
  {
    [Header("Song Display")]
    [SerializeField] private TMP_Text songNameText;
    [SerializeField] private TMP_Text songInfoText;
    [SerializeField] private TMP_Text personalBestText;
    [SerializeField] private Button previousSongButton;
    [SerializeField] private Button nextSongButton;

    [Header("Song Configuration")]
    [SerializeField] private TMP_InputField bpmInputField;
    [SerializeField] private TMP_Text defaultBpmText;
    [SerializeField] private Button resetBpmButton;
    [SerializeField] private Toggle leftHandToggle;
    [SerializeField] private Toggle practiceModeToggle;

    [Header("Navigation")]
    [SerializeField] private Button backButton;
    [SerializeField] private Button startButton;

    [Header("Song Library Settings")]
    [Tooltip("Folder path relative to Assets where music files are stored")]
    [SerializeField] private string musicFolderPath = "Assets/Music";
    [Tooltip("File extensions to look for")]
    [SerializeField] private string[] fileExtensions = { ".musicxml", ".xml" };

    // Song library
    private List<SongInfo> _songs = new List<SongInfo>();
    private int _currentSongIndex = 0;
    private MusicXmlParser _parser;

    /// <summary>
    /// Holds information about a detected song file.
    /// </summary>
    private class SongInfo
    {
      public string FilePath;
      public string DisplayName;
      public int DefaultBpm;
      public int NoteCount;
      public double DurationSeconds;
      public bool ParsedSuccessfully;
    }

    private void Start()
    {
      _parser = new MusicXmlParser();

      SetupUI();
      ScanForSongs();
      DisplayCurrentSong();
    }

    private void SetupUI()
    {
      // Navigation buttons
      if (previousSongButton != null)
        previousSongButton.onClick.AddListener(OnPreviousSong);

      if (nextSongButton != null)
        nextSongButton.onClick.AddListener(OnNextSong);

      if (backButton != null)
        backButton.onClick.AddListener(OnBackClicked);

      if (startButton != null)
        startButton.onClick.AddListener(OnStartClicked);

      if (resetBpmButton != null)
        resetBpmButton.onClick.AddListener(OnResetBpm);

      // BPM input field
      if (bpmInputField != null)
      {
        bpmInputField.contentType = TMP_InputField.ContentType.IntegerNumber;
        bpmInputField.onEndEdit.AddListener(OnBpmChanged);
      }

      // Toggles
      if (leftHandToggle != null)
      {
        leftHandToggle.isOn = GameData.LeftHandEnabled;
        leftHandToggle.onValueChanged.AddListener(OnLeftHandToggled);
      }

      if (practiceModeToggle != null)
      {
        practiceModeToggle.isOn = GameData.PracticeModeEnabled;
        practiceModeToggle.onValueChanged.AddListener(OnPracticeModeToggled);
      }
    }

    /// <summary>
    /// Scans the music folder for compatible song files.
    /// </summary>
    private void ScanForSongs()
    {
      _songs.Clear();

      string fullPath = Path.GetFullPath(musicFolderPath);

      if (!Directory.Exists(fullPath))
      {
        Debug.LogWarning($"[LevelSelection] Music folder not found: {fullPath}");

        // Try alternative paths
        fullPath = Path.Combine(Application.dataPath, "Music");
        if (!Directory.Exists(fullPath))
        {
          Debug.LogError($"[LevelSelection] Could not find music folder at: {fullPath}");
          return;
        }
      }

      Debug.Log($"[LevelSelection] Scanning for songs in: {fullPath}");

      foreach (var extension in fileExtensions)
      {
        var files = Directory.GetFiles(fullPath, $"*{extension}", SearchOption.AllDirectories);
        foreach (var file in files)
        {
          var songInfo = ParseSongFile(file);
          if (songInfo != null)
          {
            _songs.Add(songInfo);
          }
        }
      }

      // Sort alphabetically
      _songs = _songs.OrderBy(s => s.DisplayName).ToList();

      Debug.Log($"[LevelSelection] Found {_songs.Count} song(s)");

      // Update navigation button states
      UpdateNavigationButtons();
    }

    /// <summary>
    /// Parses a MusicXML file to extract song information.
    /// </summary>
    private SongInfo ParseSongFile(string filePath)
    {
      try
      {
        var score = _parser.ParseFile(filePath);
        if (score == null)
        {
          Debug.LogWarning($"[LevelSelection] Failed to parse: {filePath}");
          return null;
        }

        // Extract display name from file or title
        string displayName = !string.IsNullOrEmpty(score.Title)
            ? score.Title
            : Path.GetFileNameWithoutExtension(filePath);

        var notes = score.GetAllNotes(includeRests: false);

        return new SongInfo
        {
          FilePath = filePath,
          DisplayName = displayName,
          DefaultBpm = score.Bpm > 0 ? score.Bpm : 120,
          NoteCount = notes.Count,
          DurationSeconds = score.GetTotalDuration(),
          ParsedSuccessfully = true
        };
      }
      catch (System.Exception ex)
      {
        Debug.LogError($"[LevelSelection] Error parsing {filePath}: {ex.Message}");
        return null;
      }
    }

    /// <summary>
    /// Displays the currently selected song's information.
    /// </summary>
    private void DisplayCurrentSong()
    {
      if (_songs.Count == 0)
      {
        if (songNameText != null)
          songNameText.text = "No Songs Found";

        if (songInfoText != null)
          songInfoText.text = "Add MusicXML files to Assets/Music folder";

        if (personalBestText != null)
          personalBestText.text = "";

        if (startButton != null)
          startButton.interactable = false;

        return;
      }

      var song = _songs[_currentSongIndex];

      // Update song name
      if (songNameText != null)
      {
        songNameText.text = song.DisplayName;
      }

      // Update song info
      if (songInfoText != null)
      {
        int minutes = Mathf.FloorToInt((float)song.DurationSeconds / 60f);
        int seconds = Mathf.FloorToInt((float)song.DurationSeconds % 60f);
        songInfoText.text = $"Notes: {song.NoteCount} | Duration: {minutes}:{seconds:D2}";
      }

      // Update personal best display
      if (personalBestText != null)
      {
        var best = GameData.GetPersonalBest(song.FilePath);
        if (best != null)
        {
          string fcIndicator = best.IsFullCombo ? " FC" : "";
          personalBestText.text = $"Best: {best.Grade} {best.AccuracyPercent:F1}%{fcIndicator}";
        }
        else
        {
          personalBestText.text = "Best: --";
        }
      }

      // Update BPM
      if (bpmInputField != null)
      {
        // If switching songs, reset to default BPM
        bpmInputField.text = song.DefaultBpm.ToString();
        GameData.SelectedBpm = song.DefaultBpm;
        GameData.DefaultBpm = song.DefaultBpm;
      }

      if (defaultBpmText != null)
      {
        defaultBpmText.text = $"BPM";
      }

      // Update GameData
      GameData.SelectedSongPath = song.FilePath;
      GameData.SelectedSongName = song.DisplayName;

      // Enable start button
      if (startButton != null)
        startButton.interactable = true;

      UpdateNavigationButtons();
    }

    private void UpdateNavigationButtons()
    {
      // Always allow cycling (wrap around)
      if (previousSongButton != null)
        previousSongButton.interactable = _songs.Count > 1;

      if (nextSongButton != null)
        nextSongButton.interactable = _songs.Count > 1;
    }

    private void OnPreviousSong()
    {
      if (_songs.Count == 0) return;

      _currentSongIndex--;
      if (_currentSongIndex < 0)
        _currentSongIndex = _songs.Count - 1; // Wrap to end

      DisplayCurrentSong();
      Debug.Log($"[LevelSelection] Selected song: {_songs[_currentSongIndex].DisplayName}");
    }

    private void OnNextSong()
    {
      if (_songs.Count == 0) return;

      _currentSongIndex++;
      if (_currentSongIndex >= _songs.Count)
        _currentSongIndex = 0; // Wrap to beginning

      DisplayCurrentSong();
      Debug.Log($"[LevelSelection] Selected song: {_songs[_currentSongIndex].DisplayName}");
    }

    private void OnBpmChanged(string value)
    {
      if (int.TryParse(value, out int bpm))
      {
        // Clamp to reasonable range
        bpm = Mathf.Clamp(bpm, 20, 400);
        GameData.SelectedBpm = bpm;

        // Update field in case it was clamped
        if (bpmInputField != null && bpmInputField.text != bpm.ToString())
        {
          bpmInputField.text = bpm.ToString();
        }

        Debug.Log($"[LevelSelection] BPM set to: {bpm}");
      }
    }

    private void OnResetBpm()
    {
      if (_songs.Count == 0) return;

      var song = _songs[_currentSongIndex];
      GameData.SelectedBpm = song.DefaultBpm;

      if (bpmInputField != null)
      {
        bpmInputField.text = song.DefaultBpm.ToString();
      }

      Debug.Log($"[LevelSelection] BPM reset to default: {song.DefaultBpm}");
    }

    private void OnLeftHandToggled(bool isOn)
    {
      GameData.LeftHandEnabled = isOn;
      Debug.Log($"[LevelSelection] Left hand enabled: {isOn}");
    }

    private void OnPracticeModeToggled(bool isOn)
    {
      GameData.PracticeModeEnabled = isOn;
      Debug.Log($"[LevelSelection] Practice mode enabled: {isOn}");
    }

    private void OnBackClicked()
    {
      Debug.Log("[LevelSelection] Back button clicked - returning to Main Menu");
      SceneNavigator.GoToMainMenu();
    }

    private void OnStartClicked()
    {
      if (_songs.Count == 0 || string.IsNullOrEmpty(GameData.SelectedSongPath))
      {
        Debug.LogError("[LevelSelection] Cannot start - no song selected!");
        return;
      }

      Debug.Log($"[LevelSelection] Starting game with: {GameData.SelectedSongName} at {GameData.SelectedBpm} BPM");
      Debug.Log($"[LevelSelection] Left Hand: {GameData.LeftHandEnabled}, Practice Mode: {GameData.PracticeModeEnabled}");

      SceneNavigator.StartGame();
    }

    private void OnDestroy()
    {
      // Cleanup listeners
      if (previousSongButton != null) previousSongButton.onClick.RemoveListener(OnPreviousSong);
      if (nextSongButton != null) nextSongButton.onClick.RemoveListener(OnNextSong);
      if (backButton != null) backButton.onClick.RemoveListener(OnBackClicked);
      if (startButton != null) startButton.onClick.RemoveListener(OnStartClicked);
      if (resetBpmButton != null) resetBpmButton.onClick.RemoveListener(OnResetBpm);
      if (bpmInputField != null) bpmInputField.onEndEdit.RemoveListener(OnBpmChanged);
      if (leftHandToggle != null) leftHandToggle.onValueChanged.RemoveListener(OnLeftHandToggled);
      if (practiceModeToggle != null) practiceModeToggle.onValueChanged.RemoveListener(OnPracticeModeToggled);
    }
  }
}
