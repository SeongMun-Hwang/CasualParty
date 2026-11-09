#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;

public class TitleManager : MonoBehaviour
{
    public void StackTowerBtnOnClicked() => GameNavigation.Load("StackTower");
    public void ShowRankings()
    {
        if (PlayGamesService.Instance != null) PlayGamesService.Instance.ShowAllLeaderboards();
    }
    //싱글게임 -> 새게임 버튼
    public void BlockdokuBtnOnClicked()
    {
        // Removed File.Delete calls to allow loading existing save data
        SceneManager.LoadScene("Blockdoku_2D");
    }
    //싱글게임 -> 새게임 버튼
    public void TenSumBtnOnClicked()
    {
        SceneManager.LoadScene("10SUM");
    }
    public void MineSweeperBtnOnclicked()
    {
        SceneManager.LoadScene("MineSweeper");
    }
    public void QuitBtnOnClicked()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
