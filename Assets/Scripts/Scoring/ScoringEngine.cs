using System;
using System.Collections.Generic;
using System.Linq;

namespace PianoGame.Scoring
{
  /// <summary>
  /// Performance metrics for a scored performance.
  /// </summary>
  public class PerformanceMetrics
  {
    public int TotalNotes { get; set; }
    public int PerfectNotes { get; set; }
    public int GoodNotes { get; set; }
    public int EarlyNotes { get; set; }
    public int LateNotes { get; set; }
    public int MissedNotes { get; set; }
    public int ExtraNotes { get; set; }

    public double Accuracy => TotalNotes > 0
        ? (double)(PerfectNotes + GoodNotes) / TotalNotes
        : 0;

    public double Precision => (PerfectNotes + GoodNotes + EarlyNotes + LateNotes) > 0
        ? (double)(PerfectNotes + GoodNotes) / (PerfectNotes + GoodNotes + EarlyNotes + LateNotes)
        : 0;

    public double AverageTimingError { get; set; } // In milliseconds
    public long Score { get; set; }
    public string Grade { get; set; } // A, B, C, D, F
  }

  /// <summary>
  /// Scorer - Aggregates match results into a performance score.
  /// 
  /// Responsibility: Calculate score, metrics, and grade based on matches.
  /// Does NOT: Match notes or modify game state. That's the Matcher's job.
  /// </summary>
  public interface IScorer
  {
    PerformanceMetrics CalculateScore(IReadOnlyList<NoteMatch> matches, int totalExpectations);
    long ScoreNote(MatchResult result, double timingError);
  }

  /// <summary>
  /// Standard implementation of Scorer.
  /// Uses a points-based system for evaluation.
  /// </summary>
  public class Scorer : IScorer
  {
    // Points for different match results
    private const long PerfectPoints = 100;
    private const long GoodPoints = 75;
    private const long EarlyPoints = 50;
    private const long LatePoints = 50;
    private const long MissedPenalty = -50;
    private const long ExtraPenalty = -25;

    /// <summary>
    /// Calculates comprehensive performance metrics from match results.
    /// </summary>
    public PerformanceMetrics CalculateScore(IReadOnlyList<NoteMatch> matches, int totalExpectations)
    {
      var metrics = new PerformanceMetrics { TotalNotes = totalExpectations };

      double totalTimingError = 0;
      int timingErrorCount = 0;

      foreach (var match in matches)
      {
        switch (match.Result)
        {
          case MatchResult.Perfect:
            metrics.PerfectNotes++;
            break;
          case MatchResult.Good:
            metrics.GoodNotes++;
            break;
          case MatchResult.Early:
            metrics.EarlyNotes++;
            break;
          case MatchResult.Late:
            metrics.LateNotes++;
            break;
          case MatchResult.ExtraNote:
            metrics.ExtraNotes++;
            break;
        }

        if (match.TimingError != 0)
        {
          totalTimingError += Math.Abs(match.TimingError);
          timingErrorCount++;
        }

        metrics.Score += ScoreNote(match.Result, match.TimingError);
      }

      // Add missed notes penalty
      metrics.MissedNotes = totalExpectations - (metrics.PerfectNotes + metrics.GoodNotes +
                                                 metrics.EarlyNotes + metrics.LateNotes);
      metrics.Score += metrics.MissedNotes * MissedPenalty;

      // Add extra notes penalty
      metrics.Score += metrics.ExtraNotes * ExtraPenalty;

      // Calculate average timing error
      metrics.AverageTimingError = timingErrorCount > 0
          ? (totalTimingError / timingErrorCount) * 1000
          : 0; // Convert to milliseconds

      // Ensure score doesn't go negative
      metrics.Score = Math.Max(0, metrics.Score);

      // Determine grade
      metrics.Grade = CalculateGrade(metrics);

      return metrics;
    }

    /// <summary>
    /// Scores a single note based on match result and timing.
    /// </summary>
    public long ScoreNote(MatchResult result, double timingError)
    {
      long basePoints = result switch
      {
        MatchResult.Perfect => PerfectPoints,
        MatchResult.Good => GoodPoints,
        MatchResult.Early => EarlyPoints,
        MatchResult.Late => LatePoints,
        MatchResult.MissedNote => MissedPenalty,
        MatchResult.ExtraNote => ExtraPenalty,
        MatchResult.NoMatch => MissedPenalty,
        _ => 0
      };

      // Apply timing penalty modifier (reduces points based on error magnitude)
      if (result == MatchResult.Good || result == MatchResult.Early || result == MatchResult.Late)
      {
        double timingPenalty = Math.Abs(timingError) * 100; // Penalty increases with error
        basePoints = (long)(basePoints - timingPenalty);
        basePoints = Math.Max(0, basePoints);
      }

      return basePoints;
    }

    /// <summary>
    /// Determines a letter grade based on accuracy.
    /// </summary>
    private string CalculateGrade(PerformanceMetrics metrics)
    {
      double accuracy = metrics.Accuracy;

      return accuracy switch
      {
        >= 0.95 => "S", // Perfect
        >= 0.90 => "A", // Excellent
        >= 0.80 => "B", // Good
        >= 0.70 => "C", // Average
        >= 0.60 => "D", // Poor
        _ => "F"        // Fail
      };
    }

    /// <summary>
    /// Creates a detailed performance summary string.
    /// </summary>
    public string GenerateReport(PerformanceMetrics metrics)
    {
      var report = new System.Text.StringBuilder();

      report.AppendLine("=== Performance Report ===");
      report.AppendLine($"Grade: {metrics.Grade}");
      report.AppendLine($"Score: {metrics.Score}");
      report.AppendLine($"Accuracy: {metrics.Accuracy:P1}");
      report.AppendLine($"Precision: {metrics.Precision:P1}");
      report.AppendLine();
      report.AppendLine("Notes:");
      report.AppendLine($"  Perfect:  {metrics.PerfectNotes}");
      report.AppendLine($"  Good:     {metrics.GoodNotes}");
      report.AppendLine($"  Early:    {metrics.EarlyNotes}");
      report.AppendLine($"  Late:     {metrics.LateNotes}");
      report.AppendLine($"  Missed:   {metrics.MissedNotes}");
      report.AppendLine($"  Extra:    {metrics.ExtraNotes}");
      report.AppendLine();
      report.AppendLine($"Average Timing Error: {metrics.AverageTimingError:F1}ms");

      return report.ToString();
    }
  }
}
