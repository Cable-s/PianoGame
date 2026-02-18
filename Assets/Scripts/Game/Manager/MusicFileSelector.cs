using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>
/// Handles browsing and selecting MusicXML files from a folder.
/// </summary>
public class MusicFileSelector : MonoBehaviour
{
  [SerializeField] private string musicFolderPath = "Assets/Music"; // Folder to search for MusicXML files

  private List<string> _availableFiles = new List<string>();
  private int _selectedFileIndex = -1;
  private GameController _gameController;

  private void Start()
  {
    _gameController = GetComponent<GameController>();
    if (_gameController == null)
    {
      _gameController = FindObjectOfType<GameController>();
    }

    RefreshFileList();
  }

  /// <summary>
  /// Scan the music folder and refresh available files.
  /// </summary>
  public void RefreshFileList()
  {
    _availableFiles.Clear();
    _selectedFileIndex = -1;

    // Create folder if it doesn't exist
    if (!Directory.Exists(musicFolderPath))
    {
      Debug.LogWarning($"[MUSIC SELECTOR] Music folder not found: {musicFolderPath}. Creating it...");
      Directory.CreateDirectory(musicFolderPath);
    }

    // Find all .musicxml and .xml files
    var xmlFiles = Directory.GetFiles(musicFolderPath, "*.musicxml")
      .Concat(Directory.GetFiles(musicFolderPath, "*.xml"))
      .Where(f => !f.EndsWith(".meta")) // Ignore Unity metadata files
      .OrderBy(f => Path.GetFileNameWithoutExtension(f))
      .ToList();

    _availableFiles = xmlFiles;

    Debug.Log($"[MUSIC SELECTOR] Found {_availableFiles.Count} music files in {musicFolderPath}");
    foreach (var file in _availableFiles)
    {
      Debug.Log($"  - {Path.GetFileNameWithoutExtension(file)}");
    }

    UpdateDisplay();
  }

  /// <summary>
  /// Select a file by index.
  /// </summary>
  public void SelectFile(int index)
  {
    if (index < 0 || index >= _availableFiles.Count)
    {
      Debug.LogError($"[MUSIC SELECTOR] Invalid file index: {index}");
      return;
    }

    _selectedFileIndex = index;
    string selectedFile = _availableFiles[index];
    Debug.Log($"[MUSIC SELECTOR] Selected: {Path.GetFileNameWithoutExtension(selectedFile)}");
    UpdateDisplay();
  }

  /// <summary>
  /// Load the currently selected file.
  /// </summary>
  public void LoadSelectedFile()
  {
    if (_selectedFileIndex < 0 || _selectedFileIndex >= _availableFiles.Count)
    {
      Debug.LogError("[MUSIC SELECTOR] No file selected!");
      return;
    }

    string filePath = _availableFiles[_selectedFileIndex];
    if (!File.Exists(filePath))
    {
      Debug.LogError($"[MUSIC SELECTOR] File not found: {filePath}");
      return;
    }

    Debug.Log($"[MUSIC SELECTOR] Loading: {filePath}");

    if (_gameController != null)
    {
      _gameController.LoadMusic(filePath);
    }
    else
    {
      Debug.LogError("[MUSIC SELECTOR] GameController not found!");
    }
  }

  /// <summary>
  /// Get list of available file names.
  /// </summary>
  public List<string> GetAvailableFileNames()
  {
    return _availableFiles.Select(f => Path.GetFileNameWithoutExtension(f)).ToList();
  }

  /// <summary>
  /// Update the display with current file list and selection.
  /// </summary>
  private void UpdateDisplay()
  {
    if (_availableFiles.Count == 0)
    {
      string msg = $"No music files found in:\n{Path.GetFullPath(musicFolderPath)}\n\nPlace .musicxml files there to load them.";
      Debug.Log($"[MUSIC SELECTOR] {msg}");
      return;
    }

    string display = "Available Music Files:\n\n";
    for (int i = 0; i < _availableFiles.Count; i++)
    {
      string fileName = Path.GetFileNameWithoutExtension(_availableFiles[i]);
      string marker = i == _selectedFileIndex ? "> " : "  ";
      display += $"{marker}[{i}] {fileName}\n";
    }

    Debug.Log($"[MUSIC SELECTOR] Available files:\n{display}");
  }

  /// <summary>
  /// Navigate to next file (for controller/keyboard input).
  /// </summary>
  public void SelectNext()
  {
    if (_availableFiles.Count == 0) return;
    SelectFile((_selectedFileIndex + 1) % _availableFiles.Count);
  }

  /// <summary>
  /// Navigate to previous file.
  /// </summary>
  public void SelectPrevious()
  {
    if (_availableFiles.Count == 0) return;
    int prevIndex = _selectedFileIndex - 1;
    if (prevIndex < 0) prevIndex = _availableFiles.Count - 1;
    SelectFile(prevIndex);
  }
}
