using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Melanchall.DryWetMidi.Core;


namespace PianoGame.MIDI
{
  /// <summary>
  /// Windows MIDI API P/Invoke declarations.
  /// </summary>
  internal static class MidiNative
  {
    // Callback delegate for MIDI input
    internal delegate void MidiInputProc(IntPtr hMidiIn, uint wMsg, IntPtr dwInstance, IntPtr dwParam1, IntPtr dwParam2);

    [DllImport("winmm.dll")]
    internal static extern uint midiInGetNumDevs();

    [DllImport("winmm.dll", CharSet = CharSet.Auto)]
    internal static extern uint midiInGetDevCaps(uint uDeviceID, ref MIDIINCAPS lpMidiInCaps, uint cbMidiInCaps);

    [DllImport("winmm.dll")]
    internal static extern uint midiInOpen(out IntPtr lphMidiIn, UIntPtr uDeviceID, MidiInputProc dwCallback, IntPtr dwInstance, uint dwFlags);

    [DllImport("winmm.dll")]
    internal static extern uint midiInOpen(out IntPtr lphMidiIn, UIntPtr uDeviceID, IntPtr dwCallback, IntPtr dwInstance, uint dwFlags);

    [DllImport("winmm.dll")]
    internal static extern uint midiInClose(IntPtr hMidiIn);

    [DllImport("winmm.dll")]
    internal static extern uint midiInStart(IntPtr hMidiIn);

    [DllImport("winmm.dll")]
    internal static extern uint midiInStop(IntPtr hMidiIn);

    [DllImport("winmm.dll")]
    internal static extern uint midiInReset(IntPtr hMidiIn);

    [DllImport("winmm.dll")]
    internal static extern uint midiInPrepareHeader(IntPtr hMidiIn, ref MIDIHDR lpMidiInHdr, uint cbMidiInHdr);

    [DllImport("winmm.dll")]
    internal static extern uint midiInAddBuffer(IntPtr hMidiIn, ref MIDIHDR lpMidiInHdr, uint cbMidiInHdr);

