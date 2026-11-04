using UnityEngine;
// Compatibility component for scenes that already reference this script.
// PlayGamesService owns automatic authentication and prevents concurrent requests.
public class GooglePlayGamesManager : MonoBehaviour
{
    public void SignIn()
    {
        if (PlayGamesService.Instance != null) PlayGamesService.Instance.SignIn();
    }
}
