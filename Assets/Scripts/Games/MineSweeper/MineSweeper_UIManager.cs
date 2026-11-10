using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class MineSweeper_UIManager : MonoBehaviour
{
    [Header("Main Game UI")]
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI mineCountText;
    public TextMeshProUGUI headerBestTimeText;

    public void UpdateHeaderBestTime(float time)
    {
        if (headerBestTimeText != null)
            headerBestTimeText.text = "Best: " + (time == float.MaxValue ? "--:--" : FormatTime(time));
    }
    public Vector2 joystickInput;

    [Header("Difficulty Selection")]
    public GameObject difficultyPanel;
    public Button beginnerButton;
    public Button intermediateButton;
    public Button advancedButton;
    public TextMeshProUGUI bestTimeBeginnerText;
    public TextMeshProUGUI bestTimeIntermediateText;
    public TextMeshProUGUI bestTimeAdvancedText;

    [Header("Game Over Panel")]
    public GameObject gameOverPanel;
    public TextMeshProUGUI resultText;
    public TextMeshProUGUI finalTimeText;
    public TextMeshProUGUI bestTimeText;

    [Header("Camera Control")]
    public MineSweeper_CameraController cameraController;
    public Button zoomInButton;
    public Button zoomOutButton;
    public Button resetCameraButton;
    
    private int zoomDir = 0;

    void Awake()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        
        beginnerButton.onClick.AddListener(() => MineSweeper_GameManager.Instance.StartGame(MineSweeperDifficulty.Beginner));
        intermediateButton.onClick.AddListener(() => MineSweeper_GameManager.Instance.StartGame(MineSweeperDifficulty.Intermediate));
        advancedButton.onClick.AddListener(() => MineSweeper_GameManager.Instance.StartGame(MineSweeperDifficulty.Advanced));

        if (resetCameraButton != null) resetCameraButton.onClick.AddListener(() => cameraController.ResetCamera());
    }

    void Update()
    {
        if (cameraController == null) return;
        if (zoomDir != 0) cameraController.ContinuousZoom(zoomDir);

        // Add Joystick movement handling
        if (joystickInput != Vector2.zero)
        {
            cameraController.Move(joystickInput);
        }
    }

    public void OnZoomInDown() { zoomDir = 1; }
    public void OnZoomOutDown() { zoomDir = -1; }
    public void OnZoomPointerUp() { zoomDir = 0; }

    private int displayedSecond = int.MinValue;
    public void UpdateTimer(float time)
    {
        if (timerText == null) return;
        int second = Mathf.FloorToInt(time);
        if (displayedSecond == second) return;
        displayedSecond = second;
        timerText.text = FormatTime(time);
    }

    public void UpdateMineCount(int count) { if (mineCountText != null) mineCountText.text = "Left : " + count.ToString(); }
    public void ShowDifficultySelection(bool show) { if (difficultyPanel != null) difficultyPanel.SetActive(show); }

    public void ShowGameOverPanel(bool show, bool win = false, float time = 0, float bestTime = 0)
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(show);
            if (show)
            {
                if (resultText != null) resultText.text = win ? "YOU WIN!" : "GAME OVER";
                if (finalTimeText != null) finalTimeText.text = "Time: " + FormatTime(time);
                if (bestTimeText != null) bestTimeText.text = "Best: " + (bestTime == float.MaxValue ? "--:--" : FormatTime(bestTime));
            }
        }
    }

    public void UpdateBestTimes(MineSweeperData data)
    {
        if (bestTimeBeginnerText != null) bestTimeBeginnerText.text = data.bestTimeBeginner == float.MaxValue ? "Best: --:--" : "Best: " + FormatTime(data.bestTimeBeginner);
        if (bestTimeIntermediateText != null) bestTimeIntermediateText.text = data.bestTimeIntermediate == float.MaxValue ? "Best: --:--" : "Best: " + FormatTime(data.bestTimeIntermediate);
        if (bestTimeAdvancedText != null) bestTimeAdvancedText.text = data.bestTimeAdvanced == float.MaxValue ? "Best: --:--" : "Best: " + FormatTime(data.bestTimeAdvanced);
    }

    private string FormatTime(float time)
    {
        int minutes = Mathf.FloorToInt(time / 60);
        int seconds = Mathf.FloorToInt(time % 60);
        return string.Format("{0:00}:{1:00}", minutes, seconds);
    }
}
