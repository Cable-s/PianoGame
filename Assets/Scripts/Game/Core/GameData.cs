using UnityEngine;
using System.Collections.Generic;

namespace PianoGame.Game
{
  /// <summary>
  /// Stores a personal best record for a song.
  /// </summary>
  public class PersonalBestRecord
  {
    public string Grade { get; set; }
    public float AccuracyPercent { get; set; }
    public bool IsFullCombo { get; set; }
    public int MaxCombo { get; set; }
    public int Score { get; set; }
  }

  /// <summary>
  /// Static class to hold game state shared across scenes.
  /// Persists data like selected song, game settings, and results between scene transitions.
  /// </summary>
  public static class GameData
  {
    // ============================================
    // PERSONAL BEST RECORDS (In-memory for now)
    // ============================================

    /// <summary>
    /// Dictionary storing personal best records keyed by song file path.
    /// </summary>
    private static Dictionary<string, PersonalBestRecord> _personalBests = new Dictionary<string, PersonalBestRecord>();

    /// <summary>
    /// Gets the personal best record for a song, or null if none exists.
    /// </summary>
    public static PersonalBestRecord GetPersonalBest(string songPath)
    {
      if (string.IsNullOrEmpty(songPath))
        return null;

      _personalBests.TryGetValue(songPath, out var record);
      return record;
    }

    /// <summary>
    /// Saves the current game results as personal best if it's better than the existing record.
    /// Call this after a song completes.
    /// </summary>
    public static void SavePersonalBestIfBetter()
    {
      if (string.IsNullOrEmpty(SelectedSongPath))
        return;

      float currentAccuracy = GetAccuracyPercent();
      var existing = GetPersonalBest(SelectedSongPath);

      // Check if this is a new best (higher accuracy wins, full combo breaks ties)
      bool isNewBest = false;

      if (existing == null)
      {
        isNewBest = true;
      }
      else if (currentAccuracy > existing.AccuracyPercent)
      {
        isNewBest = true;
      }
      else if (currentAccuracy == existing.AccuracyPercent && IsFullCombo && !existing.IsFullCombo)
      {
        isNewBest = true;
      }

      if (isNewBest)
      {
        var newRecord = new PersonalBestRecord
        {
          Grade = GetLetterGrade(),
          AccuracyPercent = currentAccuracy,
          IsFullCombo = IsFullCombo,
          MaxCombo = MaxCombo,
          Score = FinalScore
        };

        _personalBests[SelectedSongPath] = newRecord;
        Debug.Log($"[GameData] New personal best for '{SelectedSongName}': {newRecord.Grade} {newRecord.AccuracyPercent:F1}%{(newRecord.IsFullCombo ? " FC!" : "")}");
      }
    }

    // ============================================
    // SELECTED SONG DATA (Set by Level Selection)
    // ============================================

    /// <summary>
    /// Full file path to the selected MusicXML file.
    /// </summary>
    public static string SelectedSongPath { get; set; }

    /// <summary>
    /// Display name of the selected song (extracted from filename or metadata).
    /// </summary>
    public static string SelectedSongName { get; set; }

    /// <summary>
    /// BPM to use for playback (can be edited by user).
    /// </summary>
    public static int SelectedBpm { get; set; } = 120;

    /// <summary>
    /// Default BPM from the song file.
    /// </summary>
    public static int DefaultBpm { get; set; } = 120;

    /// <summary>
    /// Whether left hand (bass staff) is enabled for this song session.
    /// </summary>
    public static bool LeftHandEnabled { get; set; } = true;

    /// <summary>
    /// Whether practice mode is enabled (waits for correct input vs tempo-based).
    /// </summary>
    public static bool PracticeModeEnabled { get; set; } = true;

    // ============================================
    // GAME RESULTS (Set by Game Scene on completion)
    // ============================================

    /// <summary>
    /// Player's maximum combo achieved during the song.
    /// </summary>
    public static int MaxCombo { get; set; }

    /// <summary>
    /// Total number of notes in the song (for calculating full combo).
    /// </summary>
    public static int TotalNotes { get; set; }

    /// <summary>
    /// Number of perfect hits.
    /// </summary>
    public static int PerfectHits { get; set; }

    /// <summary>
    /// Number of close/almost hits.
    /// </summary>
    public static int CloseHits { get; set; }

    /// <summary>
    /// Number of missed notes.
    /// </summary>
    public static int MissedNotes { get; set; }

    /// <summary>
    /// Final score achieved.
    /// </summary>
    public static int FinalScore { get; set; }

    /// <summary>
    /// Whether the player achieved a full combo.
    /// </summary>
    public static bool IsFullCombo => MaxCombo == TotalNotes && TotalNotes > 0;

    /// <summary>
    /// Gets the accuracy percentage (0-100).
    /// </summary>
    public static float GetAccuracyPercent()
    {
      int totalAttempts = PerfectHits + CloseHits + MissedNotes;
      if (totalAttempts == 0) return 0f;

      // Perfect = 100%, Close = 50%, Miss = 0%
      float weightedScore = (PerfectHits * 1.0f) + (CloseHits * 0.5f);
      return (weightedScore / totalAttempts) * 100f;
    }

    /// <summary>
    /// Gets a letter grade based on accuracy.
    /// </summary>
    public static string GetLetterGrade()
    {
      float accuracy = GetAccuracyPercent();

      if (IsFullCombo && accuracy >= 100f) return "S+";
      if (accuracy >= 95f) return "S";
      if (accuracy >= 90f) return "A";
      if (accuracy >= 80f) return "B";
      if (accuracy >= 70f) return "C";
      if (accuracy >= 60f) return "D";
      return "F";
    }

    /// <summary>
    /// Resets all game result data for a new game session.
    /// </summary>
    public static void ResetResults()
    {
      MaxCombo = 0;
      TotalNotes = 0;
      PerfectHits = 0;
      CloseHits = 0;
      MissedNotes = 0;
      FinalScore = 0;
    }

    /// <summary>
    /// Resets all data including song selection.
    /// </summary>
    public static void ResetAll()
    {
      SelectedSongPath = null;
      SelectedSongName = null;
      SelectedBpm = 120;
      DefaultBpm = 120;
      LeftHandEnabled = true;
      PracticeModeEnabled = true;
      ResetResults();
    }
  }
}
