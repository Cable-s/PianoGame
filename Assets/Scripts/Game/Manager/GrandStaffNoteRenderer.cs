using System.Collections.Generic;
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
  [Header("Scrolling (Optional)")]
  [Tooltip("All spawned notes/rests/lines are parented under this transform so the score can scroll smoothly. If null, a child named 'ScoreRoot' is created at runtime.")]
  [SerializeField] private Transform scrollRoot;

  [Header("Prefabs")]
  [SerializeField] private GameObject wholeNotePrefab;
  [SerializeField] private GameObject halfNotePrefab;
  [SerializeField] private GameObject quarterNotePrefab;
  [SerializeField] private GameObject eighthNotePrefab;

  [Header("Rest Prefabs")]
  [Tooltip("Prefab for a whole rest.")]
  [SerializeField] private GameObject wholeRestPrefab;

  [Tooltip("Prefab for a half rest.")]
  [SerializeField] private GameObject halfRestPrefab;

  [Tooltip("Prefab for a quarter rest.")]
  [SerializeField] private GameObject quarterRestPrefab;

  [Tooltip("Prefab for an eighth rest.")]
  [SerializeField] private GameObject eighthRestPrefab;

  [Tooltip("Optional Y offset applied only to rests (useful for prefab pivot differences).")]
  [SerializeField] private float restLocalYOffset = 0.0f;

  [Header("Calibration Markers (Optional)")]
  [Tooltip("If assigned, the renderer will derive staff line spacing + reference Ys from these markers.")]
  [SerializeField] private bool autoCalibrateFromMarkers = true;

  [Tooltip("Marker placed on the treble staff TOP line (F5).")]
  [SerializeField] private Transform trebleTopLineMarker;

  [Tooltip("Marker placed on the treble staff BOTTOM line (E4).")]
  [SerializeField] private Transform trebleBottomLineMarker;

  [Tooltip("Marker placed at the notehead center for treble G (G4, first G above middle C).")]
  [SerializeField] private Transform trebleG4Marker;

  [Tooltip("Marker placed at the notehead center for treble C (C5, octave above middle C).")]
  [SerializeField] private Transform trebleC5Marker;

  [Tooltip("Marker placed on the bass staff TOP line (A3).")]
  [SerializeField] private Transform bassTopLineMarker;

  [Tooltip("Marker placed on the bass staff BOTTOM line (G2).")]
  [SerializeField] private Transform bassBottomLineMarker;

  [Tooltip("Marker placed at the notehead center for bass F (F3, first F below middle C).")]
  [SerializeField] private Transform bassF3Marker;

  [Tooltip("Marker placed at the notehead center for bass C (C3, octave below middle C).")]
  [SerializeField] private Transform bassC3Marker;

  [Header("Alignment Offsets")]
  [Tooltip("Applied to ALL pitch-derived Y positions (notes + ledger lines). Use this for global alignment without breaking ledger alignment.")]
  [SerializeField] private float pitchGridLocalYOffset = 0.0f;

  [Tooltip("Applied only to notehead Y (not ledger lines). Prefer fixing prefab pivots first.")]
  [SerializeField] private float noteHeadLocalYOffset = 0.0f;

  [Tooltip("Draws staff line gizmos and marker validation when the object is selected.")]
  [SerializeField] private bool drawDebugGizmos = false;

  [Header("Staff Extras")]
  [Tooltip("Prefab used for ledger lines when notes fall above/below the staff.")]
  [SerializeField] private GameObject ledgerLinePrefab;

  [Tooltip("Prefab used for measure divider lines.")]
  [SerializeField] private GameObject measureLinePrefab;

  [Tooltip("Local Y position for measure lines (depends on your prefab pivot).")]
  [SerializeField] private float measureLineLocalY = 0.0f;

  [Tooltip("Optional Z offset for measure lines relative to noteLocalZ.")]
  [SerializeField] private float measureLineLocalZOffset = 0.0f;

  [Tooltip("Multiplies the ledger line prefab scale after it is spawned.")]
  [SerializeField] private float ledgerLineScaleMultiplier = 2.0f;

  [Tooltip("Local Y offset applied to ledger lines (useful if the prefab pivot is not centered).")]
  [SerializeField] private float ledgerLineLocalYOffset = -0.12f;

  [Header("Accidentals")]
  [SerializeField] private GameObject sharpPrefab;
  [SerializeField] private GameObject flatPrefab;
  [SerializeField] private float accidentalXOffset = -0.326f;

  [Header("Grand Staff Metrics (Local Space)")]
  [Tooltip("Fixed local X where the note should be played.")]
  [SerializeField] private float noteLocalX = -2.5f;

  [Header("Runup Rendering")]
  [Tooltip("Scales the runup spacing. A relative distance of 1.0 becomes this many local X units.")]
  [SerializeField] private float runupLocalUnitsPerRelative = 2.0f;

  [Tooltip("Minimum relative distance between consecutive notes in the runup.")]
  [SerializeField] private float runupMinRelativeDistance = 0.5f;

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

  private readonly List<GameObject> _activeNotes = new List<GameObject>();
  private readonly List<GameObject> _activeAccidentals = new List<GameObject>();
  private readonly List<GameObject> _activeLedgerLines = new List<GameObject>();
  private readonly List<GameObject> _activeMeasureLines = new List<GameObject>();

  private static readonly Pitch TrebleBottomLinePitch = new Pitch(Pitch.NoteName.E, 4);
  private static readonly Pitch BassBottomLinePitch = new Pitch(Pitch.NoteName.G, 2);

  private static readonly Pitch TrebleTopLinePitch = new Pitch(Pitch.NoteName.F, 5);
  private static readonly Pitch BassTopLinePitch = new Pitch(Pitch.NoteName.A, 3);

  private static readonly Pitch TrebleG4Pitch = new Pitch(Pitch.NoteName.G, 4);
  private static readonly Pitch TrebleC5Pitch = new Pitch(Pitch.NoteName.C, 5);
  private static readonly Pitch BassF3Pitch = new Pitch(Pitch.NoteName.F, 3);
  private static readonly Pitch BassC3Pitch = new Pitch(Pitch.NoteName.C, 3);

  // Common engraving convention: rests sit near the middle line.
  private static readonly Pitch TrebleRestAnchorPitch = new Pitch(Pitch.NoteName.B, 4);
  private static readonly Pitch BassRestAnchorPitch = new Pitch(Pitch.NoteName.D, 3);

  private void Awake()
  {
    TryAutoCalibrateFromMarkers();
    EnsureScrollRoot();
  }