    [DllImport("winmm.dll")]
    internal static extern uint midiInUnprepareHeader(IntPtr hMidiIn, ref MIDIHDR lpMidiInHdr, uint cbMidiInHdr);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    internal struct MIDIINCAPS
    {
      public ushort wMid;
      public ushort wPid;
      public uint vDriverVersion;
      [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
      public string szPname;
      public uint dwSupport;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MIDIHDR
    {
      public IntPtr lpData;
      public uint dwBufferLength;
      public uint dwBytesRecorded;
      public IntPtr dwUser;
      public uint dwFlags;
      public IntPtr lpNext;
      public IntPtr reserved;
      public uint dwOffset;
      [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
      public IntPtr[] dwReserved;
    }

    internal const uint MMSYSERR_NOERROR = 0;
    internal const uint MIDIERR_NODEVICE = 4;
    internal const uint CALLBACK_NULL = 0;
    internal const uint CALLBACK_FUNCTION = 0x00030000;
    internal const uint MIM_OPEN = 961;
    internal const uint MIM_CLOSE = 962;
    internal const uint MIM_DATA = 963;
    internal const uint MIM_LONGDATA = 964;
    internal const uint MIM_ERROR = 965;
    internal const uint MIM_LONGERROR = 966;
    internal const uint MHDR_INQUEUE = 1;
    internal const uint MHDR_DONE = 1;
  }

  /// <summary>
  /// Interface for MIDI input devices.
  /// Pure interface - no Unity dependencies.
  /// Responsible only for reading MIDI messages and emitting events.
  /// </summary>
  public interface IMidiInputDevice : IDisposable
  {
    event EventHandler<MidiEvent> EventReceived;

    string DeviceName { get; }
    bool IsConnected { get; }

    void Open();
    void Close();
    void DispatchQueuedEvents();
  }

  /// <summary>
  /// MIDI Input Device implementation using Windows P/Invoke + DryWetMidi.Core.
  /// Handles USB communication with digital piano.
  /// </summary>
  public class MidiInputDevice : IMidiInputDevice
  {
    private IntPtr _midiInHandle = IntPtr.Zero;
    private bool _isConnected;
    private readonly int _deviceId;
    private readonly Stopwatch _stopwatch;
    private Thread _midiInputThread;
    private bool _isThreadRunning;
    private byte[] _buffer;
    private GCHandle _bufferHandle;
    private IntPtr _headerPtr; // Unmanaged memory for header
    private MidiNative.MidiInputProc _midiCallback; // Keep delegate alive
    private Queue<MidiNative.MIDIHDR> _completedBuffers = new Queue<MidiNative.MIDIHDR>();

    // Queue for events to be dispatched on main thread
    private readonly ConcurrentQueue<MidiEvent> _eventQueue = new ConcurrentQueue<MidiEvent>();

    public event EventHandler<MidiEvent> EventReceived;

    public string DeviceName { get; private set; }
    public bool IsConnected => _isConnected;

    public MidiInputDevice(int deviceId, string deviceName)
    {
      _deviceId = deviceId;
      DeviceName = deviceName;
      _isConnected = false;
      _stopwatch = new Stopwatch();
      _isThreadRunning = false;
    }

    public void Open()
    {
      if (_isConnected)
        return;

      try
      {
        //UnityEngine.Debug.Log($"[MIDI] Opening device {_deviceId} ({DeviceName})");

        // Create callback delegate (must keep it alive)
        _midiCallback = MidiInputCallback;

        // Open MIDI input device WITH callback
        uint result = MidiNative.midiInOpen(out _midiInHandle, new UIntPtr((uint)_deviceId), _midiCallback, IntPtr.Zero, MidiNative.CALLBACK_FUNCTION);

        if (result != MidiNative.MMSYSERR_NOERROR)
        {
          //UnityEngine.Debug.LogError($"[MIDI ERROR] midiInOpen failed with code {result}");
          throw new InvalidOperationException($"Failed to open MIDI device: error code {result}");
        }

        //UnityEngine.Debug.Log($"[MIDI] midiInOpen success with callback");

        // Create and prepare multiple buffers for MIDI messages
        _buffer = new byte[4096];
        _bufferHandle = GCHandle.Alloc(_buffer, GCHandleType.Pinned);

        // Allocate unmanaged memory for MIDI header
        _headerPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(MidiNative.MIDIHDR)));
        var hdr = new MidiNative.MIDIHDR()
        {
          lpData = _bufferHandle.AddrOfPinnedObject(),
          dwBufferLength = (uint)_buffer.Length,
          dwBytesRecorded = 0,
          dwFlags = 0
        };
        Marshal.StructureToPtr(hdr, _headerPtr, false);

        result = MidiNative.midiInPrepareHeader(_midiInHandle, ref hdr, (uint)Marshal.SizeOf(typeof(MidiNative.MIDIHDR)));
        if (result != MidiNative.MMSYSERR_NOERROR)
        {
          //UnityEngine.Debug.LogError($"[MIDI ERROR] midiInPrepareHeader failed with code {result}");
          Marshal.FreeHGlobal(_headerPtr);
          _bufferHandle.Free();
          MidiNative.midiInClose(_midiInHandle);
          throw new InvalidOperationException($"Failed to prepare MIDI buffer: error code {result}");
        }

        // Store prepared header back to unmanaged memory
        Marshal.StructureToPtr(hdr, _headerPtr, true);

        // Add buffer to the input queue
        result = MidiNative.midiInAddBuffer(_midiInHandle, ref hdr, (uint)Marshal.SizeOf(typeof(MidiNative.MIDIHDR)));
        if (result != MidiNative.MMSYSERR_NOERROR)
        {
          //UnityEngine.Debug.LogError($"[MIDI ERROR] midiInAddBuffer failed with code {result}");
          MidiNative.midiInUnprepareHeader(_midiInHandle, ref hdr, (uint)Marshal.SizeOf(typeof(MidiNative.MIDIHDR)));
          Marshal.FreeHGlobal(_headerPtr);
          _bufferHandle.Free();
          MidiNative.midiInClose(_midiInHandle);
          throw new InvalidOperationException($"Failed to add MIDI buffer: error code {result}");
        }

        // Start the MIDI input
        result = MidiNative.midiInStart(_midiInHandle);
        if (result != MidiNative.MMSYSERR_NOERROR)
        {
          //UnityEngine.Debug.LogError($"[MIDI ERROR] midiInStart failed with code {result}");
          MidiNative.midiInUnprepareHeader(_midiInHandle, ref hdr, (uint)Marshal.SizeOf(typeof(MidiNative.MIDIHDR)));
          Marshal.FreeHGlobal(_headerPtr);
          _bufferHandle.Free();
          MidiNative.midiInClose(_midiInHandle);
          throw new InvalidOperationException($"midiInStart failed: error code {result}");
        }

        // Start background thread for processing
        _isThreadRunning = true;
        _stopwatch.Restart();

        _midiInputThread = new Thread(MidiInputThread)
        {
          IsBackground = true,
          Name = $"MIDI-Input-{_deviceId}"
        };
        _midiInputThread.Start();

        //UnityEngine.Debug.Log($"[MIDI] Device opened with callback");
        _isConnected = true;
      }
      catch (Exception ex)
      {
        //UnityEngine.Debug.LogError($"[MIDI ERROR] Open exception: {ex.Message}");
        throw;
      }
    }

    /// <summary>
    /// Callback function called by Windows MIDI driver when MIDI messages arrive.
    /// For MIM_DATA: dwParam1 contains 3 MIDI bytes (status, data1, data2)
    /// </summary>
    private void MidiInputCallback(IntPtr hMidiIn, uint wMsg, IntPtr dwInstance, IntPtr dwParam1, IntPtr dwParam2)
    {
      try
      {
        switch (wMsg)
        {
          case MidiNative.MIM_DATA:
            // Extract MIDI bytes from dwParam1
            uint midiData = (uint)dwParam1.ToInt64();
            byte status = (byte)(midiData & 0xFF);
            byte data1 = (byte)((midiData >> 8) & 0xFF);
            byte data2 = (byte)((midiData >> 16) & 0xFF);

            double timestamp = _stopwatch.Elapsed.TotalSeconds;
            byte statusType = (byte)(status & 0xF0);
            byte channel = (byte)(status & 0x0F);

            if (statusType == 0x90 && data2 > 0) // Note On
            {
              var noteOn = new NoteOnEvent(timestamp, channel, data1, data2);
              //UnityEngine.Debug.Log($"[MIDI] Note ON: Ch {channel}, Note {data1}, Vel {data2}");
              _eventQueue.Enqueue(noteOn);
            }
            else if (statusType == 0x80 || (statusType == 0x90 && data2 == 0)) // Note Off
            {
              var noteOff = new NoteOffEvent(timestamp, channel, data1, data2);
              //UnityEngine.Debug.Log($"[MIDI] Note OFF: Ch {channel}, Note {data1}");
              _eventQueue.Enqueue(noteOff);
            }
            else if (statusType == 0xB0) // Control Change
            {
              var cc = new ControlChangeEvent(timestamp, channel, data1, data2);
              _eventQueue.Enqueue(cc);
            }
            else if (statusType == 0xC0) // Program Change
            {
              var pc = new ProgramChangeEvent(timestamp, channel, data1);
              _eventQueue.Enqueue(pc);
            }
            break;

          case MidiNative.MIM_LONGDATA:
            //UnityEngine.Debug.Log($"[MIDI CALLBACK] MIM_LONGDATA received");
            break;

          case MidiNative.MIM_OPEN:
            //UnityEngine.Debug.Log($"[MIDI CALLBACK] MIM_OPEN received");
            break;

          case MidiNative.MIM_CLOSE:
            //UnityEngine.Debug.Log($"[MIDI CALLBACK] MIM_CLOSE received");
            break;
        }
      }
      catch (Exception ex)
      {
        //UnityEngine.Debug.LogError($"[MIDI ERROR] Callback exception: {ex.Message}");
      }
    }

    public void Close()
    {
      if (!_isConnected)
        return;

      _isThreadRunning = false;

      if (_midiInputThread != null && _midiInputThread.IsAlive)
      {
        _midiInputThread.Join(1000);
      }

      if (_midiInHandle != IntPtr.Zero)
      {
        try
        {
          MidiNative.midiInStop(_midiInHandle);

          // Unprepare the buffer header if we have one
          if (_headerPtr != IntPtr.Zero)
          {
            var hdr = (MidiNative.MIDIHDR)Marshal.PtrToStructure(_headerPtr, typeof(MidiNative.MIDIHDR));
            MidiNative.midiInUnprepareHeader(_midiInHandle, ref hdr, (uint)Marshal.SizeOf(typeof(MidiNative.MIDIHDR)));
          }

          MidiNative.midiInClose(_midiInHandle);
          _midiInHandle = IntPtr.Zero;
        }
        catch { }
      }

      if (_headerPtr != IntPtr.Zero)
      {
        Marshal.FreeHGlobal(_headerPtr);
        _headerPtr = IntPtr.Zero;
      }

      if (_bufferHandle.IsAllocated)
      {
        _bufferHandle.Free();
      }

      _stopwatch.Stop();
      _isConnected = false;
    }

    private void MidiInputThread()
    {
      //UnityEngine.Debug.Log($"[MIDI] Processing thread started");

      while (_isThreadRunning && _midiInHandle != IntPtr.Zero)
      {
        try
        {
          // Check for completed buffers and process them
          if (_headerPtr != IntPtr.Zero)
          {
            var hdr = (MidiNative.MIDIHDR)Marshal.PtrToStructure(_headerPtr, typeof(MidiNative.MIDIHDR));

            // Check if buffer is marked DONE
            if ((hdr.dwFlags & MidiNative.MHDR_DONE) != 0 && hdr.dwBytesRecorded > 0)
            {
              //UnityEngine.Debug.Log($"[MIDI] Buffer completed with {hdr.dwBytesRecorded} bytes");
              ProcessMidiBuffer(hdr.dwBytesRecorded);

              // Reset and re-add to queue
              hdr.dwBytesRecorded = 0;
              hdr.dwFlags = 0;
              Marshal.StructureToPtr(hdr, _headerPtr, true);

              uint result = MidiNative.midiInAddBuffer(_midiInHandle, ref hdr, (uint)Marshal.SizeOf(typeof(MidiNative.MIDIHDR)));
              if (result != MidiNative.MMSYSERR_NOERROR)
              {
                //UnityEngine.Debug.LogError($"[MIDI ERROR] midiInAddBuffer failed: {result}");
                break;
              }
            }
          }

          Thread.Sleep(10);
        }
        catch (Exception ex)
        {
          //UnityEngine.Debug.LogError($"[MIDI ERROR] Processing thread exception: {ex.Message}");
        }
      }

      //UnityEngine.Debug.Log($"[MIDI] Processing thread ended");
    }

    /// <summary>
    /// Process raw MIDI bytes from buffer and emit events.
    /// </summary>
    private void ProcessMidiBuffer(uint bytesRead)
    {
      int i = 0;
      while (i < bytesRead)
      {
        byte status = _buffer[i];
        if (status == 0) break; // End of data

        byte statusType = (byte)(status & 0xF0);
        byte channel = (byte)(status & 0x0F);
        double timestamp = _stopwatch.Elapsed.TotalSeconds;

        try
        {
          if (statusType == 0x90 && i + 2 < bytesRead) // Note On
          {
            byte note = _buffer[i + 1];
            byte velocity = _buffer[i + 2];

            if (velocity > 0)
            {
              var noteOn = new NoteOnEvent(timestamp, channel, note, velocity);
              //UnityEngine.Debug.Log($"[MIDI] Note ON: Ch {channel}, Note {note}, Vel {velocity}");
              _eventQueue.Enqueue(noteOn);
            }
            else
            {
              var noteOff = new NoteOffEvent(timestamp, channel, note, 0);
              //UnityEngine.Debug.Log($"[MIDI] Note OFF: Ch {channel}, Note {note}");
              _eventQueue.Enqueue(noteOff);
            }
            i += 3;
          }
          else if (statusType == 0x80 && i + 2 < bytesRead) // Note Off
          {
            byte note = _buffer[i + 1];
            byte velocity = _buffer[i + 2];
            var noteOff = new NoteOffEvent(timestamp, channel, note, velocity);
            //UnityEngine.Debug.Log($"[MIDI] Note OFF: Ch {channel}, Note {note}");
            _eventQueue.Enqueue(noteOff);
            i += 3;
          }
          else if (statusType == 0xB0 && i + 2 < bytesRead) // Control Change
          {
            byte controller = _buffer[i + 1];
            byte value = _buffer[i + 2];
            var cc = new ControlChangeEvent(timestamp, channel, controller, value);
            _eventQueue.Enqueue(cc);
            i += 3;
          }
          else
          {
            i++; // Skip unknown message
          }
        }
        catch (Exception ex)
        {
          //UnityEngine.Debug.LogError($"[MIDI ERROR] Parse error: {ex.Message}");
          i++;
        }
      }
    }

    /// <summary>
    /// Dispatch all queued MIDI events. Call this from the main thread (e.g., from Unity Update).
    /// </summary>
    public void DispatchQueuedEvents()
    {
      while (_eventQueue.TryDequeue(out var midiEvent))
      {
        EventReceived?.Invoke(this, midiEvent);
      }
    }

    public void Dispose()
    {
      Close();
    }
  }

