## MIDI music practice game
I am building a Unity based rhythm / music training game that connects a MIDI device for a real-time feedback on performance. I wanted a way to practice sight reading. It is kinda like SimplyPiano, but for my desktop (and no payment plan...)


### How It Works
1. A MIDI device sends note messages to the application.
2. The game parses raw MIDI data into:
    - Status byte
    - Note value
    - Velocity
3. Notes are matched against an expected sequence or live timing window.
4. The system evaluates:
    - Correct note
    - Timing offset
    - Duration (if applicable)
5. Feedback is rendered visually and/or through scoring logic.

### Technical Architecture
#### MIDI Layer
- Native MIDI callback handling
- Byte-level parsing of MIDI messages
- Device selection support

#### Game Logic Layer
- Note event queue
- Timing window evaluation
- Accuracy grading (Perfect / Good / Miss)

#### Visual Layer
- Note rendering
- Timing indicators
- Performance feedback UI

### Requirements
- Unity (i am using 6000.3.81f)
- MIDI input device (works for YAMAHA P-125A, no other devices tested)
- Windows (MIDI layer uses native Windows MIDI APIs)
