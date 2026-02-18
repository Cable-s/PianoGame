using UnityEngine;
using PianoGame.MusicXML;

/// <summary>
/// Renders a single "next note" on a grand staff (treble + bass) in world space.
/// 
/// Usage:
/// - Add this component to your staff GameObject.
/// - Assign treble/bass bottom-line markers (empty transforms placed on the bottom staff line).
/// - Assign note prefabs (whole/half/quarter/eighth) and optional sharp/flat prefabs.
/// - Call ShowNextNote(note) when the expected note changes.
/// </summary>
public class GrandStaffNoteRenderer : MonoBehaviour
{
  [Header("Prefabs")]
  [SerializeField] private GameObject wholeNotePrefab;
  [SerializeField] private GameObject halfNotePrefab;
  [SerializeField] private GameObject quarterNotePrefab;
  [SerializeField] private GameObject eighthNotePrefab;

  [Header("Accidentals")]
  [SerializeField] private GameObject sharpPrefab;
  [SerializeField] private GameObject flatPrefab;
  [SerializeField] private float accidentalXOffset = -0.25f;

  [Header("Grand Staff Metrics (Local Space)")]
  [Tooltip("Fixed local X where the note should be played.")]
  [SerializeField] private float noteLocalX = -2.5f;

  [Tooltip("Fixed local Z where the note should be played.")]
  [SerializeField] private float noteLocalZ = -1f;

  [Tooltip("Distance in local units between adjacent staff lines (line-to-line).")]
  [SerializeField] private float staffLineSpacing = 0.24f;

  [Header("Treble Reference (Local Y)")]
  [Tooltip("Local Y of the treble staff top line (F).")]
  [SerializeField] private float trebleTopLineY = 1.68f;

  [Tooltip("Local Y of the treble staff bottom line (E).")]
  [SerializeField] private float trebleBottomLineY = 0.72f;

  [Header("Bass Reference (Local Y)")]
  [Tooltip("Local Y of the bass staff top line (A).")]
  [SerializeField] private float bassTopLineY = -0.48f;

  [Tooltip("Local Y of the bass staff bottom line (G).")]
  [SerializeField] private float bassBottomLineY = -1.44f;

  [Tooltip("Notes at or above this MIDI note go to treble; below go to bass. Middle C (C4) = 60.")]
  [SerializeField] private int splitMidiNote = 60;

  [Tooltip("If your staff art is inverted on Y, enable this to flip the pitch-to-Y direction.")]
  [SerializeField] private bool invertYOffset = false;

  private GameObject _activeNote;
  private GameObject _activeAccidental;

  private static readonly Pitch TrebleBottomLinePitch = new Pitch(Pitch.NoteName.E, 4);
  private static readonly Pitch BassBottomLinePitch = new Pitch(Pitch.NoteName.G, 2);

  public void Clear()
  {
    if (_activeNote != null)
    {
      Destroy(_activeNote);
      _activeNote = null;
    }

    if (_activeAccidental != null)
    {
      Destroy(_activeAccidental);
      _activeAccidental = null;
    }
  }

  public void ShowNextNote(Note note)
  {
    if (note == null)
    {
      Clear();
      return;
    }

    if (note.IsRest)
    {
      // No rest glyphs yet.
      Clear();
      return;
    }

    var notePrefab = GetNotePrefab(note.Duration.Type);
    if (notePrefab == null)
    {
      Debug.LogWarning($"[STAFF] Missing note prefab for duration {note.Duration.Type}.");
      return;
    }

    // Recreate the note object each time the note changes.
    Clear();

    float localY = PitchToLocalY(note.Pitch);
    var localPos = new Vector3(noteLocalX, localY, noteLocalZ);

    _activeNote = SpawnUnderStaffKeepingPrefabScale(notePrefab, localPos);

    TryCreateAccidental(note.Pitch.Accidental, localPos);
  }

  private void TryCreateAccidental(int accidental, Vector3 noteLocalPosition)
  {
    if (accidental == 0)
      return;

    // Only render a single sharp/flat symbol for now.
    if (accidental > 0)
    {
      if (sharpPrefab == null) return;
      _activeAccidental = SpawnUnderStaffKeepingPrefabScale(
        sharpPrefab,
        noteLocalPosition + new Vector3(accidentalXOffset, -.2f, 0f));
    }
    else
    {
      if (flatPrefab == null) return;
      _activeAccidental = SpawnUnderStaffKeepingPrefabScale(
        flatPrefab,
        noteLocalPosition + new Vector3(accidentalXOffset, -.2f, 0f));
    }
  }

  private GameObject SpawnUnderStaffKeepingPrefabScale(GameObject prefab, Vector3 localPosition)
  {
    // Instantiate in world space first, then parent while preserving world transform.
    // This prevents the staff's scale from inflating the spawned prefab.
    Vector3 worldPos = transform.TransformPoint(localPosition);
    Quaternion worldRot = transform.rotation;

    var instance = Instantiate(prefab, worldPos, worldRot);
    instance.transform.SetParent(transform, true);
    return instance;
  }

  private GameObject GetNotePrefab(Duration.NoteType type)
  {
    return type switch
    {
      Duration.NoteType.Whole => wholeNotePrefab,
      Duration.NoteType.Half => halfNotePrefab,
      Duration.NoteType.Quarter => quarterNotePrefab,
      Duration.NoteType.Eighth => eighthNotePrefab,
      _ => quarterNotePrefab,
    };
  }

  private float PitchToLocalY(Pitch pitch)
  {
    bool useTreble = pitch.ToMidiNote() >= splitMidiNote;

    // Each diatonic step (line<->space) is half a line spacing.
    // With your measurements: line-to-line = 0.24, so line-to-space = 0.12.
    float stepHeight = staffLineSpacing * 0.5f;

    Pitch referencePitch = useTreble ? TrebleBottomLinePitch : BassBottomLinePitch;
    float referenceY = useTreble ? trebleBottomLineY : bassBottomLineY;

    int deltaSteps = DiatonicIndex(pitch) - DiatonicIndex(referencePitch);
    float offset = deltaSteps * stepHeight;
    return referenceY + (invertYOffset ? -offset : offset);
  }

  private static int DiatonicIndex(Pitch pitch)
  {
    return (pitch.Octave * 7) + NoteLetterIndex(pitch.Note);
  }

  private static int NoteLetterIndex(Pitch.NoteName note)
  {
    return note switch
    {
      Pitch.NoteName.C => 0,
      Pitch.NoteName.D => 1,
      Pitch.NoteName.E => 2,
      Pitch.NoteName.F => 3,
      Pitch.NoteName.G => 4,
      Pitch.NoteName.A => 5,
      Pitch.NoteName.B => 6,
      _ => 0,
    };
  }
}
