using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PianoGame.Game;

namespace PianoGame.UI
{
  /// <summary>
  /// Controller for the End Song Results scene.
  /// Displays performance statistics after completing a song.
  /// 
  /// Setup in Unity:
  /// 1. Create a Canvas with:
  ///    - Song name display (TMP_Text)
  ///    - Letter grade display (TMP_Text) - large, centered
  ///    - Max combo display (TMP_Text)
  ///    - Perfect hits count (TMP_Text)
  ///    - Close hits count (TMP_Text)
  ///    - Miss count (TMP_Text)
  ///    - Accuracy percentage (TMP_Text)
  ///    - Full combo indicator (GameObject - enable/disable)
  ///    - Continue button (returns to Level Selection)
  ///    - Optional: Retry button (restarts the song)
  /// 2. Attach this script to an empty GameObject
  /// 3. Assign the UI elements in the inspector
  /// </summary>
  public class EndSongController : MonoBehaviour
  {
    [Header("Song Info")]
    [SerializeField] private TMP_Text songNameText;

    [Header("Grade Display")]
    [SerializeField] private TMP_Text letterGradeText;
    [SerializeField] private Image gradeBackgroundImage;

    [Header("Statistics")]
    [SerializeField] private TMP_Text maxComboText;
    [SerializeField] private TMP_Text perfectHitsText;
    [SerializeField] private TMP_Text closeHitsText;
    [SerializeField] private TMP_Text missCountText;
    [SerializeField] private TMP_Text accuracyText;

    [Header("Full Combo Indicator")]
    [SerializeField] private GameObject fullComboIndicator;
    [SerializeField] private TMP_Text fullComboText;

