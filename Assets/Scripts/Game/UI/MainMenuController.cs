using UnityEngine;
using UnityEngine.UI;
using PianoGame.Game;

namespace PianoGame.UI
{
  /// <summary>
  /// Controller for the Main Menu scene.
  /// Handles navigation to other scenes via button clicks.
  /// 
  /// Setup in Unity:
  /// 1. Create a Canvas with 3 buttons: Start, Settings, Quit
  /// 2. Attach this script to an empty GameObject
  /// 3. Drag the buttons to the appropriate fields in the inspector
  /// </summary>
  public class MainMenuController : MonoBehaviour
  {
    [Header("UI References")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;

    [Header("Optional Animations")]
    [SerializeField] private float buttonFadeInDelay = 0.2f;
    [SerializeField] private CanvasGroup canvasGroup;

    private void Start()
    {
      // Initialize settings on first load
      GameSettings.Initialize();

      // Setup button listeners
      if (startButton != null)
      {
        startButton.onClick.AddListener(OnStartClicked);
      }
      else
      {
        Debug.LogWarning("[MainMenu] Start button not assigned!");
      }

      if (settingsButton != null)
      {
        settingsButton.onClick.AddListener(OnSettingsClicked);
      }
      else
      {
        Debug.LogWarning("[MainMenu] Settings button not assigned!");
      }

      if (quitButton != null)
      {
        quitButton.onClick.AddListener(OnQuitClicked);
      }
      else
      {
        Debug.LogWarning("[MainMenu] Quit button not assigned!");
      }

      // Optional: Fade in animation
      if (canvasGroup != null)
      {
        canvasGroup.alpha = 0f;
        StartCoroutine(FadeIn());
      }
    }

    private System.Collections.IEnumerator FadeIn()
    {
      yield return new WaitForSeconds(buttonFadeInDelay);

      float elapsed = 0f;
      float duration = 0.5f;

      while (elapsed < duration)
      {
        elapsed += Time.deltaTime;
        canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / duration);
        yield return null;
      }

      canvasGroup.alpha = 1f;
    }

    private void OnStartClicked()
    {
      Debug.Log("[MainMenu] Start button clicked - navigating to Level Selection");
      SceneNavigator.GoToLevelSelection();
    }

    private void OnSettingsClicked()
    {
      Debug.Log("[MainMenu] Settings button clicked - navigating to Settings");
      SceneNavigator.GoToSettings();
    }

    private void OnQuitClicked()
    {
      Debug.Log("[MainMenu] Quit button clicked - exiting game");
      SceneNavigator.QuitGame();
    }

    private void OnDestroy()
    {
      // Cleanup listeners
      if (startButton != null) startButton.onClick.RemoveListener(OnStartClicked);
      if (settingsButton != null) settingsButton.onClick.RemoveListener(OnSettingsClicked);
      if (quitButton != null) quitButton.onClick.RemoveListener(OnQuitClicked);
    }
  }
}
