using UnityEngine;

public class LoadMusicButtonEvent : MonoBehaviour
{
  private MusicFileSelector _fileSelector;

  private void Start()
  {
    _fileSelector = FindObjectOfType<MusicFileSelector>();
    if (_fileSelector == null)
    {
      Debug.LogError("[BUTTON] MusicFileSelector not found in scene!");
    }
  }

  public void LoadMusic()
  {
    Debug.Log("[BUTTON] Load Music clicked");

    if (_fileSelector == null)
      _fileSelector = FindObjectOfType<MusicFileSelector>();

    if (_fileSelector != null)
    {
      _fileSelector.LoadSelectedFile();
    }
    else
    {
      Debug.LogError("[BUTTON] MusicFileSelector not found!");
    }
  }

  public void RefreshFileList()
  {
    Debug.Log("[BUTTON] Refresh clicked");

    if (_fileSelector == null)
      _fileSelector = FindObjectOfType<MusicFileSelector>();

    if (_fileSelector != null)
    {
      _fileSelector.RefreshFileList();
    }
  }

  public void SelectNext()
  {
    Debug.Log("[BUTTON] Next clicked");

    if (_fileSelector == null)
      _fileSelector = FindObjectOfType<MusicFileSelector>();

    if (_fileSelector != null)
    {
      _fileSelector.SelectNext();
    }
  }

  public void SelectPrevious()
  {
    Debug.Log("[BUTTON] Previous clicked");

    if (_fileSelector == null)
      _fileSelector = FindObjectOfType<MusicFileSelector>();

    if (_fileSelector != null)
    {
      _fileSelector.SelectPrevious();
    }
  }

  public void ResetMusic()
  {
    Debug.Log("[BUTTON] Reset clicked");

    var gameController = FindObjectOfType<GameController>();
    if (gameController != null)
    {
      gameController.ResetGame();
    }
  }
}
