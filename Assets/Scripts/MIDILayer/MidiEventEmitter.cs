using System;
using System.Collections.Generic;

namespace PianoGame.MIDI
{
  /// <summary>
  /// MIDI Event Emitter - Dispatches MIDI input events to subscribers.
  /// 
  /// Responsibility: Route MIDI input events to listeners.
  /// Does NOT: Process events, validate against score, or contain game logic.
  /// </summary>
  public interface IMidiEventEmitter
  {
    event EventHandler<MidiEvent> MidiEventOccurred;

    void Subscribe(IMidiEventListener listener);
    void Unsubscribe(IMidiEventListener listener);
  }

  /// <summary>
  /// Listener interface for components that consume MIDI events.
  /// </summary>
  public interface IMidiEventListener
  {
    void OnMidiEvent(MidiEvent midiEvent);
  }

  /// <summary>
  /// Standard implementation of MIDI Event Emitter.
  /// Aggregates input from one or more MIDI devices and emits events.
  /// </summary>
  public class MidiEventEmitter : IMidiEventEmitter
  {
    private readonly List<IMidiEventListener> _listeners = new List<IMidiEventListener>();
    private readonly Dictionary<IMidiInputDevice, bool> _devices = new Dictionary<IMidiInputDevice, bool>();

    public event EventHandler<MidiEvent> MidiEventOccurred;

    /// <summary>
    /// Registers a MIDI input device to emit events from.
    /// </summary>
    public void RegisterDevice(IMidiInputDevice device)
    {
      if (device == null)
        throw new ArgumentNullException(nameof(device));

      if (!_devices.ContainsKey(device))
      {
        device.EventReceived += OnDeviceEventReceived;
        _devices[device] = true;
      }
    }

    /// <summary>
    /// Unregisters a MIDI input device.
    /// </summary>
    public void UnregisterDevice(IMidiInputDevice device)
    {
      if (device != null && _devices.ContainsKey(device))
      {
        device.EventReceived -= OnDeviceEventReceived;
        _devices.Remove(device);
      }
    }

    /// <summary>
    /// Subscribes a listener to receive all MIDI events.
    /// </summary>
    public void Subscribe(IMidiEventListener listener)
    {
      if (listener != null && !_listeners.Contains(listener))
      {
        _listeners.Add(listener);
      }
    }

    /// <summary>
    /// Unsubscribes a listener from MIDI events.
    /// </summary>
    public void Unsubscribe(IMidiEventListener listener)
    {
      if (listener != null)
      {
        _listeners.Remove(listener);
      }
    }

    /// <summary>
    /// Internal handler for device events.
    /// Routes events to all subscribers.
    /// </summary>
    private void OnDeviceEventReceived(object sender, MidiEvent midiEvent)
    {
      // Emit to event subscribers
      MidiEventOccurred?.Invoke(this, midiEvent);

      // Notify listener subscribers
      foreach (var listener in _listeners)
      {
        listener.OnMidiEvent(midiEvent);
      }
    }
  }
}
