using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;
using System.Linq;

namespace PianoGame.MusicXML
{
  /// <summary>
  /// Interface for parsing MusicXML files.
  /// </summary>
  public interface IMusicXmlParser
  {
    MusicXmlScore Parse(string xmlContent);
    MusicXmlScore ParseFile(string filePath);
  }

  /// <summary>
  /// MusicXML Parser - Converts MusicXML format to MusicXmlScore objects.
  /// Pure C# - no Unity dependencies.
  /// 
  /// Responsibility: Parse XML and build score data structure.
  /// Does NOT: Validate against MIDI input or contain game logic.
  /// </summary>
  public class MusicXmlParser : IMusicXmlParser
  {
    /// <summary>
    /// Parses MusicXML content from a string.
    /// </summary>
    public MusicXmlScore Parse(string xmlContent)
    {
      if (string.IsNullOrEmpty(xmlContent))
        throw new ArgumentException("XML content cannot be null or empty", nameof(xmlContent));

      try
      {
        var doc = XDocument.Parse(xmlContent);
        return ParseDocument(doc);
      }
      catch (XmlException ex)
      {
        throw new InvalidOperationException("Failed to parse MusicXML content", ex);
      }
    }

    /// <summary>
    /// Parses MusicXML from a file.
    /// </summary>
    public MusicXmlScore ParseFile(string filePath)
    {
      if (string.IsNullOrEmpty(filePath))
        throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

      try
      {
        var doc = XDocument.Load(filePath);
        return ParseDocument(doc);
      }
      catch (Exception ex)
      {
        throw new InvalidOperationException($"Failed to parse MusicXML file: {filePath}", ex);
      }
    }

    private MusicXmlScore ParseDocument(XDocument doc)
    {
      var rootElement = doc.Root;
      if (rootElement?.Name.LocalName != "score-partwise" && rootElement?.Name.LocalName != "score-timewise")
      {
        throw new InvalidOperationException("Invalid MusicXML document: root element must be score-partwise or score-timewise");
      }

      var score = new MusicXmlScore();

      // Parse metadata
      var workTitle = rootElement.Descendants().FirstOrDefault(e => e.Name.LocalName == "work-title")?.Value;
      if (!string.IsNullOrEmpty(workTitle))
        score.Title = workTitle;

      var composer = rootElement.Descendants().FirstOrDefault(e => e.Name.LocalName == "composer")?.Value;
      if (!string.IsNullOrEmpty(composer))
        score.Composer = composer;

      // Parse divisions (PPQ equivalent)
      var divisionsElem = rootElement.Descendants().FirstOrDefault(e => e.Name.LocalName == "divisions");
      if (divisionsElem != null && int.TryParse(divisionsElem.Value, out int divisions))
        score.Divisions = divisions;

      // Parse tempo
      var soundElem = rootElement.Descendants().FirstOrDefault(e => e.Name.LocalName == "sound");
      if (soundElem?.Attribute("tempo") != null && int.TryParse(soundElem.Attribute("tempo").Value, out int tempo))
        score.Bpm = tempo;

      // Parse staves (part-list and parts)
      if (rootElement.Name.LocalName == "score-partwise")
      {
        ParseScorePartwise(rootElement, score);
      }
      else
      {
        ParseScoreTimewise(rootElement, score);
      }

      return score;
    }

