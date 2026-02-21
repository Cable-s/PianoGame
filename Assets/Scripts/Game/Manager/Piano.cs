using UnityEngine;
using PianoGame.MIDI;
using PianoGame.MusicXML;
using PianoGame.Scoring;
using System;

public class Piano : MonoBehaviour
{
  public IScoreEngine ScoreEngine { get; private set; }
  public IMidiInputDevice MidiInputDevice { get; private set; }
  private MidiEventEmitter _midiEmitter;
  private GameController _gameController;

  private void Awake()
  {
    // Get GameController reference
    _gameController = GetComponent<GameController>();
    if (_gameController == null)
    {
      Debug.LogError("[PIANO] GameController not found on this GameObject!");
    }

    ScoreEngine = new ScoreEngine();

    // Create MIDI factory and device
    var factory = new RtMidiDeviceFactory();
    var availableDevices = factory.GetAvailableDevices();

    Debug.Log($"Available MIDI Devices: {availableDevices.Count}");
    for (int i = 0; i < availableDevices.Count; i++)
    {
      Debug.Log($"  {i}: {availableDevices[i]}");
    }

    // Open first available device
    if (availableDevices.Count > 0)
    {
      try
      {
        MidiInputDevice = factory.CreateDevice(0);
        Debug.Log($"[MIDI] Created device: {MidiInputDevice.DeviceName}");

        MidiInputDevice.Open();
        Debug.Log($"[MIDI] Device opened successfully");
        Debug.Log($"[MIDI] Device connected: {MidiInputDevice.IsConnected}");
      }
      catch (Exception ex)
      {
        Debug.LogError($"[MIDI ERROR] Failed to open device: {ex.Message}\n{ex.StackTrace}");
        return;
      }
    }
    else
    {
      Debug.LogError("No MIDI devices found!");
      return;
    }

    // Create emitter and subscribe to events
    _midiEmitter = new MidiEventEmitter();
    _midiEmitter.RegisterDevice(MidiInputDevice);
    _midiEmitter.MidiEventOccurred += OnMidiEventReceived;

    Debug.Log("[MIDI] System initialized and listening...");
    Debug.Log("[MIDI] Play a note on your keyboard now!");
  }

  private void Update()
  {
    // Dispatch any queued MIDI events on the main thread
    if (MidiInputDevice != null)
    {
      MidiInputDevice.DispatchQueuedEvents();
    }
  }

  private void OnMidiEventReceived(object sender, MidiEvent midiEvent)
  {
    // Only process Note On events
    if (midiEvent is NoteOnEvent noteOn)
    {
      string noteName = MidiNoteToName(noteOn.Note);
      Debug.Log($"<color=cyan>Note ON: {noteName} (MIDI {noteOn.Note}) | Velocity: {noteOn.Velocity}</color>");

      // Pass to game controller for scoring
      if (_gameController != null)
      {
        _gameController.OnMidiNoteReceived(noteOn.Note);
      }
    }
    else if (midiEvent is NoteOffEvent noteOff)
    {
      string noteName = MidiNoteToName(noteOff.Note);
      Debug.Log($"<color=gray>Note OFF: {noteName} (MIDI {noteOff.Note})</color>");

      if (_gameController != null)
      {
        _gameController.OnMidiNoteReleased(noteOff.Note);
      }
    }
  }

  /// <summary>
  /// Converts MIDI note number (0-127) to note name with octave.
  /// Middle C (C4) = 60
  /// </summary>
  private string MidiNoteToName(byte midiNote)
  {
    string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    int octave = (midiNote / 12) - 1;
    int noteIndex = midiNote % 12;

    return $"{noteNames[noteIndex]}{octave}";
  }

  private void OnDestroy()
  {
    if (_midiEmitter != null)
    {
      _midiEmitter.MidiEventOccurred -= OnMidiEventReceived;
    }

    if (MidiInputDevice != null)
    {
      MidiInputDevice.Close();
      MidiInputDevice.Dispose();
    }
  }
}
