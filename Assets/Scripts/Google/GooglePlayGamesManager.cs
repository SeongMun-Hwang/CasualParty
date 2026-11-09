using UnityEngine;
// Compatibility component for scenes that already reference this script.
// PlayGamesService synchronizes existing SDK sessions; interactive sign-in requires user action.
public class GooglePlayGamesManager : MonoBehaviour
{
    public void SignIn()
    {
        if (PlayGamesService.Instance != null) PlayGamesService.Instance.SignIn();
    }
}
