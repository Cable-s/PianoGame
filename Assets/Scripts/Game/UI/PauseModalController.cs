using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using PianoGame.Game;

namespace PianoGame.UI
{
  /// <summary>
  /// Controls the pause modal UI in the game scene.
  /// Handles resume with countdown, return to song selection, and quit.
  /// </summary>
  public class PauseModalController : MonoBehaviour
  {
    [Header("UI References")]
    [SerializeField] private GameObject modalPanel;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button songSelectionButton;
    [SerializeField] private Button quitButton;

    [Header("Resume Countdown")]
    [SerializeField] private GameObject countdownPanel;
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private float resumeCountdownSeconds = 3f;

    private GameController _gameController;
    private bool _isResuming = false;
    private float _resumeCountdownEndTime = 0f;

    private void Awake()
    {
      // Find the GameController in the scene
      _gameController = FindFirstObjectByType<GameController>();

      if (_gameController == null)
      {
        Debug.LogError("[PAUSE] Could not find GameController in scene!");
      }

      // Wire up button listeners
      if (resumeButton != null)
        resumeButton.onClick.AddListener(OnResumeClicked);

      if (songSelectionButton != null)
        songSelectionButton.onClick.AddListener(OnSongSelectionClicked);

      if (quitButton != null)
        quitButton.onClick.AddListener(OnQuitClicked);

      // Start hidden
      if (modalPanel != null)
        modalPanel.SetActive(false);

      if (countdownPanel != null)
        countdownPanel.SetActive(false);
    }

    private void Update()
    {
      // Check for ESC key to toggle pause (using new Input System)
      if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
      {
        if (_isResuming)
        {
          // Cancel resume countdown and go back to pause menu
          CancelResumeCountdown();
        }
        else if (modalPanel != null && modalPanel.activeSelf)
        {
          // Already paused, start resume
          OnResumeClicked();
        }
        else
        {
          // Not paused, pause the game
          PauseGame();
        }
      }

      // Handle resume countdown
      if (_isResuming)
      {
        UpdateResumeCountdown();
      }
    }

    /// <summary>
    /// Pauses the game and shows the pause modal.
    /// </summary>
    public void PauseGame()
    {
      if (_gameController == null || !_gameController.CanPause())
        return;

      _gameController.SetPaused(true);

      if (modalPanel != null)
        modalPanel.SetActive(true);

      if (countdownPanel != null)
        countdownPanel.SetActive(false);

      Debug.Log("[PAUSE] Game paused");
    }

    /// <summary>
    /// Starts the resume countdown.
    /// </summary>
    private void OnResumeClicked()
    {
      // Hide main modal, show countdown
      if (modalPanel != null)
        modalPanel.SetActive(false);

      if (countdownPanel != null)
        countdownPanel.SetActive(true);

      _isResuming = true;
      _resumeCountdownEndTime = Time.realtimeSinceStartup + resumeCountdownSeconds;

      Debug.Log("[PAUSE] Starting resume countdown...");
    }

    /// <summary>
    /// Cancels the resume countdown and returns to pause menu.
    /// </summary>
    private void CancelResumeCountdown()
    {
      _isResuming = false;

      if (countdownPanel != null)
        countdownPanel.SetActive(false);

      if (modalPanel != null)
        modalPanel.SetActive(true);

      Debug.Log("[PAUSE] Resume countdown cancelled");
    }

    /// <summary>
    /// Updates the resume countdown display.
    /// </summary>
    private void UpdateResumeCountdown()
    {
      float remaining = _resumeCountdownEndTime - Time.realtimeSinceStartup;

      if (remaining <= 0)
      {
        // Countdown complete, resume game
        ResumeGame();
        return;
      }

      int seconds = Mathf.CeilToInt(remaining);

      if (countdownText != null)
        countdownText.text = seconds.ToString();
    }

    /// <summary>
    /// Resumes the game after countdown completes.
    /// </summary>
    private void ResumeGame()
    {
      _isResuming = false;

      if (countdownPanel != null)
        countdownPanel.SetActive(false);

      if (_gameController != null)
        _gameController.SetPaused(false);

      Debug.Log("[PAUSE] Game resumed");
    }

    /// <summary>
    /// Returns to the song selection screen.
    /// </summary>
    private void OnSongSelectionClicked()
    {
      // Ensure time scale is normal before scene transition
      Time.timeScale = 1f;

      Debug.Log("[PAUSE] Returning to song selection...");
      SceneNavigator.GoToLevelSelection();
    }

    /// <summary>
    /// Quits the game.
    /// </summary>
    private void OnQuitClicked()
    {
      Debug.Log("[PAUSE] Quitting game...");
      SceneNavigator.QuitGame();
    }

    private void OnDestroy()
    {
      // Clean up listeners
      if (resumeButton != null)
        resumeButton.onClick.RemoveListener(OnResumeClicked);

      if (songSelectionButton != null)
        songSelectionButton.onClick.RemoveListener(OnSongSelectionClicked);

      if (quitButton != null)
        quitButton.onClick.RemoveListener(OnQuitClicked);
    }
  }
}
