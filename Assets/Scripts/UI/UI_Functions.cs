using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using UnityEngine.InputSystem;

public class UI_Functions : MonoBehaviour
{
    public static UI_Functions Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = 60; // Ensure 60 FPS on mobile
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            HandleBackInput();
        }
    }

    private float lastBackKeyPressTime = -10f;
    private const float doubleBackPressThreshold = 2.0f;

    private void HandleBackInput()
    {
        if (RankingsPanel.TryClose()) return;
        if (GameMenu.TryClose()) return;
        string currentScene = SceneManager.GetActiveScene().name;

        if (currentScene == "Title")
        {
            float currentTime = Time.time;
            if (currentTime - lastBackKeyPressTime < doubleBackPressThreshold)
            {
                Debug.Log("Quitting application...");
                Application.Quit();
            }
            else
            {
                lastBackKeyPressTime = currentTime;
                ShowToast("뒤로 가기 버튼을 한 번 더 누르면 종료됩니다.");
                Debug.Log("Press back again to exit");
            }
        }
        else
        {
            Debug.Log("Returning to Title scene...");
            BacktoTitleOnClicked();
        }
    }

    private void ShowToast(string message)
    {
        if (Application.platform == RuntimePlatform.Android)
        {
            try
            {
                AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                AndroidJavaClass toastClass = new AndroidJavaClass("android.widget.Toast");
                currentActivity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                {
                    AndroidJavaObject toastObject = toastClass.CallStatic<AndroidJavaObject>("makeText", currentActivity, message, 0);
                    toastObject.Call("show");
                }));
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to show Android toast: " + e.Message);
            }
        }
        else
        {
            Debug.Log("Toast: " + message);
        }
    }

    public void BacktoTitleOnClicked()
    {
        if (GameManager_2D.Instance != null) GameManager_2D.Instance.GoToTitle();
        else if (TENSUM_GameManager.Instance != null) TENSUM_GameManager.Instance.GoToTitle();
        else if (MineSweeper_GameManager.Instance != null) MineSweeper_GameManager.Instance.GoToTitle();
        else GameNavigation.Load("Title");
    }

    public void TriggerGameRestart()
    {
        if (GameManager_2D.Instance != null) GameManager_2D.Instance.RestartGame();
        else if (TENSUM_GameManager.Instance != null) TENSUM_GameManager.Instance.RestartGame();
        else if (MineSweeper_GameManager.Instance != null) MineSweeper_GameManager.Instance.RestartGame();
    }
}
