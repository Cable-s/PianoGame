using System;
using System.Collections.Generic;
using PianoGame.MIDI;

namespace PianoGame.Scoring
{
  /// <summary>
  /// Result of comparing a MIDI event to score expectations.
  /// </summary>
  public enum MatchResult
  {
    Perfect,      // Event matched within expected time perfectly
    Good,         // Event matched but slightly early/late
    Early,        // Event too early
    Late,         // Event too late
    MissedNote,   // Expected note not played
    ExtraNote,    // Unexpected note played
    NoMatch       // Could not match to any expectation
  }

  /// <summary>
  /// Represents a comparison between a MIDI event and a score expectation.
  /// </summary>
  public class NoteMatch
  {
    public ScoreExpectation Expectation { get; set; }
    public NoteOnEvent MidiEvent { get; set; }
    public MatchResult Result { get; set; }
    public double TimingError { get; set; } // In seconds (negative = early, positive = late)
    public int VelocityDifference { get; set; } // Difference from expected (0-127)

    public NoteMatch()
    {
      TimingError = 0;
      VelocityDifference = 0;
    }
  }

  /// <summary>
  /// Matcher - Compares MIDI input events to score expectations.
  /// 
  /// Responsibility: Match MIDI notes to expected notes and report discrepancies.
  /// Does NOT: Calculate scores or modify game state. That's the Scorer's job.
  /// </summary>
  public interface INoteMatcher
  {
    NoteMatch Match(NoteOnEvent midiEvent, IScoreEngine scoreEngine);
    void AddNote(NoteOnEvent noteOn, NoteOffEvent noteOff);
    void Reset();
  }

  /// <summary>
  /// Standard implementation of Note Matcher.
  /// Tracks played notes and matches them to expectations.
  /// </summary>
  public class NoteMatcher : INoteMatcher
  {
    private List<NoteMatch> _playedNotes = new List<NoteMatch>();
    private Dictionary<byte, double> _activeNotes = new Dictionary<byte, double>(); // MIDI note -> start time

    public IReadOnlyList<NoteMatch> PlayedNotes => _playedNotes.AsReadOnly();

    /// <summary>
    /// Matches a MIDI NoteOn event against score expectations.
    /// </summary>
    public NoteMatch Match(NoteOnEvent midiEvent, IScoreEngine scoreEngine)
    {
      if (midiEvent == null)
        throw new ArgumentNullException(nameof(midiEvent));
      if (scoreEngine == null)
        throw new ArgumentNullException(nameof(scoreEngine));

      var match = new NoteMatch { MidiEvent = midiEvent };

      // Find expectations near this event's time
      var nearbyExpectations = scoreEngine.GetExpectationsInWindow(
          midiEvent.Timestamp - 0.5,
          midiEvent.Timestamp + 0.5
      );

      // Try to find a matching note in expectations
      ScoreExpectation matchedExpectation = null;
      double bestTimingDifference = double.MaxValue;

      foreach (var exp in nearbyExpectations)
      {
        if (exp.IsRest)
          continue;

        if (exp.MidiNote == midiEvent.Note)
        {
          double timingDiff = Math.Abs(midiEvent.Timestamp - exp.ExpectedTime);
          if (timingDiff < bestTimingDifference)
          {
            bestTimingDifference = timingDiff;
            matchedExpectation = exp;
          }
        }
      }

      if (matchedExpectation != null)
      {
        match.Expectation = matchedExpectation;
        match.TimingError = midiEvent.Timestamp - matchedExpectation.ExpectedTime;
        match.VelocityDifference = Math.Abs(midiEvent.Velocity - 64); // Assume 64 is "normal"
        match.Result = DetermineMatchQuality(matchedExpectation, midiEvent);
      }
      else
      {
        // Could be an extra note not in the score
        match.Result = MatchResult.ExtraNote;
      }

      _playedNotes.Add(match);
      _activeNotes[midiEvent.Note] = midiEvent.Timestamp;

      return match;
    }

    /// <summary>
    /// Records a note-off event for a played note.
    /// </summary>
    public void AddNote(NoteOnEvent noteOn, NoteOffEvent noteOff)
    {
      if (noteOn != null && _activeNotes.ContainsKey(noteOn.Note))
      {
        _activeNotes.Remove(noteOn.Note);
      }
    }

    /// <summary>
    /// Resets the matcher for a new performance.
    /// </summary>
    public void Reset()
    {
      _playedNotes.Clear();
      _activeNotes.Clear();
    }

    /// <summary>
    /// Determines the quality of a match based on timing and velocity.
    /// </summary>
    private MatchResult DetermineMatchQuality(ScoreExpectation expectation, NoteOnEvent midiEvent)
    {
      double timingError = midiEvent.Timestamp - expectation.ExpectedTime;

      // Check timing windows
      if (timingError < -expectation.AllowedEarlyTime)
        return MatchResult.Early;

      if (timingError > expectation.AllowedLateTime)
        return MatchResult.Late;

      // Within acceptable range
      double absoluteError = Math.Abs(timingError);
      double tolerance = Math.Min(expectation.AllowedEarlyTime, expectation.AllowedLateTime) * 0.5;

      if (absoluteError < tolerance)
        return MatchResult.Perfect;
      else
        return MatchResult.Good;
    }

    /// <summary>
    /// Detects missed notes - expectations that weren't matched.
    /// </summary>
    public List<ScoreExpectation> DetectMissedNotes(IScoreEngine scoreEngine, double currentTime)
    {
      var missed = new List<ScoreExpectation>();

      foreach (var exp in scoreEngine.Expectations)
      {
        if (exp.IsRest)
          continue;

        // Has time passed for this expectation?
        if (currentTime > exp.ExpectedTime + exp.AllowedLateTime)
        {
          // Check if it was played
          bool wasPlayed = _playedNotes.Exists(m => m.Expectation?.Index == exp.Index);
          if (!wasPlayed)
          {
            missed.Add(exp);
          }
        }
      }

      return missed;
    }
  }
}
