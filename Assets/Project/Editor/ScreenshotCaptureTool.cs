using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ScreenshotCaptureTool {
    private const string ScreenshotFolder = "Assets/Project/ScreenShots";

    [MenuItem("Tools/Screenshots/Capture Game View")]
    private static void CaptureGameView() {
        if (!Application.isPlaying) {
            Debug.LogWarning("Enter Play Mode before capturing a screenshot.");
            return;
        }

        if (!Directory.Exists(ScreenshotFolder)) {
            Directory.CreateDirectory(ScreenshotFolder);
        }

        
        string activeScene = $"{SceneManager.GetActiveScene()}";
        string resolution = $"{Screen.width}x{Screen.height}";
        string fileName = $"{SceneManager.GetActiveScene().name}-{resolution}.png";
        string relativePath = Path.Combine(ScreenshotFolder, fileName);

        ScreenCapture.CaptureScreenshot(relativePath, 1);

        Debug.Log($"Screenshot captured: {relativePath}");
        AssetDatabase.Refresh();
    }
}