  /// <summary>
  /// Factory for creating and discovering MIDI input devices.
  /// Platform-agnostic interface.
  /// </summary>
  public interface IMidiDeviceFactory
  {
    IReadOnlyList<string> GetAvailableDevices();
    IMidiInputDevice CreateDevice(int deviceId);
  }

  /// <summary>
  /// Factory implementation using Windows P/Invoke.
  /// Enumerates MIDI devices on Windows platform.
  /// </summary>
  public class RtMidiDeviceFactory : IMidiDeviceFactory
  {
    /// <summary>
    /// Returns list of available MIDI input device names using P/Invoke.
    /// </summary>
    public IReadOnlyList<string> GetAvailableDevices()
    {
      var devices = new List<string>();

      try
      {
        uint deviceCount = MidiNative.midiInGetNumDevs();
        //UnityEngine.Debug.Log($"[MIDI] Total MIDI input devices: {deviceCount}");

        for (int i = 0; i < deviceCount; i++)
        {
          var caps = new MidiNative.MIDIINCAPS();
          uint result = MidiNative.midiInGetDevCaps((uint)i, ref caps, (uint)Marshal.SizeOf(typeof(MidiNative.MIDIINCAPS)));

          if (result == MidiNative.MMSYSERR_NOERROR)
          {
            devices.Add(caps.szPname);
            //UnityEngine.Debug.Log($"[MIDI]   Device {i}: {caps.szPname}");
          }
          else
          {
            //UnityEngine.Debug.LogWarning($"[MIDI] Failed to get caps for device {i}: error {result}");
          }
        }
      }
      catch (Exception ex)
      {
        //UnityEngine.Debug.LogError($"[MIDI ERROR] Error enumerating devices: {ex.Message}");
      }

      return devices.AsReadOnly();
    }

    /// <summary>
    /// Creates a MIDI input device by device ID.
    /// </summary>
    public IMidiInputDevice CreateDevice(int deviceId)
    {
      try
      {
        uint deviceCount = MidiNative.midiInGetNumDevs();
        if (deviceId < 0 || deviceId >= deviceCount)
        {
          throw new ArgumentOutOfRangeException(nameof(deviceId),
            $"Device ID {deviceId} is out of range. Available devices: {deviceCount}");
        }

        // Get device name
        var caps = new MidiNative.MIDIINCAPS();
        uint result = MidiNative.midiInGetDevCaps((uint)deviceId, ref caps, (uint)Marshal.SizeOf(typeof(MidiNative.MIDIINCAPS)));

        if (result != MidiNative.MMSYSERR_NOERROR)
        {
          throw new InvalidOperationException($"Failed to get device caps for ID {deviceId}: error {result}");
        }

        return new MidiInputDevice(deviceId, caps.szPname);
      }
      catch (Exception ex)
      {
        throw new InvalidOperationException(
          $"Failed to create MIDI device for ID {deviceId}: {ex.Message}", ex);
      }
    }
  }
}
