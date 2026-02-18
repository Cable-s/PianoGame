# RtMidi.NET Setup Guide

## Installation

### Step 1: Install NuGet Package

Using Package Manager Console in Visual Studio:
```powershell
Install-Package RtMidi.Core
```

Or using .NET CLI:
```bash
dotnet add package RtMidi.Core
```

### Step 2: Verify Installation

Check your `.csproj` file has:
```xml
<ItemGroup>
  <PackageReference Include="RtMidi.Core" Version="2.3.2" />
</ItemGroup>
```

## Implementation

### Basic Usage

```csharp
// 1. Create factory
var factory = new RtMidiDeviceFactory();

// 2. Get available devices
var devices = factory.GetAvailableDevices();
foreach (var device in devices)
    Console.WriteLine(device);

// 3. Create and open device
var midiDevice = factory.CreateDevice(0); // Device ID 0
midiDevice.Open();

// 4. Subscribe to events
midiDevice.EventReceived += (sender, midiEvent) =>
{
    Console.WriteLine($"Received: {midiEvent}");
};

// 5. Keep listening...
System.Threading.Thread.Sleep(5000);

// 6. Close and cleanup
midiDevice.Close();
midiDevice.Dispose();
```

### With Emitter and Event Listeners

```csharp
// Create components
var factory = new RtMidiDeviceFactory();
var emitter = new MidiEventEmitter();
var device = factory.CreateDevice(0);

// Open device
device.Open();
emitter.RegisterDevice(device);

// Subscribe a listener
emitter.Subscribe(new MyMidiListener());

// Listen...
System.Threading.Thread.Sleep(5000);

// Cleanup
device.Close();
device.Dispose();
```

## Key Classes

- **`RtMidiDeviceFactory`** - Creates and discovers MIDI devices
- **`MidiInputDevice`** - Represents a connected MIDI device
- **`MidiEventEmitter`** - Routes MIDI events to subscribers
- **`IMidiEventListener`** - Implement this to receive events

## Threading Model

- MIDI input runs on a background thread (non-blocking)
- `MidiInputThread()` reads messages continuously
- Events are fired on the background thread, so use thread-safe code

## Platform Support

RtMidi.Core supports:
- Windows (via WinMM API)
- macOS (via CoreMIDI)
- Linux (via ALSA)

Native libraries are automatically downloaded via NuGet.

## Troubleshooting

### "No MIDI devices found"
- Ensure your digital piano is connected via USB
- Check Device Manager (Windows) or System Report (macOS)
- Restart the application

### "RtMidi.Core not found"
- Run: `dotnet restore`
- Verify package version in `.csproj`

### "Device in use"
- Only one application can access a MIDI device at a time
- Close other MIDI applications (DAWs, music software, etc.)

## Performance Notes

- MIDI messages arrive with minimal latency (~5-10ms)
- Background thread sleeps 5ms between checks
- Adjust `Thread.Sleep(5)` in `MidiInputThread()` for different latency profiles
  - Lower = faster but higher CPU usage
  - Higher = slower but lower CPU usage

## Next Steps

1. Integrate with `ScoreEngine` to validate notes
2. Use `NoteMatcher` to compare input to expectations
3. Feed results to `Scorer` for performance metrics
