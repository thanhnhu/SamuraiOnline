using UnityEngine;
using System.Collections.Generic;

public class CharacterManager : MonoBehaviour
{
    [Header("Character Management")]
    public List<PlayerCharacter> activeCharacters = new List<PlayerCharacter>();
    public PlayerCharacter player1;
    public PlayerCharacter player2;
    
    [Header("Character Prefabs")]
    public GameObject[] characterPrefabs;
    public Transform[] spawnPoints;
    
    [Header("Game Settings")]
    public float roundTime = 99f;
    public int maxRounds = 3;
    public float roundEndDelay = 3f;
    
    [Header("Battle State")]
    public bool isRoundActive = false;
    public float currentRoundTime;
    public int currentRound = 1;
    public int player1Wins = 0;
    public int player2Wins = 0;

    [Header("Background Management")]
    public BackgroundManager backgroundManager;
    
    private static CharacterManager instance;
    public static CharacterManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<CharacterManager>();
                if (instance == null)
                {
                    GameObject go = new GameObject("CharacterManager");
                    instance = go.AddComponent<CharacterManager>();
                }
            }
            return instance;
        }
    }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        InitializeCharacterManager();
    }

    private void Update()
    {
        if (isRoundActive)
        {
            UpdateRoundTimer();
            CheckRoundEnd();
        }
    }

    private void InitializeCharacterManager()
    {
        currentRoundTime = roundTime;
        activeCharacters.Clear();
        
        // Load selected background
        LoadSelectedBackground();
        
        // Find existing characters or spawn new ones
        PlayerCharacter[] existingCharacters = FindObjectsOfType<PlayerCharacter>();
        if (existingCharacters.Length >= 2)
        {
            player1 = existingCharacters[0];
            player2 = existingCharacters[1];
            activeCharacters.Add(player1);
            activeCharacters.Add(player2);
        }
        else
        {
            SpawnCharacters();
        }
        
        // Set up character references
        SetupCharacterReferences();
    }

    private void SpawnCharacters()
    {
        if (characterPrefabs.Length < 2 || spawnPoints.Length < 2)
        {
            Debug.LogError("Not enough character prefabs or spawn points!");
            return;
        }

        // Spawn Player 1
        GameObject p1Obj = Instantiate(characterPrefabs[0], spawnPoints[0].position, spawnPoints[0].rotation);
        player1 = p1Obj.GetComponent<PlayerCharacter>();
        if (player1 != null)
        {
            player1.characterName = "Player 1";
            SetupCharacterInput(player1, 1);
            activeCharacters.Add(player1);
        }

        // Spawn Player 2
        GameObject p2Obj = Instantiate(characterPrefabs[1], spawnPoints[1].position, spawnPoints[1].rotation);
        player2 = p2Obj.GetComponent<PlayerCharacter>();
        if (player2 != null)
        {
            player2.characterName = "Player 2";
            SetupCharacterInput(player2, 2);
            activeCharacters.Add(player2);
        }
    }

    private void SetupCharacterInput(PlayerCharacter character, int playerNumber)
    {
        CharacterInputHandler inputHandler = character.GetComponent<CharacterInputHandler>();
        if (inputHandler != null)
        {
            inputHandler.playerNumber = playerNumber;
            
            // Set different controls for player 2
            if (playerNumber == 2)
            {
                inputHandler.leftKey = KeyCode.LeftArrow;
                inputHandler.rightKey = KeyCode.RightArrow;
                inputHandler.upKey = KeyCode.UpArrow;
                inputHandler.downKey = KeyCode.DownArrow;
                inputHandler.jumpKey = KeyCode.Return;
                inputHandler.lightAttackKey = KeyCode.Keypad1;
                inputHandler.mediumAttackKey = KeyCode.Keypad2;
                inputHandler.heavyAttackKey = KeyCode.Keypad3;
                inputHandler.blockKey = KeyCode.Keypad0;
                inputHandler.specialKey = KeyCode.KeypadPlus;
            }
        }
    }

    private void SetupCharacterReferences()
    {
        // Make characters face each other
        if (player1 != null && player2 != null)
        {
            // Player 1 faces right, Player 2 faces left
            if (player1.IsFacingRight() == player2.IsFacingRight())
            {
                player2.Flip();
            }
        }
    }

    public void StartRound()
    {
        isRoundActive = true;
        currentRoundTime = roundTime;
        
        // Reset character positions and health
        ResetCharacters();
        
        // Notify UI
        BattleUI.Instance?.OnRoundStart(currentRound);
    }

    public void EndRound(PlayerCharacter winner)
    {
        isRoundActive = false;
        
        if (winner == player1)
        {
            player1Wins++;
        }
        else if (winner == player2)
        {
            player2Wins++;
        }
        
        // Check if match is over
        if (player1Wins >= (maxRounds + 1) / 2 || player2Wins >= (maxRounds + 1) / 2)
        {
            EndMatch(winner);
        }
        else
        {
            // Start next round after delay
            Invoke(nameof(StartNextRound), roundEndDelay);
        }
        
        // Notify UI
        BattleUI.Instance?.OnRoundEnd(winner);
    }

    private void StartNextRound()
    {
        currentRound++;
        StartRound();
    }

    private void EndMatch(PlayerCharacter winner)
    {
        // Match is over
        Debug.Log($"Match ended! {winner.characterName} wins!");
        
        // Notify UI
        BattleUI.Instance?.OnMatchEnd(winner);
        
        // Could transition to results screen or main menu
    }

    private void UpdateRoundTimer()
    {
        currentRoundTime -= Time.deltaTime;
        
        if (currentRoundTime <= 0)
        {
            // Time's up - determine winner by health
            PlayerCharacter winner = DetermineWinnerByHealth();
            EndRound(winner);
        }
        
        // Update UI
        BattleUI.Instance?.UpdateTimer(currentRoundTime);
    }

    private void CheckRoundEnd()
    {
        if (player1.IsDead() && !player2.IsDead())
        {
            EndRound(player2);
        }
        else if (player2.IsDead() && !player1.IsDead())
        {
            EndRound(player1);
        }
        else if (player1.IsDead() && player2.IsDead())
        {
            // Double KO - determine winner by health percentage
            PlayerCharacter winner = DetermineWinnerByHealth();
            EndRound(winner);
        }
    }

    private PlayerCharacter DetermineWinnerByHealth()
    {
        float p1HealthPercent = player1.GetHealthPercentage();
        float p2HealthPercent = player2.GetHealthPercentage();
        
        if (p1HealthPercent > p2HealthPercent)
        {
            return player1;
        }
        else if (p2HealthPercent > p1HealthPercent)
        {
            return player2;
        }
        else
        {
            // Tie - random winner
            return Random.value > 0.5f ? player1 : player2;
        }
    }

    private void ResetCharacters()
    {
        if (player1 != null)
        {
            player1.currentHealth = player1.maxHealth;
            player1.specialMeter = 0f;
            player1.rageMeter = 0f;
            player1.transform.position = spawnPoints[0].position;
            player1.ResetCombo();
        }
        
        if (player2 != null)
        {
            player2.currentHealth = player2.maxHealth;
            player2.specialMeter = 0f;
            player2.rageMeter = 0f;
            player2.transform.position = spawnPoints[1].position;
            player2.ResetCombo();
        }
    }

    public PlayerCharacter GetPlayer1()
    {
        return player1;
    }

    public PlayerCharacter GetPlayer2()
    {
        return player2;
    }

    public List<PlayerCharacter> GetActiveCharacters()
    {
        return activeCharacters;
    }

    public void PauseGame()
    {
        Time.timeScale = 0f;
    }

    public void ResumeGame()
    {
        Time.timeScale = 1f;
    }

    public void RestartMatch()
    {
        player1Wins = 0;
        player2Wins = 0;
        currentRound = 1;
        StartRound();
    }
    
    private void LoadSelectedBackground()
    {
        // Find or create background manager
        if (backgroundManager == null)
        {
            backgroundManager = FindObjectOfType<BackgroundManager>();
        }
        
        if (backgroundManager == null)
        {
            // Create background manager if it doesn't exist
            GameObject bgManagerGO = new GameObject("BackgroundManager");
            backgroundManager = bgManagerGO.AddComponent<BackgroundManager>();
            
            // Create background renderer
            GameObject bgRendererGO = new GameObject("BackgroundRenderer");
            bgRendererGO.transform.SetParent(bgManagerGO.transform);
            SpriteRenderer bgRenderer = bgRendererGO.AddComponent<SpriteRenderer>();
            bgRenderer.sortingOrder = -10; // Behind everything
            
            backgroundManager.backgroundRenderer = bgRenderer;
        }
        
        // Load the selected stage background
        string selectedStage = PlayerPrefs.GetString("SelectedStage", "B001");
        string selectedStageName = PlayerPrefs.GetString("SelectedStageName", "Dojo Training Hall");
        
        Debug.Log($"Loading background: {selectedStageName} ({selectedStage})");
        backgroundManager.LoadBackground(selectedStage);
    }
} 