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
        int currentMeasure = 1;
        int numerator = 4, denominator = 4;

        foreach (var measure in measures)
        {
          var measureObj = new Measure(currentMeasure, currentTime);
          measureObj.Numerator = numerator;
          measureObj.Denominator = denominator;

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

          // Parse notes in measure
          var notes = measure.Elements().Where(e => e.Name.LocalName == "note").ToList();
          double noteStartBeat = 0;

          foreach (var noteElem in notes)
          {
            var note = ParseNote(noteElem, currentTime + (noteStartBeat * 60.0 / (score.Bpm * 4.0)));
            note.StartBeat = noteStartBeat;

            // Update beat position
            var durationElem = noteElem.Elements().FirstOrDefault(e => e.Name.LocalName == "duration");
            if (durationElem != null && int.TryParse(durationElem.Value, out int dur))
            {
              noteStartBeat += (double)dur / score.Divisions;
            }

            if (!note.IsRest)
              measureObj.Notes.Add(note);
          }

          staff.Measures.Add(measureObj);
          currentTime += measureObj.GetDuration(score.Bpm);
          currentMeasure++;
        }
      }
    }

    private void ParseScoreTimewise(XElement rootElement, MusicXmlScore score)
    {
      // TODO: Implement score-timewise parsing
      // This is less common than score-partwise, implement if needed
      throw new NotImplementedException("Score-timewise parsing not yet implemented");
    }

    private Note ParseNote(XElement noteElem, double startTime)
    {
      var note = new Note { StartTime = startTime };

      // Check if rest
      var restElem = noteElem.Elements().FirstOrDefault(e => e.Name.LocalName == "rest");
      if (restElem != null)
      {
        note.IsRest = true;
        return note;
      }

      // Parse pitch
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

      // Parse duration
      var durationElem = noteElem.Elements().FirstOrDefault(e => e.Name.LocalName == "duration");
      if (durationElem != null && int.TryParse(durationElem.Value, out int duration))
      {
        // Determine note type from divisions
        // This is a simplified mapping
        note.Duration = new Duration(Duration.NoteType.Quarter);
      }

      return note;
    }
  }
}