    private void ParseScorePartwise(XElement rootElement, MusicXmlScore score)
    {
      var parts = rootElement.Descendants().Where(e => e.Name.LocalName == "part").ToList();

      foreach (var part in parts)
      {
        var staff = new Staff(score.Staves.Count + 1);
        score.Staves.Add(staff);

        var measures = part.Elements().Where(e => e.Name.LocalName == "measure").ToList();
        double currentTime = 0;
        double currentBeatGlobal = 0;
        int sequentialMeasureNumber = 1;
        int numerator = 4, denominator = 4;

        foreach (var measure in measures)
        {
          int measureNumber = sequentialMeasureNumber;
          var numberAttr = measure.Attribute("number")?.Value;
          if (!string.IsNullOrWhiteSpace(numberAttr) && int.TryParse(numberAttr, out int parsedNumber))
            measureNumber = parsedNumber;

          // Parse time signature
          var attributesElem = measure.Element(measure.Name.NamespaceName + "attributes")
              ?? measure.Elements().FirstOrDefault(e => e.Name.LocalName == "attributes");

          if (attributesElem != null)
          {
            var timeElem = attributesElem.Elements().FirstOrDefault(e => e.Name.LocalName == "time");
            if (timeElem != null)
            {
              var beats = timeElem.Elements().FirstOrDefault(e => e.Name.LocalName == "beats")?.Value;
              var beatType = timeElem.Elements().FirstOrDefault(e => e.Name.LocalName == "beat-type")?.Value;

              if (int.TryParse(beats, out int b))
                numerator = b;
              if (int.TryParse(beatType, out int bt))
                denominator = bt;
            }
          }

          var measureObj = new Measure(measureNumber, currentTime);
          measureObj.Numerator = numerator;
          measureObj.Denominator = denominator;

          // Parse notes in measure
          // MusicXML encodes polyphony (multiple voices/staves) via <backup>/<forward>.
          // We need to respect those to get correct simultaneity.
          double cursorDivisions = 0;
          double maxCursorDivisions = 0;

          foreach (var elem in measure.Elements())
          {
            string name = elem.Name.LocalName;
            if (name == "backup")
            {
              var durElem = elem.Elements().FirstOrDefault(e => e.Name.LocalName == "duration");
              if (durElem != null && int.TryParse(durElem.Value, out int dur))
                cursorDivisions = Math.Max(0, cursorDivisions - dur);
              continue;
            }
            if (name == "forward")
            {
              var durElem = elem.Elements().FirstOrDefault(e => e.Name.LocalName == "duration");
              if (durElem != null && int.TryParse(durElem.Value, out int dur))
              {
                cursorDivisions += dur;
                maxCursorDivisions = Math.Max(maxCursorDivisions, cursorDivisions);
              }
              continue;
            }
            if (name != "note")
              continue;

            var noteElem = elem;
            bool isChordTone = noteElem.Elements().Any(e => e.Name.LocalName == "chord");

            // Start beat is the current cursor position (unless this is a chord tone).
            double noteStartBeat = cursorDivisions / score.Divisions;

            var note = ParseNote(noteElem, currentTime);
            note.MeasureNumber = measureNumber;
            note.StartBeat = noteStartBeat;
            note.StartBeatGlobal = currentBeatGlobal + noteStartBeat;
            note.StartTime = currentTime + (noteStartBeat * 60.0 / score.Bpm);

            // Keep rests too so timing/rendering can remain faithful.
            measureObj.Notes.Add(note);

            // Advance cursor by note duration (unless chord tone).
            var durationElem = noteElem.Elements().FirstOrDefault(e => e.Name.LocalName == "duration");
            if (!isChordTone && durationElem != null && int.TryParse(durationElem.Value, out int durNote))
            {
              cursorDivisions += durNote;
              maxCursorDivisions = Math.Max(maxCursorDivisions, cursorDivisions);
            }
          }

          double measureDurationBeats = maxCursorDivisions / score.Divisions;

          staff.Measures.Add(measureObj);
          // Advance time/beats based on the measure time signature (in quarter-note beats).
          double measureBeats = measureDurationBeats;
          if (measureBeats <= 0)
            measureBeats = numerator * (4.0 / denominator);

          currentBeatGlobal += measureBeats;
          currentTime += measureBeats * (60.0 / score.Bpm);
          sequentialMeasureNumber++;
        }
      }
    }

    private void ParseScoreTimewise(XElement rootElement, MusicXmlScore score)
    {
      // TODO: Implement score-timewise parsing
      // This is less common than score-partwise, implement if needed
      throw new NotImplementedException("Score-timewise parsing not yet implemented");
    }

    private Note ParseNote(XElement noteElem, double measureStartTime)
    {
      var note = new Note { StartTime = measureStartTime };

      // Parse staff assignment (for multi-staff instruments like piano)
      var staffElem = noteElem.Elements().FirstOrDefault(e => e.Name.LocalName == "staff")?.Value;
      if (int.TryParse(staffElem, out int staffNumber))
        note.StaffNumber = staffNumber;

      // Check if rest
      var restElem = noteElem.Elements().FirstOrDefault(e => e.Name.LocalName == "rest");
      bool isRest = restElem != null;
      if (isRest)
        note.IsRest = true;

      // Parse pitch (rests typically have no pitch)
      if (!isRest)
      {
        var pitchElem = noteElem.Elements().FirstOrDefault(e => e.Name.LocalName == "pitch");
        if (pitchElem != null)
        {
          var stepElem = pitchElem.Elements().FirstOrDefault(e => e.Name.LocalName == "step");
          var octaveElem = pitchElem.Elements().FirstOrDefault(e => e.Name.LocalName == "octave");
          var alterElem = pitchElem.Elements().FirstOrDefault(e => e.Name.LocalName == "alter");

          if (Enum.TryParse<Pitch.NoteName>(stepElem?.Value ?? "C", out var noteName))
          {
            int octave = int.TryParse(octaveElem?.Value, out int o) ? o : 4;
            int accidental = int.TryParse(alterElem?.Value, out int alt) ? alt : 0;
            note.Pitch = new Pitch(noteName, octave, accidental);
          }
        }
      }

      // Parse duration
      var durationElem = noteElem.Elements().FirstOrDefault(e => e.Name.LocalName == "duration");
      if (durationElem != null && int.TryParse(durationElem.Value, out int duration))
      {
        // Prefer MusicXML's <type> for rhythmic value.
        var typeElem = noteElem.Elements().FirstOrDefault(e => e.Name.LocalName == "type")?.Value;
        var dots = noteElem.Elements().Count(e => e.Name.LocalName == "dot");

        note.Duration = new Duration(ParseNoteType(typeElem), dots);
      }

      return note;
    }

    private static Duration.NoteType ParseNoteType(string musicXmlType)
    {
      if (string.IsNullOrEmpty(musicXmlType))
        return Duration.NoteType.Quarter;

      return musicXmlType.Trim().ToLowerInvariant() switch
      {
        "whole" => Duration.NoteType.Whole,
        "half" => Duration.NoteType.Half,
        "quarter" => Duration.NoteType.Quarter,
        "eighth" => Duration.NoteType.Eighth,
        "16th" => Duration.NoteType.Sixteenth,
        "sixteenth" => Duration.NoteType.Sixteenth,
        "32nd" => Duration.NoteType.ThirtySecond,
        "thirty-second" => Duration.NoteType.ThirtySecond,
        _ => Duration.NoteType.Quarter,
      };
    }
  }
}