    [Header("Navigation")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button mainMenuButton;

    [Header("Grade Colors")]
    [SerializeField] private Color gradeColorSPlus = new Color(1f, 0.84f, 0f); // Gold
    [SerializeField] private Color gradeColorS = new Color(1f, 0.84f, 0f);     // Gold
    [SerializeField] private Color gradeColorA = new Color(0.2f, 0.8f, 0.2f);  // Green
    [SerializeField] private Color gradeColorB = new Color(0.3f, 0.6f, 1f);    // Blue
    [SerializeField] private Color gradeColorC = new Color(0.8f, 0.6f, 0.2f);  // Orange
    [SerializeField] private Color gradeColorD = new Color(0.8f, 0.4f, 0.2f);  // Dark Orange
    [SerializeField] private Color gradeColorF = new Color(0.8f, 0.2f, 0.2f);  // Red

    [Header("Animation Settings")]
    [SerializeField] private float statRevealDelay = 0.3f;
    [SerializeField] private float gradeRevealDelay = 1.5f;

    private void Start()
    {
      SetupNavigationButtons();
      DisplayResults();
    }

    private void SetupNavigationButtons()
    {
      if (continueButton != null)
      {
        continueButton.onClick.AddListener(OnContinueClicked);
      }

      if (retryButton != null)
      {
        retryButton.onClick.AddListener(OnRetryClicked);
      }

      if (mainMenuButton != null)
      {
        mainMenuButton.onClick.AddListener(OnMainMenuClicked);
      }
    }

    /// <summary>
    /// Displays all the results from GameData.
    /// </summary>
    private void DisplayResults()
    {
      // Song name
      if (songNameText != null)
      {
        songNameText.text = !string.IsNullOrEmpty(GameData.SelectedSongName)
            ? GameData.SelectedSongName
            : "Unknown Song";
      }

      // Start the animated reveal
      StartCoroutine(RevealResultsAnimated());
    }

    private System.Collections.IEnumerator RevealResultsAnimated()
    {
      // Hide elements initially for dramatic reveal
      SetAllStatsVisible(false);

      yield return new WaitForSeconds(0.2f);

      // Reveal stats one by one
      yield return RevealStat(maxComboText, $"Max Combo: {GameData.MaxCombo}");
      yield return new WaitForSeconds(statRevealDelay);

      yield return RevealStat(perfectHitsText, $"Perfect: {GameData.PerfectHits}");
      yield return new WaitForSeconds(statRevealDelay);

      yield return RevealStat(closeHitsText, $"Almost: {GameData.CloseHits}");
      yield return new WaitForSeconds(statRevealDelay);

      yield return RevealStat(missCountText, $"Miss: {GameData.MissedNotes}");
      yield return new WaitForSeconds(statRevealDelay);

      // Accuracy
      float accuracy = GameData.GetAccuracyPercent();
      yield return RevealStat(accuracyText, $"Accuracy: {accuracy:F1}%");
      yield return new WaitForSeconds(statRevealDelay);

      // Full combo indicator
      if (fullComboIndicator != null)
      {
        fullComboIndicator.SetActive(GameData.IsFullCombo);

        if (GameData.IsFullCombo && fullComboText != null)
        {
          fullComboText.text = "FULL COMBO!";
        }
      }

      yield return new WaitForSeconds(gradeRevealDelay - (statRevealDelay * 6));

      // Finally reveal the grade with fanfare
      yield return RevealGrade();
    }

    private void SetAllStatsVisible(bool visible)
    {
      SetTextVisible(maxComboText, visible);
      SetTextVisible(perfectHitsText, visible);
      SetTextVisible(closeHitsText, visible);
      SetTextVisible(missCountText, visible);
      SetTextVisible(accuracyText, visible);

      if (fullComboIndicator != null)
        fullComboIndicator.SetActive(false);

      if (letterGradeText != null)
      {
        letterGradeText.gameObject.SetActive(visible);
      }
    }

    private void SetTextVisible(TMP_Text text, bool visible)
    {
      if (text != null)
      {
        text.gameObject.SetActive(visible);
      }
    }

    private System.Collections.IEnumerator RevealStat(TMP_Text textComponent, string value)
    {
      if (textComponent == null) yield break;

      textComponent.text = value;
      textComponent.gameObject.SetActive(true);

      // Simple scale punch effect
      var transform = textComponent.transform;
      Vector3 originalScale = transform.localScale;
      transform.localScale = originalScale * 1.2f;

      float elapsed = 0f;
      float duration = 0.15f;

      while (elapsed < duration)
      {
        elapsed += Time.deltaTime;
        float t = elapsed / duration;
        transform.localScale = Vector3.Lerp(originalScale * 1.2f, originalScale, t);
        yield return null;
      }

      transform.localScale = originalScale;
    }

    private System.Collections.IEnumerator RevealGrade()
    {
      if (letterGradeText == null) yield break;

      string grade = GameData.GetLetterGrade();
      Color gradeColor = GetGradeColor(grade);

      letterGradeText.text = grade;
      letterGradeText.color = gradeColor;
      if (grade == "S+") {
        letterGradeText.fontSize = 200.0f;
      } else {
        letterGradeText.fontSize = 363.9f;
      }
      letterGradeText.gameObject.SetActive(true);

      // Update background if present
      if (gradeBackgroundImage != null)
      {
        gradeBackgroundImage.color = new Color(gradeColor.r, gradeColor.g, gradeColor.b, 0.3f);
      }

      // Dramatic scale animation
      var transform = letterGradeText.transform;
      Vector3 originalScale = transform.localScale;
      transform.localScale = Vector3.zero;

      float elapsed = 0f;
      float duration = 0.5f;

      while (elapsed < duration)
      {
        elapsed += Time.deltaTime;
        float t = elapsed / duration;
        // Overshoot easing
        float overshoot = 1.7f;
        float s = overshoot;
        float eased = (t = t - 1) * t * ((s + 1) * t + s) + 1;
        transform.localScale = Vector3.Lerp(Vector3.zero, originalScale, eased);
        yield return null;
      }

      transform.localScale = originalScale;
    }

    private Color GetGradeColor(string grade)
    {
      return grade switch
      {
        "S+" => gradeColorSPlus,
        "S" => gradeColorS,
        "A" => gradeColorA,
        "B" => gradeColorB,
        "C" => gradeColorC,
        "D" => gradeColorD,
        _ => gradeColorF
      };
    }

    private void OnContinueClicked()
    {
      Debug.Log("[EndSong] Continue clicked - returning to Level Selection");
      SceneNavigator.GoToLevelSelection();
    }

    private void OnRetryClicked()
    {
      Debug.Log("[EndSong] Retry clicked - restarting song");

      // Keep the same song selection, just reset results
      GameData.ResetResults();
      SceneNavigator.StartGame();
    }

    private void OnMainMenuClicked()
    {
      Debug.Log("[EndSong] Main Menu clicked - returning to Main Menu");
      SceneNavigator.GoToMainMenu();
    }

    private void OnDestroy()
    {
      // Cleanup listeners
      if (continueButton != null) continueButton.onClick.RemoveListener(OnContinueClicked);
      if (retryButton != null) retryButton.onClick.RemoveListener(OnRetryClicked);
      if (mainMenuButton != null) mainMenuButton.onClick.RemoveListener(OnMainMenuClicked);
    }
  }
}
