using System;

namespace PianoGame.MIDI
{
  /// <summary>
  /// Base class for all MIDI events.
  /// Represents a single MIDI message from an input device.
  /// </summary>
  public abstract class MidiEvent
  {
    public double Timestamp { get; set; }
    public byte Channel { get; set; }

    protected MidiEvent(double timestamp, byte channel)
    {
      Timestamp = timestamp;
      Channel = channel;
    }
  }

  /// <summary>
  /// MIDI Note On event - key pressed on input device
  /// </summary>
  public class NoteOnEvent : MidiEvent
  {
    public byte Note { get; set; }
    public byte Velocity { get; set; }

    public NoteOnEvent(double timestamp, byte channel, byte note, byte velocity)
        : base(timestamp, channel)
    {
      Note = note;
      Velocity = velocity;
    }

    public override string ToString()
        => $"NoteOn: Note={Note}, Velocity={Velocity}, Time={Timestamp}";
  }

  /// <summary>
  /// MIDI Note Off event - key released on input device
  /// </summary>
  public class NoteOffEvent : MidiEvent
  {
    public byte Note { get; set; }
    public byte Velocity { get; set; }

    public NoteOffEvent(double timestamp, byte channel, byte note, byte velocity = 0)
        : base(timestamp, channel)
    {
      Note = note;
      Velocity = velocity;
    }

    public override string ToString()
        => $"NoteOff: Note={Note}, Time={Timestamp}";
  }

  /// <summary>
  /// MIDI Control Change event
  /// </summary>
  public class ControlChangeEvent : MidiEvent
  {
    public byte Controller { get; set; }
    public byte Value { get; set; }

    public ControlChangeEvent(double timestamp, byte channel, byte controller, byte value)
        : base(timestamp, channel)
    {
      Controller = controller;
      Value = value;
    }

    public override string ToString()
        => $"CC: Controller={Controller}, Value={Value}, Time={Timestamp}";
  }

  /// <summary>
  /// MIDI Program Change event
  /// </summary>
  public class ProgramChangeEvent : MidiEvent
  {
    public byte Program { get; set; }

    public ProgramChangeEvent(double timestamp, byte channel, byte program)
        : base(timestamp, channel)
    {
      Program = program;
    }

    public override string ToString()
        => $"ProgramChange: Program={Program}, Time={Timestamp}";
  }

  /// <summary>
  /// MIDI Pitch Bend event
  /// </summary>
  public class PitchBendEvent : MidiEvent
  {
    public ushort Value { get; set; } // 0-16383, center is 8192

    public PitchBendEvent(double timestamp, byte channel, ushort value)
        : base(timestamp, channel)
    {
      Value = value;
    }

    public override string ToString()
        => $"PitchBend: Value={Value}, Time={Timestamp}";
  }
}
