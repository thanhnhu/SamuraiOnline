using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject mainMenuPanel;
    public GameObject battlePanel;
    public GameObject networkPanel;
    public GameObject spectatorPanel;
    public GameObject replayPanel;
    public GameObject settingsPanel;
    public GameObject pausePanel;
    public GameObject loadingPanel;
    
    [Header("UI Managers")]
    public NetworkUI networkUI;
    public SpectatorUI spectatorUI;
    public ReplayUI replayUI;
    public BattleUI battleUI;
    
    [Header("Navigation")]
    public Button mainMenuButton;
    public Button networkButton;
    public Button spectatorButton;
    public Button replayButton;
    public Button settingsButton;
    public Button pauseButton;
    
    [Header("Status Display")]
    public GameObject statusPanel;
    public TMP_Text statusText;
    public Image statusIcon;
    public Button statusButton;
    
    [Header("Notifications")]
    public GameObject notificationPanel;
    public TMP_Text notificationText;
    public Button notificationButton;
    public float notificationDuration = 3f;
    
    [Header("Loading Screen")]
    public GameObject loadingScreen;
    public TMP_Text loadingText;
    public Slider loadingProgress;
    public Image loadingBackground;
    
    [Header("Transition Effects")]
    public Animator transitionAnimator;
    public float transitionDuration = 0.5f;
    
    // Private variables
    private GameObject currentPanel;
    private bool isTransitioning = false;
    private System.Action onTransitionComplete;
    private float notificationTimer = 0f;
    private bool showingNotification = false;
    
    // Events
    public System.Action<string> OnPanelChanged;
    public System.Action<string> OnNotificationShown;
    public System.Action OnLoadingStarted;
    public System.Action OnLoadingFinished;
    
    private void Start()
    {
        SetupUI();
        SetupEventListeners();
        
        // Show main menu by default
        ShowPanel("MainMenu");
    }
    
    private void Update()
    {
        // Handle notification timer
        if (showingNotification)
        {
            notificationTimer -= Time.deltaTime;
            if (notificationTimer <= 0f)
            {
                HideNotification();
            }
        }
        
        // Handle escape key for pause menu
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            HandleEscapeKey();
        }
    }
    
    #region UI Setup
    
    private void SetupUI()
    {
        // Set initial states
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(false);
        
        if (battlePanel != null)
            battlePanel.SetActive(false);
        
        if (networkPanel != null)
            networkPanel.SetActive(false);
        
        if (spectatorPanel != null)
            spectatorPanel.SetActive(false);
        
        if (replayPanel != null)
            replayPanel.SetActive(false);
        
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
        
        if (pausePanel != null)
            pausePanel.SetActive(false);
        
        if (loadingPanel != null)
            loadingPanel.SetActive(false);
        
        if (statusPanel != null)
            statusPanel.SetActive(false);
        
        if (notificationPanel != null)
            notificationPanel.SetActive(false);
        
        if (loadingScreen != null)
            loadingScreen.SetActive(false);
    }
    
    private void SetupEventListeners()
    {
        // Navigation buttons
        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(() => ShowPanel("MainMenu"));
        
        if (networkButton != null)
            networkButton.onClick.AddListener(() => ShowPanel("Network"));
        
        if (spectatorButton != null)
            spectatorButton.onClick.AddListener(() => ShowPanel("Spectator"));
        
        if (replayButton != null)
            replayButton.onClick.AddListener(() => ShowPanel("Replay"));
        
        if (settingsButton != null)
            settingsButton.onClick.AddListener(() => ShowPanel("Settings"));
        
        if (pauseButton != null)
            pauseButton.onClick.AddListener(() => ShowPanel("Pause"));
        
        // Status button
        if (statusButton != null)
            statusButton.onClick.AddListener(HideStatus);
        
        // Notification button
        if (notificationButton != null)
            notificationButton.onClick.AddListener(HideNotification);
    }
    
    #endregion
    
    #region Panel Management
    
    public void ShowPanel(string panelName)
    {
        if (isTransitioning) return;
        
        GameObject targetPanel = GetPanelByName(panelName);
        if (targetPanel == null)
        {
            Debug.LogWarning($"Panel '{panelName}' not found!");
            return;
        }
        
        if (currentPanel == targetPanel) return;
        
        // Start transition
        StartTransition(() => {
            // Hide current panel
            if (currentPanel != null)
            {
                currentPanel.SetActive(false);
            }
            
            // Show target panel
            targetPanel.SetActive(true);
            currentPanel = targetPanel;
            
            // Handle panel-specific setup
            HandlePanelSetup(panelName);
            
            OnPanelChanged?.Invoke(panelName);
        });
    }
    
    private GameObject GetPanelByName(string panelName)
    {
        switch (panelName.ToLower())
        {
            case "mainmenu":
                return mainMenuPanel;
            case "battle":
                return battlePanel;
            case "network":
                return networkPanel;
            case "spectator":
                return spectatorPanel;
            case "replay":
                return replayPanel;
            case "settings":
                return settingsPanel;
            case "pause":
                return pausePanel;
            case "loading":
                return loadingPanel;
            default:
                return null;
        }
    }
    
    private void HandlePanelSetup(string panelName)
    {
        switch (panelName.ToLower())
        {
            case "network":
                if (networkUI != null)
                    networkUI.gameObject.SetActive(true);
                break;
                
            case "spectator":
                if (spectatorUI != null)
                    spectatorUI.ShowSpectatorUI();
                break;
                
            case "replay":
                if (replayUI != null)
                    replayUI.ShowReplayUI();
                break;
                
            case "battle":
                if (battleUI != null)
                    battleUI.gameObject.SetActive(true);
                break;
        }
    }
    
    public void HidePanel(string panelName)
    {
        GameObject panel = GetPanelByName(panelName);
        if (panel != null)
        {
            panel.SetActive(false);
        }
    }
    
    public void HideAllPanels()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (battlePanel != null) battlePanel.SetActive(false);
        if (networkPanel != null) networkPanel.SetActive(false);
        if (spectatorPanel != null) spectatorPanel.SetActive(false);
        if (replayPanel != null) replayPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);
        if (loadingPanel != null) loadingPanel.SetActive(false);
        
        currentPanel = null;
    }
    
    #endregion
    
    #region Transition Effects
    
    private void StartTransition(System.Action onComplete)
    {
        if (transitionAnimator != null)
        {
            isTransitioning = true;
            onTransitionComplete = onComplete;
            
            // Play transition animation
            transitionAnimator.SetTrigger("StartTransition");
            
            // Wait for transition duration
            Invoke(nameof(CompleteTransition), transitionDuration);
        }
        else
        {
            // No transition animator, complete immediately
            onComplete?.Invoke();
        }
    }
    
    private void CompleteTransition()
    {
        isTransitioning = false;
        onTransitionComplete?.Invoke();
        onTransitionComplete = null;
        
        if (transitionAnimator != null)
        {
            transitionAnimator.SetTrigger("EndTransition");
        }
    }
    
    #endregion
    
    #region Status Display
    
    public void ShowStatus(string message, StatusType type = StatusType.Info)
    {
        if (statusPanel == null) return;
        
        statusPanel.SetActive(true);
        
        if (statusText != null)
            statusText.text = message;
        
        if (statusIcon != null)
        {
            // Set icon based on status type
            switch (type)
            {
                case StatusType.Success:
                    statusIcon.color = Color.green;
                    break;
                case StatusType.Warning:
                    statusIcon.color = Color.yellow;
                    break;
                case StatusType.Error:
                    statusIcon.color = Color.red;
                    break;
                default:
                    statusIcon.color = Color.blue;
                    break;
            }
        }
    }
    
    public void HideStatus()
    {
        if (statusPanel != null)
            statusPanel.SetActive(false);
    }
    
    #endregion
    
    #region Notifications
    
    public void ShowNotification(string message, float duration = -1f)
    {
        if (notificationPanel == null) return;
        
        notificationPanel.SetActive(true);
        
        if (notificationText != null)
            notificationText.text = message;
        
        notificationTimer = duration > 0f ? duration : notificationDuration;
        showingNotification = true;
        
        OnNotificationShown?.Invoke(message);
    }
    
    public void HideNotification()
    {
        if (notificationPanel != null)
            notificationPanel.SetActive(false);
        
        showingNotification = false;
    }
    
    #endregion
    
    #region Loading Screen
    
    public void ShowLoadingScreen(string message = "Loading...")
    {
        if (loadingScreen == null) return;
        
        loadingScreen.SetActive(true);
        
        if (loadingText != null)
            loadingText.text = message;
        
        if (loadingProgress != null)
            loadingProgress.value = 0f;
        
        OnLoadingStarted?.Invoke();
    }
    
    public void UpdateLoadingProgress(float progress, string message = null)
    {
        if (loadingScreen == null || !loadingScreen.activeSelf) return;
        
        if (loadingProgress != null)
            loadingProgress.value = Mathf.Clamp01(progress);
        
        if (loadingText != null && !string.IsNullOrEmpty(message))
            loadingText.text = message;
    }
    
    public void HideLoadingScreen()
    {
        if (loadingScreen != null)
            loadingScreen.SetActive(false);
        
        OnLoadingFinished?.Invoke();
    }
    
    #endregion
    
    #region Input Handling
    
    private void HandleEscapeKey()
    {
        // Don't handle escape if we're in a text input field
        if (IsAnyInputFieldFocused()) return;
        
        // If we're in a sub-panel, go back to main menu
        if (currentPanel != mainMenuPanel)
        {
            ShowPanel("MainMenu");
        }
        else
        {
            // If we're already in main menu, show pause menu
            ShowPanel("Pause");
        }
    }
    
    private bool IsAnyInputFieldFocused()
    {
        // Check if any input field is currently focused
        var inputFields = FindObjectsOfType<TMP_InputField>();
        foreach (var inputField in inputFields)
        {
            if (inputField.isFocused)
                return true;
        }
        return false;
    }
    
    #endregion
    
    #region Game State Integration
    
    public void OnGameStarted()
    {
        ShowPanel("Battle");
        ShowNotification("Game started!", 2f);
    }
    
    public void OnGameEnded()
    {
        ShowNotification("Game ended!", 3f);
        
        // Show results for a few seconds, then return to main menu
        Invoke(nameof(ReturnToMainMenu), 3f);
    }
    
    public void OnNetworkConnected()
    {
        ShowStatus("Connected to network", StatusType.Success);
        ShowNotification("Connected to network!", 2f);
    }
    
    public void OnNetworkDisconnected()
    {
        ShowStatus("Disconnected from network", StatusType.Warning);
        ShowNotification("Disconnected from network", 3f);
    }
    
    public void OnMatchFound()
    {
        ShowNotification("Match found! Joining...", 2f);
    }
    
    public void OnSpectatingStarted()
    {
        ShowPanel("Spectator");
        ShowNotification("Spectating mode activated", 2f);
    }
    
    public void OnReplayLoaded()
    {
        ShowPanel("Replay");
        ShowNotification("Replay loaded successfully", 2f);
    }
    
    private void ReturnToMainMenu()
    {
        ShowPanel("MainMenu");
    }
    
    #endregion
    
    #region Utility Methods
    
    public void QuitGame()
    {
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
    
    public void RestartGame()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
        );
    }
    
    public void OpenURL(string url)
    {
        Application.OpenURL(url);
    }
    
    public void CopyToClipboard(string text)
    {
        GUIUtility.systemCopyBuffer = text;
        ShowNotification("Copied to clipboard!", 1f);
    }
    
    #endregion
    
    #region Public Properties
    
    public bool IsTransitioning => isTransitioning;
    public GameObject CurrentPanel => currentPanel;
    public bool IsNotificationShowing => showingNotification;
    public bool IsLoadingScreenShowing => loadingScreen != null && loadingScreen.activeSelf;
    
    #endregion
}

public enum StatusType
{
    Info,
    Success,
    Warning,
    Error
} 