#if UNITY_EDITOR
  private void OnValidate()
  {
    // Keep inspector edits responsive in edit mode.
    if (!Application.isPlaying)
      TryAutoCalibrateFromMarkers();
  }
#endif

  private void TryAutoCalibrateFromMarkers()
  {
    if (!autoCalibrateFromMarkers)
      return;

    bool haveTreble = trebleTopLineMarker != null && trebleBottomLineMarker != null;
    bool haveBass = bassTopLineMarker != null && bassBottomLineMarker != null;

    if (!haveTreble && !haveBass)
      return;

    if (haveTreble)
    {
      trebleTopLineY = GetMarkerLocalY(trebleTopLineMarker);
      trebleBottomLineY = GetMarkerLocalY(trebleBottomLineMarker);
    }

    if (haveBass)
    {
      bassTopLineY = GetMarkerLocalY(bassTopLineMarker);
      bassBottomLineY = GetMarkerLocalY(bassBottomLineMarker);
    }

    float? trebleSpacing = null;
    float? bassSpacing = null;

    if (haveTreble)
      trebleSpacing = Mathf.Abs(trebleTopLineY - trebleBottomLineY) / 4f;
    if (haveBass)
      bassSpacing = Mathf.Abs(bassTopLineY - bassBottomLineY) / 4f;

    float newSpacing = staffLineSpacing;
    if (trebleSpacing.HasValue && bassSpacing.HasValue)
    {
      newSpacing = (trebleSpacing.Value + bassSpacing.Value) * 0.5f;
      if (Mathf.Abs(trebleSpacing.Value - bassSpacing.Value) > 0.001f)
        Debug.LogWarning($"[STAFF] Treble/bass spacing mismatch (treble={trebleSpacing.Value:F4}, bass={bassSpacing.Value:F4}). Using average={newSpacing:F4}.", this);
    }
    else if (trebleSpacing.HasValue)
    {
      newSpacing = trebleSpacing.Value;
    }
    else if (bassSpacing.HasValue)
    {
      newSpacing = bassSpacing.Value;
    }

    if (newSpacing > 0.0001f)
      staffLineSpacing = newSpacing;

    // Helpful sanity check: if top is below bottom, the art is inverted.
    if (haveTreble && trebleTopLineY < trebleBottomLineY && !invertYOffset)
      Debug.LogWarning("[STAFF] Treble top marker is below bottom marker. If your staff art is flipped, enable 'invertYOffset'.", this);
    if (haveBass && bassTopLineY < bassBottomLineY && !invertYOffset)
      Debug.LogWarning("[STAFF] Bass top marker is below bottom marker. If your staff art is flipped, enable 'invertYOffset'.", this);
  }

  private float GetMarkerLocalY(Transform marker)
  {
    // Marker may be anywhere in the scene; convert to this staff object's local space.
    Vector3 local = transform.InverseTransformPoint(marker.position);
    return local.y;
  }

  public void Clear()
  {
    EnsureScrollRoot();

    // Destroy everything we spawned under the scroll root.
    if (scrollRoot != null)
    {
      for (int i = scrollRoot.childCount - 1; i >= 0; i--)
      {
        var child = scrollRoot.GetChild(i);
        if (child != null)
          Destroy(child.gameObject);
      }
    }

    _activeNote = null;
    _activeAccidental = null;
    _activeNotes.Clear();
    _activeAccidentals.Clear();
    _activeLedgerLines.Clear();
    _activeMeasureLines.Clear();
  }

  public float GetLocalUnitsPerBeat()
  {
    // Keep scrolling consistent with the original spacing: relative = beats * 0.5, then multiplied by runupLocalUnitsPerRelative.
    return runupLocalUnitsPerRelative * 0.5f;
  }

  public void SetScrollBeat(double beat)
  {
    EnsureScrollRoot();
    if (scrollRoot == null)
      return;

    float x = -((float)beat * GetLocalUnitsPerBeat());
    scrollRoot.localPosition = new Vector3(x, 0f, 0f);
  }

  /// <summary>
  /// Places the entire score once under a scroll root. Use SetScrollBeat() every frame to scroll smoothly.
  /// </summary>
  public void BuildFullScore(IReadOnlyList<Note> notes)
  {
    if (notes == null || notes.Count == 0)
    {
      Clear();
      return;
    }

    Clear();
    EnsureScrollRoot();

    float unitsPerBeat = GetLocalUnitsPerBeat();
    double? lastMeasureStartBeatGlobal = null;

    int i = 0;
    while (i < notes.Count)
    {
      var groupStartNote = notes[i];
      if (groupStartNote == null)
      {
        i++;
        continue;
      }

      double groupBeat = groupStartNote.StartBeatGlobal;
      float localX = noteLocalX + ((float)groupBeat * unitsPerBeat);

      // Measure line at the start of each new measure.
      if (measureLinePrefab != null)
      {
        bool isStartOfMeasure = Mathf.Abs((float)groupStartNote.StartBeat) < 0.0001f;
        if (isStartOfMeasure)
        {
          if (lastMeasureStartBeatGlobal == null || !Approximately(groupBeat, lastMeasureStartBeatGlobal.Value))
          {
            SpawnMeasureLine(localX);
            lastMeasureStartBeatGlobal = groupBeat;
          }
        }
      }

      // Render all notes that start at this same beat (chords).
      int j = i;
      while (j < notes.Count)
      {
        var n = notes[j];
        if (n == null)
        {
          j++;
          continue;
        }

        if (!Approximately(n.StartBeatGlobal, groupBeat))
          break;

        if (n.IsRest)
        {
          var restPrefab = GetRestPrefab(n.Duration.Type);
          if (restPrefab != null)
          {
            bool useTreble = ShouldUseTreble(n);
            float localY = GetRestLocalY(useTreble);
            var localPos = new Vector3(localX, localY, noteLocalZ);
            var restGo = SpawnUnderStaffKeepingPrefabScale(restPrefab, localPos);
            _activeNotes.Add(restGo);
          }
          else
          {
            Debug.LogWarning($"[STAFF] Missing rest prefab for duration {n.Duration.Type}.");
          }
        }
        else
        {
          var notePrefab = GetNotePrefab(n.Duration.Type);
          if (notePrefab != null)
          {
            bool useTreble = ShouldUseTreble(n);
            float localY = PitchToLocalY(n.Pitch, useTreble) + pitchGridLocalYOffset + noteHeadLocalYOffset;
            var localPos = new Vector3(localX, localY, noteLocalZ);

            TryCreateLedgerLines(n.Pitch, localX, useTreble);

            var noteGo = SpawnUnderStaffKeepingPrefabScale(notePrefab, localPos);
            _activeNotes.Add(noteGo);

            var accidentalGo = TryCreateAccidental(n.Pitch.Accidental, localPos);
            if (accidentalGo != null)
              _activeAccidentals.Add(accidentalGo);
          }
          else
          {
            Debug.LogWarning($"[STAFF] Missing note prefab for duration {n.Duration.Type}.");
          }
        }

        j++;
      }

      i = j;
    }

    if (_activeNotes.Count > 0)
      _activeNote = _activeNotes[0];
    if (_activeAccidentals.Count > 0)
      _activeAccidental = _activeAccidentals[0];
  }

  public void ShowNextNote(Note note)
  {
    if (note == null)
    {
      Clear();
      return;
    }

    // Back-compat: still supports the single-note view.
    ShowUpcomingNotes(new[] { note }, 0);
  }

  private bool ShouldUseTreble(Note note)
  {
    if (note != null)
    {
      if (note.StaffNumber == 1)
        return true;
      if (note.StaffNumber == 2)
        return false;
    }

    // Rests may not have a pitch. Default them to treble unless explicitly assigned to bass.
    if (note == null || note.IsRest)
      return true;

    // Fallback for older scores / non-piano parts.
    return note != null && note.Pitch.ToMidiNote() >= splitMidiNote;
  }

  /// <summary>
  /// Renders a runup (queue) of upcoming notes on the staff.
  /// notes[startIndex] is rendered at the fixed play position (noteLocalX), and subsequent notes
  /// are laid out to the right with horizontal gaps based on the previous note's duration.
  /// </summary>
  public void ShowUpcomingNotes(IReadOnlyList<Note> notes, int startIndex)
  {
    if (notes == null || notes.Count == 0 || startIndex < 0 || startIndex >= notes.Count)
    {
      Clear();
      return;
    }

    Clear();

    // Layout is based on absolute beat positions, grouped by StartBeatGlobal.
    // This keeps spacing consistent across measures, and keeps chords stacked at the same X.
    float xOffsetRelative = 0f;
    double? lastMeasureStartBeatGlobal = null;

    int i = startIndex;
    while (i < notes.Count)
    {
      var groupStartNote = notes[i];
      if (groupStartNote == null)
      {
        i++;
        continue;
      }

      double groupBeat = groupStartNote.StartBeatGlobal;
      float localX = noteLocalX + (xOffsetRelative * runupLocalUnitsPerRelative);

      // Measure line at the start of each new measure.
      if (measureLinePrefab != null)
      {
        bool isStartOfMeasure = Mathf.Abs((float)groupStartNote.StartBeat) < 0.0001f;
        if (isStartOfMeasure)
        {
          if (lastMeasureStartBeatGlobal == null || !Approximately(groupBeat, lastMeasureStartBeatGlobal.Value))
          {
            SpawnMeasureLine(localX);
            lastMeasureStartBeatGlobal = groupBeat;
          }
        }
      }

      // Render all notes that start at this same beat (chords).
      int j = i;
      while (j < notes.Count)
      {
        var n = notes[j];
        if (n == null)
        {
          j++;
          continue;
        }

        if (!Approximately(n.StartBeatGlobal, groupBeat))
          break;

        if (n.IsRest)
        {
          var restPrefab = GetRestPrefab(n.Duration.Type);
          if (restPrefab != null)
          {
            bool useTreble = ShouldUseTreble(n);
            float localY = GetRestLocalY(useTreble);
            var localPos = new Vector3(localX, localY, noteLocalZ);
            var restGo = SpawnUnderStaffKeepingPrefabScale(restPrefab, localPos);
            _activeNotes.Add(restGo);
          }
          else
          {
            Debug.LogWarning($"[STAFF] Missing rest prefab for duration {n.Duration.Type}.");
          }
        }
        else
        {
          var notePrefab = GetNotePrefab(n.Duration.Type);
          if (notePrefab != null)
          {
            bool useTreble = ShouldUseTreble(n);
            float localY = PitchToLocalY(n.Pitch, useTreble) + pitchGridLocalYOffset + noteHeadLocalYOffset;
            var localPos = new Vector3(localX, localY, noteLocalZ);

            TryCreateLedgerLines(n.Pitch, localX, useTreble);

            var noteGo = SpawnUnderStaffKeepingPrefabScale(notePrefab, localPos);
            _activeNotes.Add(noteGo);

            var accidentalGo = TryCreateAccidental(n.Pitch.Accidental, localPos);
            if (accidentalGo != null)
              _activeAccidentals.Add(accidentalGo);
          }
          else
          {
            Debug.LogWarning($"[STAFF] Missing note prefab for duration {n.Duration.Type}.");
          }
        }

        j++;
      }

      // Advance to the next beat-group.
      if (j < notes.Count)
      {
        var next = notes[j];
        if (next != null)
        {
          float deltaBeats = (float)(next.StartBeatGlobal - groupBeat);
          float relativeGap = Mathf.Max(runupMinRelativeDistance, deltaBeats * 0.5f);
          xOffsetRelative += relativeGap;
        }
      }

      i = j;
    }

    // Maintain existing single-note fields for any external scripts/inspector debugging.
    if (_activeNotes.Count > 0)
      _activeNote = _activeNotes[0];
    if (_activeAccidentals.Count > 0)
      _activeAccidental = _activeAccidentals[0];
  }

  private static bool Approximately(double a, double b)
  {
    return Mathf.Abs((float)(a - b)) < 0.0001f;
  }

  private void SpawnMeasureLine(float localX)
  {
    if (measureLinePrefab == null)
      return;

    var localPos = new Vector3(localX - 0.5f, measureLineLocalY, noteLocalZ + measureLineLocalZOffset);
    var line = SpawnUnderStaffKeepingPrefabScale(measureLinePrefab, localPos);
    _activeMeasureLines.Add(line);
  }

  private void TryCreateLedgerLines(Pitch pitch, float localX, bool useTreble)
  {
    if (ledgerLinePrefab == null)
      return;

    float stepHeight = staffLineSpacing * 0.5f;
    Pitch referencePitch = useTreble ? TrebleBottomLinePitch : BassBottomLinePitch;
    float referenceY = useTreble ? trebleBottomLineY : bassBottomLineY;

    int noteStep = DiatonicIndex(pitch) - DiatonicIndex(referencePitch);

    // Staff lines are at steps 0,2,4,6,8 (bottom to top). Ledger lines continue the pattern.
    const int bottomLineStep = 0;
    const int topLineStep = 8;

    if (noteStep > topLineStep)
    {
      int maxLineStep = (noteStep % 2 == 0) ? noteStep : noteStep - 1;
      for (int step = topLineStep + 2; step <= maxLineStep; step += 2)
      {
        float y = referenceY + (invertYOffset ? -(step * stepHeight) : (step * stepHeight));
        var ledgerLocalPos = new Vector3(localX, y + pitchGridLocalYOffset + noteHeadLocalYOffset + ledgerLineLocalYOffset, noteLocalZ);
        var line = SpawnUnderStaffKeepingPrefabScale(ledgerLinePrefab, ledgerLocalPos);
        if (line != null)
          line.transform.localScale *= ledgerLineScaleMultiplier;
        _activeLedgerLines.Add(line);
      }
    }
    else if (noteStep < bottomLineStep)
    {
      int minLineStep = (noteStep % 2 == 0) ? noteStep : noteStep + 1;
      for (int step = bottomLineStep - 2; step >= minLineStep; step -= 2)
      {
        float y = referenceY + (invertYOffset ? -(step * stepHeight) : (step * stepHeight));
        var ledgerLocalPos = new Vector3(localX, y + pitchGridLocalYOffset + noteHeadLocalYOffset + ledgerLineLocalYOffset, noteLocalZ);
        var line = SpawnUnderStaffKeepingPrefabScale(ledgerLinePrefab, ledgerLocalPos);
        if (line != null)
          line.transform.localScale *= ledgerLineScaleMultiplier;
        _activeLedgerLines.Add(line);
      }
    }
  }

  private void OnDrawGizmosSelected()
  {
    if (!drawDebugGizmos)
      return;

    // Draw treble + bass staff lines and a few validation points.
    Gizmos.matrix = transform.localToWorldMatrix;

    float halfWidth = 4.0f;
    float stepHeight = staffLineSpacing * 0.5f;

    DrawStaffDebug(trebleBottomLineY, TrebleBottomLinePitch, halfWidth, stepHeight, Color.green);
    DrawStaffDebug(bassBottomLineY, BassBottomLinePitch, halfWidth, stepHeight, Color.cyan);

    // Marker validation points (if provided).
    DrawMarkerValidation(trebleG4Marker, TrebleG4Pitch, true, Color.yellow);
    DrawMarkerValidation(trebleC5Marker, TrebleC5Pitch, true, Color.yellow);
    DrawMarkerValidation(bassF3Marker, BassF3Pitch, false, Color.yellow);
    DrawMarkerValidation(bassC3Marker, BassC3Pitch, false, Color.yellow);
  }

  private void DrawStaffDebug(float bottomLineY, Pitch bottomLinePitch, float halfWidth, float stepHeight, Color lineColor)
  {
    // Staff lines at steps 0,2,4,6,8.
    Gizmos.color = lineColor;
    for (int step = 0; step <= 8; step += 2)
    {
      float y = bottomLineY + pitchGridLocalYOffset + noteHeadLocalYOffset + (invertYOffset ? -(step * stepHeight) : (step * stepHeight));
      Gizmos.DrawLine(new Vector3(-halfWidth, y, noteLocalZ), new Vector3(halfWidth, y, noteLocalZ));
    }

    // A couple ledger lines above and below.
    Gizmos.color = new Color(lineColor.r, lineColor.g, lineColor.b, 0.6f);
    for (int step = -4; step <= 12; step += 2)
    {
      if (step >= 0 && step <= 8)
        continue;
      float y = bottomLineY + pitchGridLocalYOffset + noteHeadLocalYOffset + (invertYOffset ? -(step * stepHeight) : (step * stepHeight));
      Gizmos.DrawLine(new Vector3(-halfWidth * 0.4f, y, noteLocalZ), new Vector3(halfWidth * 0.4f, y, noteLocalZ));
    }
  }

  private void DrawMarkerValidation(Transform marker, Pitch pitch, bool useTreble, Color color)
  {
    if (marker == null)
      return;

    // Marker point in local space.
    Vector3 markerLocal = transform.InverseTransformPoint(marker.position);
    float expectedY = PitchToLocalY(pitch, useTreble) + pitchGridLocalYOffset + noteHeadLocalYOffset;

    Gizmos.color = color;
    Gizmos.DrawSphere(new Vector3(markerLocal.x, markerLocal.y, noteLocalZ), 0.04f);

    Gizmos.color = Color.magenta;
    Gizmos.DrawSphere(new Vector3(markerLocal.x, expectedY, noteLocalZ), 0.035f);

    Gizmos.color = new Color(1f, 0f, 1f, 0.5f);
    Gizmos.DrawLine(new Vector3(markerLocal.x, markerLocal.y, noteLocalZ), new Vector3(markerLocal.x, expectedY, noteLocalZ));
  }

  private float GetRelativeDistanceFromDuration(Duration duration)
  {
    // Spec:
    // - min distance between notes = 0.5 relative
    // - whole notes = 2 relative
    // Since whole = 4 beats, a linear mapping is: relative = beats * 0.5, clamped to min.
    float beats = (float)duration.GetBeats();
    float relative = beats * 0.5f;
    return Mathf.Max(runupMinRelativeDistance, relative);
  }

  private GameObject TryCreateAccidental(int accidental, Vector3 noteLocalPosition)
  {
    if (accidental == 0)
      return null;

    // Only render a single sharp/flat symbol for now.
    if (accidental > 0)
    {
      if (sharpPrefab == null) return null;
      return SpawnUnderStaffKeepingPrefabScale(
        sharpPrefab,
        noteLocalPosition + new Vector3(accidentalXOffset, 0f, 0f));
    }
    else
    {
      if (flatPrefab == null) return null;
      return SpawnUnderStaffKeepingPrefabScale(
        flatPrefab,
        noteLocalPosition + new Vector3(accidentalXOffset, 0f, 0f));
    }
  }

  private GameObject SpawnUnderStaffKeepingPrefabScale(GameObject prefab, Vector3 localPosition)
  {
    EnsureScrollRoot();

    // Instantiate in world space first, then parent while preserving world transform.
    // This prevents the staff's scale from inflating the spawned prefab.
    Transform parent = scrollRoot != null ? scrollRoot : transform;

    Vector3 worldPos = transform.TransformPoint(localPosition);
    Quaternion worldRot = transform.rotation;

    var instance = Instantiate(prefab, worldPos, worldRot);
    instance.transform.SetParent(parent, true);
    return instance;
  }

  private void EnsureScrollRoot()
  {
    if (scrollRoot != null)
      return;

    // Create a child root so the score can be moved without affecting staff markers/art.
    var existing = transform.Find("ScoreRoot");
    if (existing != null)
    {
      scrollRoot = existing;
      return;
    }

    var rootGo = new GameObject("ScoreRoot");
    rootGo.transform.SetParent(transform, false);
    rootGo.transform.localPosition = Vector3.zero;
    rootGo.transform.localRotation = Quaternion.identity;
    rootGo.transform.localScale = Vector3.one;
    scrollRoot = rootGo.transform;
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

  private GameObject GetRestPrefab(Duration.NoteType type)
  {
    return type switch
    {
      Duration.NoteType.Whole => wholeRestPrefab,
      Duration.NoteType.Half => halfRestPrefab,
      Duration.NoteType.Quarter => quarterRestPrefab,
      Duration.NoteType.Eighth => eighthRestPrefab,
      _ => quarterRestPrefab,
    };
  }

  private float GetRestLocalY(bool useTreble)
  {
    // Rest placement is intentionally independent of notehead pivot.
    Pitch anchor = useTreble ? TrebleRestAnchorPitch : BassRestAnchorPitch;
    return PitchToLocalY(anchor, useTreble) + pitchGridLocalYOffset + restLocalYOffset;
  }

  private float PitchToLocalY(Pitch pitch)
  {
    bool useTreble = pitch.ToMidiNote() >= splitMidiNote;
    return PitchToLocalY(pitch, useTreble);
  }

  private float PitchToLocalY(Pitch pitch, bool useTreble)
  {
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
