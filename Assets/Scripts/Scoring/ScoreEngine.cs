using System;
using System.Collections.Generic;
using PianoGame.MusicXML;

namespace PianoGame.Scoring
{
  /// <summary>
  /// Score Engine - Defines expectations based on a MusicXML score.
  /// 
  /// Responsibility: Convert score into expected note sequence with timing.
  /// Does NOT: Process MIDI input or validate events. That's the Matcher's job.
  /// </summary>
  public interface IScoreEngine
  {
    MusicXmlScore CurrentScore { get; }
    IReadOnlyList<ScoreExpectation> Expectations { get; }

    void LoadScore(MusicXmlScore score);

    public List<ScoreExpectation> GetExpectationsInWindow(double startTime, double endTime);
  }

  /// <summary>
  /// Represents what the player is expected to play at a specific moment.
  /// </summary>
  public class ScoreExpectation
  {
    public int Index { get; set; } // Position in the sequence
    public byte MidiNote { get; set; } // Expected MIDI note (0-127)
    public double ExpectedTime { get; set; } // Expected time in seconds
    public double AllowedEarlyTime { get; set; } // How early player can play
    public double AllowedLateTime { get; set; } // How late player can play
    public double Duration { get; set; } // Expected note duration in seconds
    public bool IsRest { get; set; } // Is this a rest period?

    public ScoreExpectation(int index, byte midiNote, double expectedTime, double duration,
        double allowedEarlyTime = 0.1, double allowedLateTime = 0.1)
    {
      Index = index;
      MidiNote = midiNote;
      ExpectedTime = expectedTime;
      Duration = duration;
      AllowedEarlyTime = allowedEarlyTime;
      AllowedLateTime = allowedLateTime;
      IsRest = false;
    }

    public ScoreExpectation(int index, double expectedTime, double duration)
    {
      Index = index;
      MidiNote = 0;
      ExpectedTime = expectedTime;
      Duration = duration;
      AllowedEarlyTime = 0;
      AllowedLateTime = 0;
      IsRest = true;
    }

    public bool IsExpectedTime(double actualTime)
    {
      return actualTime >= (ExpectedTime - AllowedEarlyTime) &&
             actualTime <= (ExpectedTime + AllowedLateTime);
    }
  }

  /// <summary>
  /// Standard implementation of Score Engine.
  /// Builds expectations from a loaded score.
  /// </summary>
  public class ScoreEngine : IScoreEngine
  {
    private MusicXmlScore _currentScore;
    private List<ScoreExpectation> _expectations = new List<ScoreExpectation>();

    public MusicXmlScore CurrentScore => _currentScore;
    public IReadOnlyList<ScoreExpectation> Expectations => _expectations.AsReadOnly();

    public void LoadScore(MusicXmlScore score)
    {
      if (score == null)
        throw new ArgumentNullException(nameof(score));

      _currentScore = score;
      BuildExpectations();
    }

    /// <summary>
    /// Builds the expected note sequence from the score.
    /// Converts music notation into expectations for the Matcher to use.
    /// </summary>
    private void BuildExpectations()
    {
      _expectations.Clear();

      var allNotes = _currentScore.GetAllNotes();
      int index = 0;

      foreach (var note in allNotes)
      {
        double duration = note.Duration.GetBeats(_currentScore.Divisions) * (60.0 / _currentScore.Bpm);

        if (note.IsRest)
        {
          var rest = new ScoreExpectation(index, note.StartTime, duration);
          _expectations.Add(rest);
        }
        else
        {
          var expectation = new ScoreExpectation(
              index,
              note.Pitch.ToMidiNote(),
              note.StartTime,
              duration,
              allowedEarlyTime: 0.1, // 100ms tolerance
              allowedLateTime: 0.1
          );
          _expectations.Add(expectation);
        }

        index++;
      }
    }

    /// <summary>
    /// Gets the expectation closest to the given time.
    /// Useful for the Matcher to find relevant expectations.
    /// </summary>
    public ScoreExpectation GetExpectationAtTime(double time)
    {
      ScoreExpectation closest = null;
      double minDistance = double.MaxValue;

      foreach (var exp in _expectations)
      {
        double distance = Math.Abs(exp.ExpectedTime - time);
        if (distance < minDistance)
        {
          minDistance = distance;
          closest = exp;
        }
      }

      return closest;
    }

    /// <summary>
    /// Gets expectations within a time window (useful for lookahead).
    /// </summary>
    public List<ScoreExpectation> GetExpectationsInWindow(double startTime, double endTime)
    {
      var result = new List<ScoreExpectation>();

      foreach (var exp in _expectations)
      {
        if (exp.ExpectedTime >= startTime && exp.ExpectedTime <= endTime)
        {
          result.Add(exp);
        }
      }

      return result;
    }
  }
}
