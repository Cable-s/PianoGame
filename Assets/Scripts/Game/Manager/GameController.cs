using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using TMPro;
using PianoGame.MIDI;
using PianoGame.MusicXML;
using PianoGame.Scoring;

/// <summary>
/// Main game controller - orchestrates MIDI input, music loading, and scoring.
/// </summary>
public class GameController : MonoBehaviour
{
  [SerializeField] private TMP_Text scoreDisplay; // Drag a Text UI element here
  [SerializeField] private TMP_Text nextNoteDisplay; // Drag another Text UI element here
  [SerializeField] private GrandStaffNoteRenderer staffRenderer;
  [SerializeField] private string testMusicPath = "Assets/Music/MozaChloSample.musicxml"; // Path to test file

  private MusicXmlParser _parser;
  private List<Note> _expectedNotes = new List<Note>();
  private int _currentNoteIndex = 0;
  private int _score = 0;
  private int _totalNotes = 0;
  private NoteMatcher _noteMatcher;
  private bool _isPlaying = false;

  private void Start()
  {
    Debug.Log("[GAME] === GameController START ===");

    _parser = new MusicXmlParser();
    _noteMatcher = new NoteMatcher();

    // Auto-find TMP_Text fields if not assigned
    var allTexts = FindObjectsByType<TMP_Text>(FindObjectsSortMode.None);

    if (scoreDisplay == null && allTexts.Length > 0)
    {
      scoreDisplay = allTexts[0];
      Debug.Log($"[GAME] Found scoreDisplay: {(scoreDisplay != null ? scoreDisplay.name : "FAILED")}");
    }

    if (nextNoteDisplay == null && allTexts.Length > 1)
    {
      nextNoteDisplay = allTexts[1];
      Debug.Log($"[GAME] Found nextNoteDisplay: {(nextNoteDisplay != null ? nextNoteDisplay.name : "FAILED")}");
    }

    if (staffRenderer == null)
    {
      staffRenderer = FindObjectOfType<GrandStaffNoteRenderer>();
    }
  }

  /// <summary>
  /// Load a MusicXML file and prepare for playing.
  /// </summary>
  public void LoadMusic(string filePath)
  {
    try
    {
      Debug.Log($"[GAME] LoadMusic called with filePath: '{filePath}'");

      if (!File.Exists(filePath))
      {
        Debug.LogError($"[GAME] File not found: {filePath}");
        Debug.LogError($"[GAME] Current directory: {System.IO.Directory.GetCurrentDirectory()}");
        Debug.LogError($"[GAME] Full path would be: {System.IO.Path.GetFullPath(filePath)}");
        return;
      }

      Debug.Log($"[GAME] File EXISTS: {filePath}");
      Debug.Log($"[GAME] Loading music from: {filePath}");

      var score = _parser.ParseFile(filePath);
      if (score == null)
      {
        Debug.LogError("[GAME] Failed to parse music file");
        return;
      }

      var allNotes = score.GetAllNotes();
      if (allNotes.Count == 0)
      {
        Debug.LogError("[GAME] Music file has no notes");
        return;
      }

      _expectedNotes = new List<Note>(allNotes);
      _totalNotes = _expectedNotes.Count;
      _currentNoteIndex = 0;
      _score = 0;
      _isPlaying = true;

      Debug.Log($"[GAME] Loaded {_totalNotes} notes from {filePath}");
      Debug.Log($"[GAME] Notes: {string.Join(", ", _expectedNotes.Select(n => n.Pitch.ToString()))}");

      UpdateUI();
    }
    catch (Exception ex)
    {
      Debug.LogError($"[GAME] Error loading music: {ex.Message}");
    }
  }

  /// <summary>
  /// Called when a MIDI note is played.
  /// </summary>
  public void OnMidiNoteReceived(byte midiNoteNumber)
  {
    if (!_isPlaying || _currentNoteIndex >= _expectedNotes.Count)
      return;

    var expectedNote = _expectedNotes[_currentNoteIndex];
    byte expectedMidiNote = expectedNote.Pitch.ToMidiNote();

    Debug.Log($"[GAME] Played: {midiNoteNumber}, Expected: {expectedMidiNote}");

    if (midiNoteNumber == expectedMidiNote)
    {
      _score++;
      _currentNoteIndex++;
      UpdateUI();
      Debug.Log($"[GAME] ✓ CORRECT! Note {expectedNote.Pitch}");

      if (_currentNoteIndex >= _expectedNotes.Count)
      {
        EndGame();
      }
    }
    else
    {
      Debug.Log($"[GAME] ✗ WRONG! Expected {expectedNote.Pitch}, got {Pitch.FromMidiNote(midiNoteNumber)}");
    }
  }

  private void EndGame()
  {
    _isPlaying = false;
    Debug.Log($"[GAME] SONG COMPLETE! Final Score: {_score}/{_totalNotes}");
    UpdateUI();
  }

  private void UpdateUI()
  {
    string nextNote = _currentNoteIndex < _expectedNotes.Count
      ? _expectedNotes[_currentNoteIndex].Pitch.ToString()
      : "DONE";

    string scoreText = $"Score: {_score}/{_totalNotes}";

    Debug.Log($"[GAME UI] UpdateUI called! scoreDisplay={scoreDisplay}, nextNoteDisplay={nextNoteDisplay}");
    Debug.Log($"[GAME UI] Setting scoreDisplay.text to: '{scoreText}'");
    Debug.Log($"[GAME UI] Setting nextNoteDisplay.text to: '{nextNote}'");

    if (scoreDisplay != null)
    {
      scoreDisplay.text = scoreText;
      Debug.Log($"[GAME UI] scoreDisplay.text is now: '{scoreDisplay.text}'");
    }
    else
      Debug.LogWarning("[GAME] scoreDisplay is null, cannot update!");

    if (nextNoteDisplay != null)
    {
      nextNoteDisplay.text = nextNote;
      Debug.Log($"[GAME UI] nextNoteDisplay.text is now: '{nextNoteDisplay.text}'");
    }
    else
      Debug.LogWarning("[GAME] nextNoteDisplay is null, cannot update!");

    if (staffRenderer != null)
    {
      if (_currentNoteIndex < _expectedNotes.Count)
        staffRenderer.ShowNextNote(_expectedNotes[_currentNoteIndex]);
      else
        staffRenderer.Clear();
    }

    Debug.Log($"[GAME UI] {scoreText} | Next Note: {nextNote}");
  }

  public void StartGame()
  {
    LoadMusic(testMusicPath);
  }

  public void ResetGame()
  {
    _currentNoteIndex = 0;
    _score = 0;
    _isPlaying = false;
    UpdateUI();
    Debug.Log("[GAME] Game reset");
  }
}
