using UnityEngine;

public sealed class GameMenu : MonoBehaviour
{
    public GameObject popup;
    private static GameMenu opened;
    public static bool IsOpen => opened != null && opened.popup != null && opened.popup.activeInHierarchy;

    public void Open()
    {
        if (SettingsManager.Instance == null) new GameObject("Settings Manager").AddComponent<SettingsManager>();
        popup.SetActive(true);
        popup.transform.SetAsLastSibling();
        opened = this;
    }

    public void Close()
    {
        popup.SetActive(false);
        if (opened == this) opened = null;
    }

    public static bool TryClose()
    {
        if (!IsOpen) return false;
        opened.Close();
        return true;
    }

    public void ReturnToTitle()
    {
        Close();
        if (GameManager_2D.Instance != null) GameManager_2D.Instance.GoToTitle();
        else if (TENSUM_GameManager.Instance != null) TENSUM_GameManager.Instance.GoToTitle();
        else if (MineSweeper_GameManager.Instance != null) MineSweeper_GameManager.Instance.GoToTitle();
    }

    public void Restart()
    {
        Close();
        if (GameManager_2D.Instance != null) GameManager_2D.Instance.uiManager.ShowResetPanel();
        else if (TENSUM_GameManager.Instance != null) TENSUM_GameManager.Instance.RestartGame();
        else if (MineSweeper_GameManager.Instance != null) MineSweeper_GameManager.Instance.RestartGame();
    }
}
