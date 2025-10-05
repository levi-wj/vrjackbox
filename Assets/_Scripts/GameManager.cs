using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public bool IsRoundInProgress { get; private set; }
    
    public const string LOBBY_SCENE = "Lobby";
    public const string GAME_SCENE = "Game";
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    public void StartNewRound()
    {
        IsRoundInProgress = true;
        SceneManager.LoadScene(GAME_SCENE);
    }
    
    public void EndRound()
    {
        IsRoundInProgress = false;
        SceneManager.LoadScene(LOBBY_SCENE);
    }
}
