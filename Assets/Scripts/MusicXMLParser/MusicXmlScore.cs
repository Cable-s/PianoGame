using System;
using System.Collections.Generic;

namespace PianoGame.MusicXML
{
  /// <summary>
  /// Represents a musical note's pitch and octave.
  /// Pure data - no Unity dependencies.
  /// </summary>
  public struct Pitch : IEquatable<Pitch>
  {
    public enum NoteName { C, D, E, F, G, A, B }

    public NoteName Note { get; set; }
    public int Octave { get; set; }
    public int Accidental { get; set; } // -2 to +2 for flats/sharps

    public Pitch(NoteName note, int octave, int accidental = 0)
    {
      Note = note;
      Octave = octave;
      Accidental = accidental;
    }

    /// <summary>
    /// Converts to MIDI note number (0-127).
    /// Middle C (C4) = 60.
    /// </summary>
    public byte ToMidiNote()
    {
      int semitone = Note switch
      {
        NoteName.C => 0,
        NoteName.D => 2,
        NoteName.E => 4,
        NoteName.F => 5,
        NoteName.G => 7,
        NoteName.A => 9,
        NoteName.B => 11,
        _ => 0,
      };

      int midiNote = (Octave + 1) * 12 + semitone + Accidental;
      return (byte)Math.Clamp(midiNote, 0, 127);
    }

    public static Pitch FromMidiNote(byte midiNote)
    {
      int octave = (midiNote / 12) - 1;
      int noteValue = midiNote % 12;

      // Map MIDI semitone values (0-11) to NoteName + Accidental
      // 0=C, 1=C#, 2=D, 3=D#, 4=E, 5=F, 6=F#, 7=G, 8=G#, 9=A, 10=A#, 11=B
      NoteName note;
      int accidental;

      switch (noteValue)
      {
        case 0: note = NoteName.C; accidental = 0; break;
        case 1: note = NoteName.C; accidental = 1; break; // C#
        case 2: note = NoteName.D; accidental = 0; break;
        case 3: note = NoteName.D; accidental = 1; break; // D#
        case 4: note = NoteName.E; accidental = 0; break;
        case 5: note = NoteName.F; accidental = 0; break;
        case 6: note = NoteName.F; accidental = 1; break; // F#
        case 7: note = NoteName.G; accidental = 0; break;
        case 8: note = NoteName.G; accidental = 1; break; // G#
        case 9: note = NoteName.A; accidental = 0; break;
        case 10: note = NoteName.A; accidental = 1; break; // A#
        case 11: note = NoteName.B; accidental = 0; break;
        default: note = NoteName.C; accidental = 0; break;
      }

      return new Pitch(note, octave, accidental);
    }

    public bool Equals(Pitch other)
        => Note == other.Note && Octave == other.Octave && Accidental == other.Accidental;

    public override bool Equals(object obj)
        => obj is Pitch pitch && Equals(pitch);

    public override int GetHashCode()
        => HashCode.Combine(Note, Octave, Accidental);

    public override string ToString()
    {
      string noteName = Note switch
      {
        NoteName.C => "C",
        NoteName.D => "D",
        NoteName.E => "E",
        NoteName.F => "F",
        NoteName.G => "G",
        NoteName.A => "A",
        NoteName.B => "B",
        _ => "?"
      };

      if (Accidental > 0)
        noteName += new string('#', Accidental);
      else if (Accidental < 0)
        noteName += new string('b', -Accidental);

      return $"{noteName}{Octave}";
    }
  }

  /// <summary>
  /// Represents the duration and rhythm of a note.
  /// </summary>
  public struct Duration : IEquatable<Duration>
  {
    public enum NoteType { Whole, Half, Quarter, Eighth, Sixteenth, ThirtySecond }

    public NoteType Type { get; set; }
    public int Dots { get; set; } // For dotted notes
    public int Tuplet { get; set; } // For tuplets (triplets = 3, etc)

    public Duration(NoteType type, int dots = 0, int tuplet = 1)
    {
      Type = type;
      Dots = dots;
      Tuplet = tuplet;
    }

    /// <summary>
    /// Returns the duration in beats (assuming quarter note = 1 beat).
    /// </summary>
    public double GetBeats(int divisionsPerQuarter = 4)
    {
      double baseBeat = Type switch
      {
        NoteType.Whole => 4.0,
        NoteType.Half => 2.0,
        NoteType.Quarter => 1.0,
        NoteType.Eighth => 0.5,
        NoteType.Sixteenth => 0.25,
        NoteType.ThirtySecond => 0.125,
        _ => 1.0
      };

      // Apply dots
      double dotMultiplier = 1.0;
      for (int i = 0; i < Dots; i++)
        dotMultiplier += Math.Pow(0.5, i + 2);

      // Apply tuplet
      return (baseBeat * dotMultiplier) / Tuplet;
    }

