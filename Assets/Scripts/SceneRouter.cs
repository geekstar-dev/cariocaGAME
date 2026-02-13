using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneRouter : MonoBehaviour
{
    public void GoHome() => Load("Home");
    public void GoSoloSetup() => Load("SoloSetup");
    public void GoMultiplayerMenu() => Load("MultiplayerMenu");
    public void GoOptions() => Load("Options");
    public void GoGameTable() => Load("GameTable");

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void Load(string sceneName) => SceneManager.LoadScene(sceneName);
}