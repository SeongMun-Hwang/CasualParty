using UnityEngine;
// Compatibility component for scenes that already reference this script.
// PlayGamesService starts authentication only after an explicit user action.
public class GooglePlayGamesManager : MonoBehaviour
{
    public void SignIn()
    {
        if (PlayGamesService.Instance != null) PlayGamesService.Instance.SignIn();
    }
}
