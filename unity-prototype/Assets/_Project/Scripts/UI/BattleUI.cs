using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BattleUI : MonoBehaviour
{
    [Header("Health Bars")]
    public Slider player1HealthBar;
    public Slider player2HealthBar;
    public TextMeshProUGUI player1HealthText;
    public TextMeshProUGUI player2HealthText;
    
    [Header("Special Meters")]
    public Slider player1SpecialMeter;
    public Slider player2SpecialMeter;
    public Slider player1RageMeter;
    public Slider player2RageMeter;
    
    [Header("Round Information")]
    public TextMeshProUGUI roundText;
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI player1WinsText;
    public TextMeshProUGUI player2WinsText;
    
    [Header("Round End")]
    public GameObject roundEndPanel;
    public TextMeshProUGUI roundEndText;
    public TextMeshProUGUI winnerText;
    
    [Header("Match End")]
    public GameObject matchEndPanel;
    public TextMeshProUGUI matchEndText;
    public Button rematchButton;
    public Button mainMenuButton;
    
    private static BattleUI instance;
    public static BattleUI Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<BattleUI>();
            }
            return instance;
        }
    }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        InitializeUI();
        SetupButtons();
    }

    private void Update()
    {
        UpdateHealthBars();
        UpdateMeters();
        UpdateRoundInfo();
    }

    private void InitializeUI()
    {
        // Hide end panels initially
        if (roundEndPanel != null) roundEndPanel.SetActive(false);
        if (matchEndPanel != null) matchEndPanel.SetActive(false);
        
        // Initialize health bars
        if (player1HealthBar != null) player1HealthBar.maxValue = 100f;
        if (player2HealthBar != null) player2HealthBar.maxValue = 100f;
        
        // Initialize meters
        if (player1SpecialMeter != null) player1SpecialMeter.maxValue = 100f;
        if (player2SpecialMeter != null) player2SpecialMeter.maxValue = 100f;
        if (player1RageMeter != null) player1RageMeter.maxValue = 100f;
        if (player2RageMeter != null) player2RageMeter.maxValue = 100f;
    }

    private void SetupButtons()
    {
        if (rematchButton != null)
        {
            rematchButton.onClick.AddListener(OnRematchClicked);
        }
        
        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.AddListener(OnMainMenuClicked);
        }
    }

    private void UpdateHealthBars()
    {
        PlayerCharacter p1 = CharacterManager.Instance?.GetPlayer1();
        PlayerCharacter p2 = CharacterManager.Instance?.GetPlayer2();
        
        if (p1 != null)
        {
            float healthPercent = p1.GetHealthPercentage() * 100f;
            if (player1HealthBar != null) player1HealthBar.value = healthPercent;
            if (player1HealthText != null) player1HealthText.text = $"{p1.currentHealth:F0}/{p1.maxHealth:F0}";
        }
        
        if (p2 != null)
        {
            float healthPercent = p2.GetHealthPercentage() * 100f;
            if (player2HealthBar != null) player2HealthBar.value = healthPercent;
            if (player2HealthText != null) player2HealthText.text = $"{p2.currentHealth:F0}/{p2.maxHealth:F0}";
        }
    }

    private void UpdateMeters()
    {
        PlayerCharacter p1 = CharacterManager.Instance?.GetPlayer1();
        PlayerCharacter p2 = CharacterManager.Instance?.GetPlayer2();
        
        if (p1 != null)
        {
            float specialPercent = p1.GetSpecialMeterPercentage() * 100f;
            if (player1SpecialMeter != null) player1SpecialMeter.value = specialPercent;
            
            if (p1 is PlayerCharacter player1)
            {
                float ragePercent = player1.GetRageMeterPercentage() * 100f;
                if (player1RageMeter != null) player1RageMeter.value = ragePercent;
            }
        }
        
        if (p2 != null)
        {
            float specialPercent = p2.GetSpecialMeterPercentage() * 100f;
            if (player2SpecialMeter != null) player2SpecialMeter.value = specialPercent;
            
            if (p2 is PlayerCharacter player2)
            {
                float ragePercent = player2.GetRageMeterPercentage() * 100f;
                if (player2RageMeter != null) player2RageMeter.value = ragePercent;
            }
        }
    }

    private void UpdateRoundInfo()
    {
        CharacterManager manager = CharacterManager.Instance;
        if (manager == null) return;
        
        // Update round text
        if (roundText != null)
        {
            roundText.text = $"ROUND {manager.currentRound}";
        }
        
        // Update wins
        if (player1WinsText != null)
        {
            player1WinsText.text = manager.player1Wins.ToString();
        }
        
        if (player2WinsText != null)
        {
            player2WinsText.text = manager.player2Wins.ToString();
        }
    }

    public void UpdateTimer(float time)
    {
        if (timerText != null)
        {
            int minutes = Mathf.FloorToInt(time / 60f);
            int seconds = Mathf.FloorToInt(time % 60f);
            timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
        }
    }

    public void OnRoundStart(int roundNumber)
    {
        Debug.Log($"Round {roundNumber} started!");
        
        // Hide end panels
        if (roundEndPanel != null) roundEndPanel.SetActive(false);
        if (matchEndPanel != null) matchEndPanel.SetActive(false);
        
        // Update round text
        if (roundText != null)
        {
            roundText.text = $"ROUND {roundNumber}";
        }
    }

    public void OnRoundEnd(PlayerCharacter winner)
    {
        Debug.Log($"Round ended! {winner.characterName} wins!");
        
        // Show round end panel
        if (roundEndPanel != null)
        {
            roundEndPanel.SetActive(true);
            
            if (winnerText != null)
            {
                winnerText.text = $"{winner.characterName} WINS!";
            }
            
            if (roundEndText != null)
            {
                CharacterManager manager = CharacterManager.Instance;
                if (manager != null)
                {
                    roundEndText.text = $"Round {manager.currentRound}";
                }
            }
        }
    }

    public void OnMatchEnd(PlayerCharacter winner)
    {
        Debug.Log($"Match ended! {winner.characterName} wins the match!");
        
        // Show match end panel
        if (matchEndPanel != null)
        {
            matchEndPanel.SetActive(true);
            
            if (matchEndText != null)
            {
                matchEndText.text = $"{winner.characterName} WINS THE MATCH!";
            }
        }
    }

    private void OnRematchClicked()
    {
        CharacterManager.Instance?.RestartMatch();
        
        // Hide panels
        if (roundEndPanel != null) roundEndPanel.SetActive(false);
        if (matchEndPanel != null) matchEndPanel.SetActive(false);
    }

    private void OnMainMenuClicked()
    {
        // Load main menu scene
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }

    public void ShowComboText(string comboText, Vector3 position)
    {
        // This would show combo text at the specified position
        Debug.Log($"Combo: {comboText} at {position}");
    }

    public void ShowDamageText(float damage, Vector3 position)
    {
        // This would show damage text at the specified position
        Debug.Log($"Damage: {damage} at {position}");
    }

    public void FlashHealthBar(int playerNumber)
    {
        // Flash the health bar when taking damage
        Slider healthBar = playerNumber == 1 ? player1HealthBar : player2HealthBar;
        if (healthBar != null)
        {
            // Could implement a flash effect here
            Debug.Log($"Health bar {playerNumber} flashed!");
        }
    }
} 