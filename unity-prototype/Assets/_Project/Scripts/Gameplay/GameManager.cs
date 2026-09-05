using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public CharacterStatus player;
    public CharacterStatus enemy;
    public Transform playerSpawn;
    public Transform enemySpawn;
    public int maxRounds = 3;
    public int currentRound = 1;
    public int playerWins = 0;
    public int enemyWins = 0;

    public SimpleUIManager uiManager;

    public string SelectedCharacter { get; set; }
    public string SelectedStage { get; set; }

    public void SetSelectedCharacter(string characterName)
    {
        SelectedCharacter = characterName;
    }

    public void SetSelectedStage(string stageName)
    {
        SelectedStage = stageName;
    }

    void Awake()
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

    void Start()
    {
        StartRound();
    }

    void Update()
    {
        if (player.state == CharacterState.Dead)
        {
            enemyWins++;
            EndRound(false);
        }
        else if (enemy.state == CharacterState.Dead)
        {
            playerWins++;
            EndRound(true);
        }
    }

    void StartRound()
    {
        currentRound++;
        // Reset vị trí và máu
        player.transform.position = playerSpawn.position;
        enemy.transform.position = enemySpawn.position;
        player.currentHealth = player.maxHealth;
        enemy.currentHealth = enemy.maxHealth;
        player.state = CharacterState.Idle;
        enemy.state = CharacterState.Idle;
        uiManager?.UpdateUI();
    }

    void EndRound(bool playerWin)
    {
        uiManager?.ShowRoundResult(playerWin ? "Player thắng!" : "Enemy thắng!");
        Invoke(nameof(StartRound), 2f);
    }
} 