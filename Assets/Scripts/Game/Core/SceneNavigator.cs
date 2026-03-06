using UnityEngine;
using UnityEngine.SceneManagement;

namespace PianoGame.Game
{
  /// <summary>
  /// Centralized scene management for the Piano Game.
  /// Provides constants for scene names and navigation methods.
  /// </summary>
  public static class SceneNavigator
  {
    // Scene name constants - ensure these match your Unity scene names
    public const string SCENE_MAIN_MENU = "MainMenu";
    public const string SCENE_SETTINGS = "Settings";
    public const string SCENE_LEVEL_SELECTION = "LevelSelection";
    public const string SCENE_GAME = "Game";
    public const string SCENE_END_SONG = "EndSong";

    /// <summary>
    /// Loads the Main Menu scene.
    /// </summary>
    public static void GoToMainMenu()
    {
      SceneManager.LoadScene(SCENE_MAIN_MENU);
    }

    /// <summary>
    /// Loads the Settings scene.
    /// </summary>
    public static void GoToSettings()
    {
      SceneManager.LoadScene(SCENE_SETTINGS);
    }

    /// <summary>
    /// Loads the Level Selection scene.
    /// </summary>
    public static void GoToLevelSelection()
    {
      SceneManager.LoadScene(SCENE_LEVEL_SELECTION);
    }

    /// <summary>
    /// Loads the Game scene with the currently selected song.
    /// </summary>
    public static void StartGame()
    {
      if (string.IsNullOrEmpty(GameData.SelectedSongPath))
      {
        Debug.LogError("[SceneNavigator] Cannot start game - no song selected!");
        return;
      }

      GameData.ResetResults();
      SceneManager.LoadScene(SCENE_GAME);
    }

    /// <summary>
    /// Loads the End Song Results scene.
    /// </summary>
    public static void GoToEndSong()
    {
      SceneManager.LoadScene(SCENE_END_SONG);
    }

    /// <summary>
    /// Quits the application.
    /// </summary>
    public static void QuitGame()
    {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
      Application.Quit();
#endif
    }

    /// <summary>
    /// Reloads the current scene.
    /// </summary>
    public static void ReloadCurrentScene()
    {
      SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
  }
}
