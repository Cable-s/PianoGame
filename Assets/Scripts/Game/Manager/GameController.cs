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

  [Header("Editables")]
  [Tooltip("Enable/disable evaluating & rendering the left hand (bass staff / staff 2).")]
  [SerializeField] private bool leftHandEnabled = true;

  [Tooltip("Enable/disable evaluating & rendering the right hand (treble staff / staff 1).")]
  [SerializeField] private bool rightHandEnabled = true;

  [Tooltip("Practice mode: waits for correct input to advance. If OFF, plays at tempo and advances automatically.")]
  [SerializeField] private bool practiceMode = true;

  [Tooltip("Tempo in BPM (quarter note = 1 beat). Used when Practice mode is OFF.")]
  [SerializeField] private int tempoBpm = 120;

  [Tooltip("Seconds to count down before starting.")]
  [SerializeField] private float startCountdownSeconds = 3f;

  [Header("Chord / Simultaneous Input")]
  [Tooltip("Time window (seconds) to complete a chord/two-hand group after the first correct key is pressed.")]
  [SerializeField] private float simultaneousWindowSeconds = 0.25f;

  [Header("Performance (Logging)")]
  [Tooltip("Notes with duration >= this many beats are treated as hold notes (Guitar Hero style).")]
  [SerializeField] private float holdRequiredMinBeats = 1f;

  [Tooltip("Release is considered a break if it occurs more than this many beats before the expected end.")]
  [SerializeField] private float holdReleaseToleranceBeats = 0.1f;

  private MusicXmlParser _parser;
  private MusicXmlScore _currentScore;
  private List<Note> _renderNotes = new List<Note>();
  private Dictionary<Note, int> _renderIndexByNote = new Dictionary<Note, int>();
  private Dictionary<double, int> _renderStartIndexByBeat = new Dictionary<double, int>();

  private class ExpectedNoteEvent
  {
    public Note Note;
    public byte Midi;
    public double StartBeat;
    public double EndBeat;
    public bool Hit;
    public bool Broken;
  }

  private class ExpectedNoteGroup
  {
    public double StartBeatGlobal;
    public List<Note> Notes = new List<Note>();
    public HashSet<byte> RequiredMidiNotes = new HashSet<byte>();
    public List<ExpectedNoteEvent> Events = new List<ExpectedNoteEvent>();
  }

  private List<ExpectedNoteGroup> _expectedGroups = new List<ExpectedNoteGroup>();
  private int _currentGroupIndex = 0;
  private HashSet<byte> _currentGroupHits = new HashSet<byte>();
  private bool _groupWindowActive = false;
  private float _groupWindowStartTime = 0f;

  private List<Note> _expectedNotes = new List<Note>();
  private int _currentNoteIndex = 0; // legacy single-note index (no longer used for gameplay)
  private int _score = 0;
  private int _mistakes = 0;
  private int _totalNotes = 0;
  private NoteMatcher _noteMatcher;
  private bool _isPlaying = false;

  private bool _countdownActive = false;
  private float _countdownEndTime = 0f;
  private bool _songStarted = false;
  private float _songStartTime = 0f;

  private string _lastLoadedMusicPath = null;

  private double _practiceBeat = 0.0;
  private float _practiceLastUpdateTime = 0f;

  private readonly Dictionary<byte, ExpectedNoteEvent> _activeHoldsByMidi = new Dictionary<byte, ExpectedNoteEvent>();
  private readonly List<double> _noteOnErrorsSeconds = new List<double>();
  private int _holdNotesExpected = 0;
  private int _holdBreaks = 0;
  private int _missedNotes = 0;

  private void Update()
  {
    if (!_isPlaying)
      return;

    if (_countdownActive)
    {
      UpdateCountdownUI();
      return;
    }

    if (!_songStarted)
      return;

    if (practiceMode)
      UpdatePracticeBeat();
    else
      AdvanceGroupsByTempo();

    if (staffRenderer != null)
      staffRenderer.SetScrollBeat(GetCurrentBeatClock());

    if (_groupWindowActive && _currentGroupIndex < _expectedGroups.Count)
    {
      if (Time.time - _groupWindowStartTime > simultaneousWindowSeconds)
      {
        // Missing note(s) within the window.
        RegisterMistakeAndResetGroup("Timed out (missing chord note)");
      }
    }
  }

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
      staffRenderer = FindFirstObjectByType<GrandStaffNoteRenderer>();
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

      _currentScore = score;

      var allNotesForRender = score.GetAllNotes(includeRests: true);
      var allNotesForGameplay = score.GetAllNotes(includeRests: false);

      if (allNotesForGameplay.Count == 0)
      {
        Debug.LogError("[GAME] Music file has no notes");
        return;
      }

      // Only mark as "last loaded" once we know it parsed and is playable.
      _lastLoadedMusicPath = filePath;

      // If this file has explicit <staff> values (piano), prefer only staff 1/2 notes.
      bool hasExplicitPianoStaffs = allNotesForRender.Any(n => n != null && (n.StaffNumber == 1 || n.StaffNumber == 2));

      if (hasExplicitPianoStaffs)
      {
        // Keep rests even if they don't have a staff number, so the runup can show leading rests.
        allNotesForRender = allNotesForRender.Where(n => n != null && (n.IsRest || n.StaffNumber == 1 || n.StaffNumber == 2)).ToList();
        allNotesForGameplay = allNotesForGameplay.Where(n => n != null && (n.StaffNumber == 1 || n.StaffNumber == 2)).ToList();
      }

      // Apply left/right hand toggles.
      allNotesForRender = allNotesForRender.Where(IsEnabledByHand).ToList();
      allNotesForGameplay = allNotesForGameplay.Where(IsEnabledByHand).ToList();

      _renderNotes = new List<Note>(allNotesForRender);
      _renderIndexByNote.Clear();
      _renderStartIndexByBeat.Clear();
      for (int i = 0; i < _renderNotes.Count; i++)
      {
        var n = _renderNotes[i];
        if (n != null && !_renderIndexByNote.ContainsKey(n))
          _renderIndexByNote.Add(n, i);

        if (n != null && !_renderStartIndexByBeat.ContainsKey(n.StartBeatGlobal))
          _renderStartIndexByBeat.Add(n.StartBeatGlobal, i);
      }

      _expectedNotes = new List<Note>(allNotesForGameplay);
      _expectedGroups = BuildExpectedGroups(_expectedNotes);
      _totalNotes = _expectedGroups.Sum(g => g.RequiredMidiNotes.Count);
      _currentGroupIndex = 0;
      _currentGroupHits.Clear();
      _groupWindowActive = false;
      _groupWindowStartTime = 0f;
      _currentNoteIndex = 0;
      _score = 0;
      _mistakes = 0;

      _activeHoldsByMidi.Clear();
      _noteOnErrorsSeconds.Clear();
      _holdNotesExpected = 0;
      _holdBreaks = 0;
      _missedNotes = 0;

      _isPlaying = true;
      _songStarted = false;
      _countdownActive = true;
      _countdownEndTime = Time.time + Mathf.Max(0f, startCountdownSeconds);

      Debug.Log($"[GAME] Loaded {_totalNotes} notes from {filePath}");
      Debug.Log($"[GAME] Note groups: {_expectedGroups.Count}");

      // Build the full score once; scrolling will be driven by a beat clock.
      if (staffRenderer != null && _renderNotes.Count > 0)
      {
        staffRenderer.BuildFullScore(_renderNotes);
        staffRenderer.SetScrollBeat(0.0);
      }

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
    if (!_isPlaying || _countdownActive || !_songStarted || _currentGroupIndex >= _expectedGroups.Count)
      return;

    // In practice mode, if we're currently inside a rest span before the next playable group,
    // ignore input (you can't "play" a rest).
    if (practiceMode)
    {
      double elapsedBeats = GetCurrentBeatClock();
      double groupBeat = _expectedGroups[_currentGroupIndex].StartBeatGlobal;
      if (elapsedBeats + 0.0001 < groupBeat)
        return;
    }

    var group = _expectedGroups[_currentGroupIndex];

    Debug.Log($"[GAME] Played: {midiNoteNumber}, Expected group size: {group.RequiredMidiNotes.Count}");

    if (practiceMode)
    {
      // If the player hits anything outside the expected set, it's a mistake and the group resets.
      if (!group.RequiredMidiNotes.Contains(midiNoteNumber))
      {
        RegisterMistakeAndResetGroup("Wrong/extra note");
        return;
      }

      // Start the time window on the first correct key press.
      if (!_groupWindowActive)
      {
        _groupWindowActive = true;
        _groupWindowStartTime = Time.time;
      }

      // If the window has already expired, count as a miss.
      if (Time.time - _groupWindowStartTime > simultaneousWindowSeconds)
      {
        RegisterMistakeAndResetGroup("Too slow (missing chord note)");
        return;
      }

      // Count each required note once.
      if (_currentGroupHits.Add(midiNoteNumber))
      {
        _score++;
        Debug.Log($"[GAME] ✓ CORRECT! ({_currentGroupHits.Count}/{group.RequiredMidiNotes.Count})");

        TryLogAndStartHoldIfExpected(midiNoteNumber, group);
      }

      // If chord complete within window, advance.
      if (_currentGroupHits.Count >= group.RequiredMidiNotes.Count)
      {
        _currentGroupIndex++;
        _currentGroupHits.Clear();
        _groupWindowActive = false;
        _groupWindowStartTime = 0f;

        UpdateUI();

        if (_currentGroupIndex >= _expectedGroups.Count)
          EndGame();
      }
    }
    else
    {
      // Tempo mode: score hits against the CURRENT group, but progression is driven by time.
      if (!group.RequiredMidiNotes.Contains(midiNoteNumber))
      {
        _mistakes++;
        Debug.Log($"[GAME] ✗ MISTAKE ({_mistakes}): Wrong/extra note (tempo mode)");
        UpdateUI();
        return;
      }

      if (_currentGroupHits.Add(midiNoteNumber))
      {
        _score++;
        Debug.Log($"[GAME] ✓ HIT (tempo mode)! ({_currentGroupHits.Count}/{group.RequiredMidiNotes.Count})");
        UpdateUI();

        TryLogAndStartHoldIfExpected(midiNoteNumber, group);
      }
    }
  }

  /// <summary>
  /// Called when a MIDI note is released. Used for hold-note break detection.
  /// </summary>
  public void OnMidiNoteReleased(byte midiNoteNumber)
  {
    if (!_isPlaying || _countdownActive || !_songStarted)
      return;

    if (_activeHoldsByMidi.TryGetValue(midiNoteNumber, out var ev))
    {
      double nowBeat = GetCurrentBeatClock();
      if (nowBeat < ev.EndBeat - holdReleaseToleranceBeats)
      {
        if (!ev.Broken)
        {
          ev.Broken = true;
          _holdBreaks++;
          Debug.Log($"[PERF] HOLD BREAK: MIDI {midiNoteNumber} released at beat {nowBeat:F2}, expected end {ev.EndBeat:F2}");
        }
      }

      _activeHoldsByMidi.Remove(midiNoteNumber);
    }
  }

  private void EndGame()
  {
    _isPlaying = false;

    string perfSummary = BuildPerformanceSummary();
    Debug.Log($"[GAME] SONG COMPLETE! Final Score: {_score}/{_totalNotes} | Mistakes: {_mistakes} | {perfSummary}");
    UpdateUI();
  }

  private void UpdateUI()
  {
    if (_countdownActive)
    {
      // Countdown UI is driven by UpdateCountdownUI(); keep score visible.
      string scoreTexts = $"Score: {_score}/{_totalNotes}  Mistakes: {_mistakes}";
      if (scoreDisplay != null)
        scoreDisplay.text = scoreTexts;
      return;
    }

    string nextNote;
    if (_currentGroupIndex < _expectedGroups.Count)
    {
      var group = _expectedGroups[_currentGroupIndex];

      // In practice mode, the beat clock advances through rests but stops at playable groups.
      // If we're still before the next playable group, show rest.
      if (practiceMode && _songStarted)
      {
        double beat = GetCurrentBeatClock();
        if (beat + 0.0001 < group.StartBeatGlobal)
        {
          nextNote = "(rest)";
          goto NEXT_NOTE_DONE;
        }
      }

      var treble = group.Notes.Where(n => n != null && n.StaffNumber == 1 && !n.IsRest).Select(n => n.Pitch.ToString()).Distinct().ToList();
      var bass = group.Notes.Where(n => n != null && n.StaffNumber == 2 && !n.IsRest).Select(n => n.Pitch.ToString()).Distinct().ToList();

      if (treble.Count > 0 && bass.Count > 0)
        nextNote = $"Treble: {string.Join("+", treble)} | Bass: {string.Join("+", bass)}";
      else if (treble.Count > 0)
        nextNote = $"Treble: {string.Join("+", treble)}";
      else if (bass.Count > 0)
        nextNote = $"Bass: {string.Join("+", bass)}";
      else
        nextNote = "(rest)";
    }
    else
    {
      nextNote = "DONE";
    }

  NEXT_NOTE_DONE:

    string scoreText = $"Score: {_score}/{_totalNotes}  Mistakes: {_mistakes}";

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

    Debug.Log($"[GAME UI] {scoreText} | Next Note: {nextNote}");
  }

  public void StartGame()
  {
    LoadMusic(testMusicPath);
  }

  public void ResetGame()
  {
    Debug.Log("[GAME] Reset requested");

    // Preferred behavior: fully restart the song (including countdown) by re-loading.
    string pathToReload = !string.IsNullOrEmpty(_lastLoadedMusicPath) ? _lastLoadedMusicPath : testMusicPath;

    // Ensure parser exists even if Reset is clicked very early.
    if (_parser == null)
      _parser = new MusicXmlParser();
    if (_noteMatcher == null)
      _noteMatcher = new NoteMatcher();

    if (!string.IsNullOrEmpty(pathToReload) && File.Exists(pathToReload))
    {
      LoadMusic(pathToReload);
      return;
    }

    // Fallback: clear state even if no file is available.
    _currentGroupIndex = 0;
    _expectedGroups.Clear();
    _expectedNotes.Clear();
    _renderNotes.Clear();
    _renderIndexByNote.Clear();
    _renderStartIndexByBeat.Clear();

    _currentGroupHits.Clear();
    _groupWindowActive = false;
    _groupWindowStartTime = 0f;
    _currentNoteIndex = 0;
    _score = 0;
    _mistakes = 0;
    _totalNotes = 0;
    _missedNotes = 0;

    _activeHoldsByMidi.Clear();
    _noteOnErrorsSeconds.Clear();
    _holdNotesExpected = 0;
    _holdBreaks = 0;

    _countdownActive = false;
    _countdownEndTime = 0f;
    _songStarted = false;
    _songStartTime = 0f;
    _practiceBeat = 0.0;
    _practiceLastUpdateTime = 0f;
    _isPlaying = false;

    if (staffRenderer != null)
      staffRenderer.Clear();

    UpdateUI();
    Debug.LogWarning($"[GAME] Reset cleared state but could not reload file: '{pathToReload}'");
  }

  private void RegisterMistakeAndResetGroup(string reason)
  {
    _mistakes++;
    _currentGroupHits.Clear();
    _groupWindowActive = false;
    _groupWindowStartTime = 0f;
    Debug.Log($"[GAME] ✗ MISTAKE ({_mistakes}): {reason}");
    UpdateUI();
  }

  private void UpdateCountdownUI()
  {
    float remaining = _countdownEndTime - Time.time;
    int seconds = Mathf.CeilToInt(remaining);

    if (seconds > 0)
    {
      if (nextNoteDisplay != null)
        nextNoteDisplay.text = seconds.ToString();
      return;
    }

    // Start!
    _countdownActive = false;
    _songStarted = true;
    _songStartTime = Time.time;

    _practiceBeat = 0.0;
    _practiceLastUpdateTime = Time.time;

    _currentGroupHits.Clear();
    _groupWindowActive = false;
    _groupWindowStartTime = 0f;

    UpdateUI();
  }

  private int GetPlaybackBpm()
  {
    if (tempoBpm > 0)
      return tempoBpm;
    if (_currentScore != null && _currentScore.Bpm > 0)
      return _currentScore.Bpm;
    return 120;
  }

  private double GetElapsedBeats()
  {
    int bpm = GetPlaybackBpm();
    double elapsedSeconds = Time.time - _songStartTime;
    return elapsedSeconds * (bpm / 60.0);
  }

  private double GetCurrentBeatClock()
  {
    return practiceMode ? _practiceBeat : GetElapsedBeats();
  }

  private void UpdatePracticeBeat()
  {
    if (!practiceMode || !_songStarted || _countdownActive)
      return;

    if (_currentGroupIndex >= _expectedGroups.Count)
      return;

    double targetBeat = _expectedGroups[_currentGroupIndex].StartBeatGlobal;
    if (_practiceBeat + 0.0001 >= targetBeat)
    {
      _practiceLastUpdateTime = Time.time;
      _practiceBeat = targetBeat;
      return;
    }

    int bpm = GetPlaybackBpm();
    float now = Time.time;
    float dt = Mathf.Max(0f, now - _practiceLastUpdateTime);
    _practiceLastUpdateTime = now;

    double advance = dt * (bpm / 60.0);
    _practiceBeat = Math.Min(targetBeat, _practiceBeat + advance);
  }

  private void AdvanceGroupsByTempo()
  {
    if (_currentGroupIndex >= _expectedGroups.Count)
      return;

    int bpm = GetPlaybackBpm();
    float elapsed = Time.time - _songStartTime;

    // Advance as long as we've passed the NEXT group's scheduled time.
    bool advanced = false;
    while (_currentGroupIndex + 1 < _expectedGroups.Count)
    {
      double nextBeat = _expectedGroups[_currentGroupIndex + 1].StartBeatGlobal;
      float nextTime = (float)(nextBeat * (60.0 / bpm));

      if (elapsed < nextTime)
        break;

      // If the current group wasn't completed in time, count a mistake.
      var current = _expectedGroups[_currentGroupIndex];
      if (_currentGroupHits.Count < current.RequiredMidiNotes.Count)
      {
        _mistakes++;
        _missedNotes += Mathf.Max(0, current.RequiredMidiNotes.Count - _currentGroupHits.Count);
        Debug.Log($"[GAME] ✗ MISTAKE ({_mistakes}): Missed group at beat {current.StartBeatGlobal:F2} (tempo mode)");
      }

      _currentGroupIndex++;
      _currentGroupHits.Clear();
      advanced = true;
    }

    if (advanced)
    {
      UpdateUI();
      if (_currentGroupIndex >= _expectedGroups.Count)
        EndGame();
    }
  }

  private bool IsEnabledByHand(Note n)
  {
    if (n == null)
      return false;

    // If neither is enabled, render/evaluate nothing.
    if (!leftHandEnabled && !rightHandEnabled)
      return false;

    if (n.StaffNumber == 1)
      return rightHandEnabled;
    if (n.StaffNumber == 2)
      return leftHandEnabled;

    // If staff is not specified (common for some rests), keep it if either hand is enabled.
    return leftHandEnabled || rightHandEnabled;
  }

  private static List<ExpectedNoteGroup> BuildExpectedGroups(List<Note> notes)
  {
    var groups = new List<ExpectedNoteGroup>();
    if (notes == null || notes.Count == 0)
      return groups;

    // Group by absolute beat position.
    var ordered = notes
      .Where(n => n != null && !n.IsRest)
      .OrderBy(n => n.StartBeatGlobal)
      .ToList();

    const double eps = 0.0001;
    ExpectedNoteGroup current = null;
    foreach (var n in ordered)
    {
      if (current == null || System.Math.Abs(n.StartBeatGlobal - current.StartBeatGlobal) > eps)
      {
        current = new ExpectedNoteGroup { StartBeatGlobal = n.StartBeatGlobal };
        groups.Add(current);
      }

      current.Notes.Add(n);
      byte midi = n.Pitch.ToMidiNote();
      current.RequiredMidiNotes.Add(midi);

      double startBeat = n.StartBeatGlobal;
      double endBeat = startBeat + n.Duration.GetBeats();
      current.Events.Add(new ExpectedNoteEvent
      {
        Note = n,
        Midi = midi,
        StartBeat = startBeat,
        EndBeat = endBeat,
        Hit = false,
        Broken = false,
      });
    }

    return groups;
  }

  private void TryLogAndStartHoldIfExpected(byte midiNoteNumber, ExpectedNoteGroup group)
  {
    if (group == null)
      return;

    var ev = group.Events.FirstOrDefault(e => e != null && e.Midi == midiNoteNumber);
    if (ev == null)
      return;

    if (!ev.Hit)
      ev.Hit = true;

    int bpm = GetPlaybackBpm();
    double nowBeat = GetCurrentBeatClock();
    double deltaBeats = nowBeat - ev.StartBeat;
    double deltaSeconds = deltaBeats * (60.0 / bpm);
    _noteOnErrorsSeconds.Add(deltaSeconds);

    double durBeats = ev.EndBeat - ev.StartBeat;
    if (durBeats + 0.0001 >= holdRequiredMinBeats)
    {
      if (!_activeHoldsByMidi.ContainsKey(midiNoteNumber))
      {
        _activeHoldsByMidi.Add(midiNoteNumber, ev);
        _holdNotesExpected++;
      }
    }
  }

  private string BuildPerformanceSummary()
  {
    if (_noteOnErrorsSeconds.Count == 0)
      return $"Perf: no hits";

    double avgAbs = _noteOnErrorsSeconds.Select(System.Math.Abs).Average();
    int early = _noteOnErrorsSeconds.Count(d => d < 0);
    int late = _noteOnErrorsSeconds.Count - early;

    return $"Perf: avg|Δt|={avgAbs:F3}s (early={early}, late={late}), holds={_holdNotesExpected}, breaks={_holdBreaks}, missedNotes={_missedNotes}";
  }
}