    public bool Equals(Duration other)
        => Type == other.Type && Dots == other.Dots && Tuplet == other.Tuplet;

    public override bool Equals(object obj)
        => obj is Duration duration && Equals(duration);

    public override int GetHashCode()
        => HashCode.Combine(Type, Dots, Tuplet);
  }

  /// <summary>
  /// Represents a single note in a score.
  /// Includes pitch, duration, and timing information.
  /// </summary>
  public class Note
  {
    public Pitch Pitch { get; set; }
    public Duration Duration { get; set; }
    public double StartBeat { get; set; } // Position in the measure (in beats)
    public double StartTime { get; set; } // Absolute time in seconds
    public double StartBeatGlobal { get; set; } // Absolute position in the score (in beats)
    public int MeasureNumber { get; set; } // 1-based measure number
    public int StaffNumber { get; set; } // MusicXML staff number (1=top/treble, 2=bottom/bass)
    public bool IsRest { get; set; }

    public Note()
    {
      IsRest = false;
      Duration = new Duration(Duration.NoteType.Quarter);
    }

    public Note(Pitch pitch, Duration duration, double startBeat, double startTime)
    {
      Pitch = pitch;
      Duration = duration;
      StartBeat = startBeat;
      StartTime = startTime;
      IsRest = false;
    }

    public override string ToString()
        => IsRest ? "Rest" : $"{Pitch} ({Duration.Type})";
  }

  /// <summary>
  /// Represents a measure (bar) in a score.
  /// Contains multiple notes and timing information.
  /// </summary>
  public class Measure
  {
    public int Number { get; set; }
    public List<Note> Notes { get; set; } = new List<Note>();
    public double StartTime { get; set; } // In seconds
    public int Numerator { get; set; } = 4; // Time signature numerator
    public int Denominator { get; set; } = 4; // Time signature denominator

    public Measure(int number, double startTime)
    {
      Number = number;
      StartTime = startTime;
    }

    public double GetDuration(int bpm)
    {
      double beatsPerMeasure = (double)Numerator;
      double beatsPerSecond = bpm / 60.0;
      return beatsPerMeasure / beatsPerSecond;
    }
  }

  /// <summary>
  /// Represents a staff (e.g., treble or bass clef).
  /// </summary>
  public class Staff
  {
    public int Number { get; set; }
    public string ClefType { get; set; } = "treble"; // treble, bass, alto, etc.
    public List<Measure> Measures { get; set; } = new List<Measure>();

    public Staff(int number)
    {
      Number = number;
    }
  }

  /// <summary>
  /// Represents a complete musical score.
  /// This is the data structure that defines expectations for the Score Engine.
  /// 
  /// Responsibility: Represents score structure and metadata.
  /// Does NOT: Process MIDI input, validate events, or contain game logic.
  /// </summary>
  public class MusicXmlScore
  {
    public string Title { get; set; }
    public string Composer { get; set; }
    public int Bpm { get; set; } = 120;
    public int Divisions { get; set; } = 4; // Divisions per quarter note
    public List<Staff> Staves { get; set; } = new List<Staff>();

    /// <summary>
    /// Gets the total duration of the score in seconds.
    /// </summary>
    public double GetTotalDuration()
    {
      double totalTime = 0;
      foreach (var staff in Staves)
      {
        foreach (var measure in staff.Measures)
        {
          totalTime = Math.Max(totalTime, measure.StartTime + measure.GetDuration(Bpm));
        }
      }
      return totalTime;
    }

    /// <summary>
    /// Gets all notes from all staves in chronological order.
    /// </summary>
    public List<Note> GetAllNotes(bool includeRests = false)
    {
      var allNotes = new List<Note>();
      foreach (var staff in Staves)
      {
        foreach (var measure in staff.Measures)
        {
          if (includeRests)
          {
            allNotes.AddRange(measure.Notes);
          }
          else
          {
            foreach (var n in measure.Notes)
            {
              if (n != null && !n.IsRest)
                allNotes.Add(n);
            }
          }
        }
      }
      allNotes.Sort((a, b) => a.StartBeatGlobal.CompareTo(b.StartBeatGlobal));
      return allNotes;
    }
  }
}
