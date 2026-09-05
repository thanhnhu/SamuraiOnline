using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Realtime;
using Photon.Pun;

public class SpectatorUI : MonoBehaviour
{
    [Header("Spectator Panel")]
    public GameObject spectatorPanel;
    public GameObject spectatorListPanel;
    public GameObject cameraControlPanel;
    public GameObject matchInfoPanel;
    
    [Header("Spectator List")]
    public Transform spectatorListContent;
    public GameObject spectatorListItemPrefab;
    public TMP_Text spectatorCountText;
    public Button refreshSpectatorListButton;
    
    [Header("Camera Controls")]
    public Button cameraFollowP1Button;
    public Button cameraFollowP2Button;
    public Button cameraFreeButton;
    public Button cameraZoomInButton;
    public Button cameraZoomOutButton;
    public Slider cameraZoomSlider;
    public TMP_Text cameraZoomText;
    public Button cameraResetButton;
    
    [Header("Match Information")]
    public TMP_Text matchTitleText;
    public TMP_Text matchTimeText;
    public TMP_Text roundInfoText;
    public TMP_Text player1NameText;
    public TMP_Text player2NameText;
    public TMP_Text player1HealthText;
    public TMP_Text player2HealthText;
    public TMP_Text player1RageText;
    public TMP_Text player2RageText;
    public Image player1HealthBar;
    public Image player2HealthBar;
    public Image player1RageBar;
    public Image player2RageBar;
    
    [Header("Player Stats")]
    public Transform playerStatsContent;
    public GameObject playerStatItemPrefab;
    
    [Header("Network Info")]
    public TMP_Text pingText;
    public TMP_Text latencyText;
    public TMP_Text spectatorCountNetworkText;
    public TMP_Text roomNameText;
    
    [Header("Spectator Controls")]
    public Button leaveSpectatorButton;
    public Button takeScreenshotButton;
    public Button recordMatchButton;
    public Toggle showHitboxesToggle;
    public Toggle showInputsToggle;
    public Toggle showFrameDataToggle;
    
    [Header("Chat System")]
    public GameObject chatPanel;
    public Transform chatContent;
    public GameObject chatMessagePrefab;
    public TMP_InputField chatInputField;
    public Button sendChatButton;
    public ScrollRect chatScrollRect;
    
    // Private variables
    private SpectatorManager spectatorManager;
    private List<GameObject> spectatorListItems = new List<GameObject>();
    private List<GameObject> playerStatItems = new List<GameObject>();
    private List<GameObject> chatMessages = new List<GameObject>();
    private bool isRecording = false;
    private float matchStartTime = 0f;
    
    private void Start()
    {
        spectatorManager = FindObjectOfType<SpectatorManager>();
        if (spectatorManager == null)
        {
            Debug.LogError("SpectatorManager not found in scene!");
            return;
        }
        
        SetupUI();
        SetupEventListeners();
        
        // Subscribe to spectator manager events
        spectatorManager.OnSpectatingStarted += OnSpectatingStarted;
        spectatorManager.OnSpectatingStopped += OnSpectatingStopped;
        spectatorManager.OnSpectatorJoined += OnSpectatorJoined;
        spectatorManager.OnSpectatorLeft += OnSpectatorLeft;
    }
    
    private void Update()
    {
        if (spectatorManager != null && spectatorManager.IsSpectating())
        {
            UpdateMatchInfo();
            UpdateNetworkInfo();
        }
    }
    
    #region UI Setup
    
    private void SetupUI()
    {
        // Set initial states
        if (spectatorPanel != null)
            spectatorPanel.SetActive(false);
        
        if (chatPanel != null)
            chatPanel.SetActive(false);
        
        // Setup camera zoom slider
        if (cameraZoomSlider != null)
        {
            cameraZoomSlider.minValue = 5f;
            cameraZoomSlider.maxValue = 15f;
            cameraZoomSlider.value = 10f;
        }
        
        // Setup health bars
        if (player1HealthBar != null)
            player1HealthBar.fillAmount = 1f;
        
        if (player2HealthBar != null)
            player2HealthBar.fillAmount = 1f;
        
        if (player1RageBar != null)
            player1RageBar.fillAmount = 0f;
        
        if (player2RageBar != null)
            player2RageBar.fillAmount = 0f;
    }
    
    private void SetupEventListeners()
    {
        // Spectator list button
        if (refreshSpectatorListButton != null)
            refreshSpectatorListButton.onClick.AddListener(RefreshSpectatorList);
        
        // Camera control buttons
        if (cameraFollowP1Button != null)
            cameraFollowP1Button.onClick.AddListener(() => SetCameraTarget(1));
        
        if (cameraFollowP2Button != null)
            cameraFollowP2Button.onClick.AddListener(() => SetCameraTarget(2));
        
        if (cameraFreeButton != null)
            cameraFreeButton.onClick.AddListener(() => SetCameraTarget(0));
        
        if (cameraZoomInButton != null)
            cameraZoomInButton.onClick.AddListener(ZoomIn);
        
        if (cameraZoomOutButton != null)
            cameraZoomOutButton.onClick.AddListener(ZoomOut);
        
        if (cameraResetButton != null)
            cameraResetButton.onClick.AddListener(ResetCamera);
        
        if (cameraZoomSlider != null)
            cameraZoomSlider.onValueChanged.AddListener(SetCameraZoom);
        
        // Spectator control buttons
        if (leaveSpectatorButton != null)
            leaveSpectatorButton.onClick.AddListener(LeaveSpectatorMode);
        
        if (takeScreenshotButton != null)
            takeScreenshotButton.onClick.AddListener(TakeScreenshot);
        
        if (recordMatchButton != null)
            recordMatchButton.onClick.AddListener(ToggleRecording);
        
        // Toggle buttons
        if (showHitboxesToggle != null)
            showHitboxesToggle.onValueChanged.AddListener(SetShowHitboxes);
        
        if (showInputsToggle != null)
            showInputsToggle.onValueChanged.AddListener(SetShowInputs);
        
        if (showFrameDataToggle != null)
            showFrameDataToggle.onValueChanged.AddListener(SetShowFrameData);
        
        // Chat system
        if (sendChatButton != null)
            sendChatButton.onClick.AddListener(SendChatMessage);
        
        if (chatInputField != null)
        {
            chatInputField.onEndEdit.AddListener((text) => {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    SendChatMessage();
                }
            });
        }
    }
    
    #endregion
    
    #region Spectator List Management
    
    public void RefreshSpectatorList()
    {
        if (spectatorManager == null || spectatorListContent == null) return;
        
        // Clear existing items
        foreach (GameObject item in spectatorListItems)
        {
            if (item != null)
            {
                Destroy(item);
            }
        }
        spectatorListItems.Clear();
        
        // Get current spectators
        List<Player> spectators = spectatorManager.GetSpectators();
        
        // Update count text
        if (spectatorCountText != null)
        {
            spectatorCountText.text = $"Spectators: {spectators.Count}/{spectatorManager.maxSpectatorsPerRoom}";
        }
        
        // Create list items
        foreach (Player spectator in spectators)
        {
            CreateSpectatorListItem(spectator);
        }
    }
    
    private void CreateSpectatorListItem(Player spectator)
    {
        if (spectatorListItemPrefab == null || spectatorListContent == null) return;
        
        GameObject listItem = Instantiate(spectatorListItemPrefab, spectatorListContent);
        
        // Set up text components
        var nameText = listItem.transform.Find("NameText")?.GetComponent<TMP_Text>();
        if (nameText != null)
        {
            nameText.text = spectator.NickName;
        }
        
        var statusText = listItem.transform.Find("StatusText")?.GetComponent<TMP_Text>();
        if (statusText != null)
        {
            statusText.text = "Watching";
        }
        
        // Set up ping indicator
        var pingIndicator = listItem.transform.Find("PingIndicator")?.GetComponent<Image>();
        if (pingIndicator != null)
        {
            // int ping = spectator.GetPing(); // TODO: Implement ping measurement
            int ping = 0; // Placeholder
            Color pingColor = GetPingColor(ping);
            pingIndicator.color = pingColor;
        }
        
        spectatorListItems.Add(listItem);
    }
    
    private Color GetPingColor(int ping)
    {
        if (ping < 50) return Color.green;
        if (ping < 100) return Color.yellow;
        if (ping < 200) return new Color(1f, 0.5f, 0f); // Orange
        return Color.red;
    }
    
    #endregion
    
    #region Camera Controls
    
    private void SetCameraTarget(int playerId)
    {
        if (spectatorManager == null) return;
        
        if (playerId == 0)
        {
            // Free camera mode
            spectatorManager.SwitchSpectatorTarget(null);
        }
        else
        {
            // Follow specific player
            List<Player> players = spectatorManager.GetPlayersInMatch();
            if (playerId <= players.Count)
            {
                // Find the player's character and set as target
                var characters = FindObjectsOfType<PlayerCharacter>();
                foreach (var character in characters)
                {
                    // TODO: Implement proper player identification
                    // For now, just check by name
                    if (character.name.Contains(players[playerId - 1].NickName))
                    {
                        spectatorManager.SwitchSpectatorTarget(character.transform);
                        break;
                    }
                }
            }
        }
        
        UpdateCameraButtons(playerId);
    }
    
    private void ZoomIn()
    {
        if (spectatorManager == null) return;
        
        spectatorManager.ZoomIn();
        
        if (cameraZoomSlider != null)
        {
            cameraZoomSlider.value = Mathf.Max(cameraZoomSlider.value - 1f, cameraZoomSlider.minValue);
        }
    }
    
    private void ZoomOut()
    {
        if (spectatorManager == null) return;
        
        spectatorManager.ZoomOut();
        
        if (cameraZoomSlider != null)
        {
            cameraZoomSlider.value = Mathf.Min(cameraZoomSlider.value + 1f, cameraZoomSlider.maxValue);
        }
    }
    
    private void SetCameraZoom(float zoom)
    {
        if (spectatorManager == null) return;
        
        // This would need to be implemented in SpectatorManager
        // For now, just update the text
        if (cameraZoomText != null)
        {
            cameraZoomText.text = $"Zoom: {zoom:F1}x";
        }
    }
    
    private void ResetCamera()
    {
        if (spectatorManager == null) return;
        
        // Reset to default zoom and position
        if (cameraZoomSlider != null)
        {
            cameraZoomSlider.value = 10f;
        }
        
        // Set camera to follow first player
        SetCameraTarget(1);
    }
    
    private void UpdateCameraButtons(int activeTarget)
    {
        if (cameraFollowP1Button != null)
            cameraFollowP1Button.interactable = (activeTarget != 1);
        
        if (cameraFollowP2Button != null)
            cameraFollowP2Button.interactable = (activeTarget != 2);
        
        if (cameraFreeButton != null)
            cameraFreeButton.interactable = (activeTarget != 0);
    }
    
    #endregion
    
    #region Match Information
    
    private void UpdateMatchInfo()
    {
        if (spectatorManager == null) return;
        
        // Update match time
        if (matchTimeText != null)
        {
            float matchTime = Time.time - matchStartTime;
            matchTimeText.text = $"Time: {matchTime:F1}s";
        }
        
        // Update round info
        if (roundInfoText != null)
        {
            if (NetworkGameManager.Instance != null)
            {
                roundInfoText.text = $"Round {NetworkGameManager.Instance.currentRound}/{NetworkGameManager.Instance.maxRounds}";
            }
        }
        
        // Update player information
        UpdatePlayerInfo();
    }
    
    private void UpdatePlayerInfo()
    {
        if (spectatorManager == null) return;
        
        List<Player> players = spectatorManager.GetPlayersInMatch();
        
        if (players.Count >= 1)
        {
            UpdatePlayer1Info(players[0]);
        }
        
        if (players.Count >= 2)
        {
            UpdatePlayer2Info(players[1]);
        }
    }
    
    private void UpdatePlayer1Info(Player player)
    {
        if (player1NameText != null)
            player1NameText.text = player.NickName;
        
        // Find player's character
        var character = FindPlayerCharacter(player);
        if (character != null)
        {
            if (player1HealthText != null)
                player1HealthText.text = $"HP: {character.currentHealth}/{character.maxHealth}";
            
            if (player1RageText != null)
                player1RageText.text = $"Rage: {character.rageMeter:F0}%";
            
            if (player1HealthBar != null)
                player1HealthBar.fillAmount = (float)character.currentHealth / character.maxHealth;
            
            if (player1RageBar != null)
                player1RageBar.fillAmount = character.rageMeter / 100f;
        }
    }
    
    private void UpdatePlayer2Info(Player player)
    {
        if (player2NameText != null)
            player2NameText.text = player.NickName;
        
        // Find player's character
        var character = FindPlayerCharacter(player);
        if (character != null)
        {
            if (player2HealthText != null)
                player2HealthText.text = $"HP: {character.currentHealth}/{character.maxHealth}";
            
            if (player2RageText != null)
                player2RageText.text = $"Rage: {character.rageMeter:F0}%";
            
            if (player2HealthBar != null)
                player2HealthBar.fillAmount = (float)character.currentHealth / character.maxHealth;
            
            if (player2RageBar != null)
                player2RageBar.fillAmount = character.rageMeter / 100f;
        }
    }
    
    private PlayerCharacter FindPlayerCharacter(Player player)
    {
        var characters = FindObjectsOfType<PlayerCharacter>();
        foreach (var character in characters)
        {
            // TODO: Implement proper player identification
            // For now, just check by name
            if (character.name.Contains(player.NickName))
            {
                return character;
            }
        }
        return null;
    }
    
    #endregion
    
    #region Spectator Controls
    
    private void LeaveSpectatorMode()
    {
        if (spectatorManager == null) return;
        
        spectatorManager.LeaveSpectatorMode();
    }
    
    private void TakeScreenshot()
    {
        string fileName = $"Screenshot_{System.DateTime.Now:yyyyMMdd_HHmmss}.png";
        string filePath = System.IO.Path.Combine(Application.persistentDataPath, fileName);
        
        try
        {
            ScreenCapture.CaptureScreenshotAsTexture();
            Debug.Log($"Screenshot saved to: {filePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to take screenshot: {e.Message}");
        }
    }
    
    private void ToggleRecording()
    {
        isRecording = !isRecording;
        
        if (recordMatchButton != null)
        {
            var textComponent = recordMatchButton.GetComponentInChildren<TMP_Text>();
            if (textComponent != null)
            {
                textComponent.text = isRecording ? "Stop Recording" : "Start Recording";
            }
        }
        
        if (isRecording)
        {
            Debug.Log("Started recording match");
        }
        else
        {
            Debug.Log("Stopped recording match");
        }
    }
    
    private void SetShowHitboxes(bool show)
    {
        // Toggle hitbox visualization
        Debug.Log($"Show hitboxes: {show}");
    }
    
    private void SetShowInputs(bool show)
    {
        // Toggle input display
        Debug.Log($"Show inputs: {show}");
    }
    
    private void SetShowFrameData(bool show)
    {
        // Toggle frame data display
        Debug.Log($"Show frame data: {show}");
    }
    
    #endregion
    
    #region Chat System
    
    private void SendChatMessage()
    {
        if (chatInputField == null || string.IsNullOrEmpty(chatInputField.text)) return;
        
        string message = chatInputField.text.Trim();
        if (string.IsNullOrEmpty(message)) return;
        
        // Add message to chat
        AddChatMessage(PhotonNetwork.LocalPlayer.NickName, message);
        
        // Send to other spectators (this would need to be implemented with Photon RPCs)
        // For now, just clear the input
        chatInputField.text = "";
        
        Debug.Log($"Chat: {PhotonNetwork.LocalPlayer.NickName}: {message}");
    }
    
    private void AddChatMessage(string playerName, string message)
    {
        if (chatMessagePrefab == null || chatContent == null) return;
        
        GameObject chatMessage = Instantiate(chatMessagePrefab, chatContent);
        
        var nameText = chatMessage.transform.Find("NameText")?.GetComponent<TMP_Text>();
        if (nameText != null)
            nameText.text = playerName;
        
        var messageText = chatMessage.transform.Find("MessageText")?.GetComponent<TMP_Text>();
        if (messageText != null)
            messageText.text = message;
        
        var timeText = chatMessage.transform.Find("TimeText")?.GetComponent<TMP_Text>();
        if (timeText != null)
            timeText.text = System.DateTime.Now.ToString("HH:mm");
        
        chatMessages.Add(chatMessage);
        
        // Limit chat messages
        if (chatMessages.Count > 50)
        {
            GameObject oldMessage = chatMessages[0];
            chatMessages.RemoveAt(0);
            if (oldMessage != null)
                Destroy(oldMessage);
        }
        
        // Scroll to bottom
        if (chatScrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            chatScrollRect.verticalNormalizedPosition = 0f;
        }
    }
    
    #endregion
    
    #region Network Info
    
    private void UpdateNetworkInfo()
    {
        if (pingText != null)
            pingText.text = $"Ping: {PhotonNetwork.GetPing()}ms";
        
        if (latencyText != null)
            latencyText.text = $"Latency: {PhotonNetwork.NetworkClientState}";
        
        if (spectatorCountNetworkText != null)
        {
            List<Player> spectators = spectatorManager.GetSpectators();
            spectatorCountNetworkText.text = $"Spectators: {spectators.Count}";
        }
        
        if (roomNameText != null)
            roomNameText.text = $"Room: {PhotonNetwork.CurrentRoom.Name}";
    }
    
    #endregion
    
    #region Event Handlers
    
    private void OnSpectatingStarted(string roomName)
    {
        if (spectatorPanel != null)
            spectatorPanel.SetActive(true);
        
        matchStartTime = Time.time;
        
        if (matchTitleText != null)
            matchTitleText.text = $"Spectating: {roomName}";
        
        RefreshSpectatorList();
        
        Debug.Log($"Started spectating room: {roomName}");
    }
    
    private void OnSpectatingStopped()
    {
        if (spectatorPanel != null)
            spectatorPanel.SetActive(false);
        
        if (chatPanel != null)
            chatPanel.SetActive(false);
        
        // Clear lists
        foreach (GameObject item in spectatorListItems)
        {
            if (item != null)
                Destroy(item);
        }
        spectatorListItems.Clear();
        
        foreach (GameObject item in playerStatItems)
        {
            if (item != null)
                Destroy(item);
        }
        playerStatItems.Clear();
        
        foreach (GameObject message in chatMessages)
        {
            if (message != null)
                Destroy(message);
        }
        chatMessages.Clear();
        
        Debug.Log("Stopped spectating");
    }
    
    private void OnSpectatorJoined(Player spectator)
    {
        RefreshSpectatorList();
        AddChatMessage("System", $"{spectator.NickName} joined as spectator");
    }
    
    private void OnSpectatorLeft(Player spectator)
    {
        RefreshSpectatorList();
        AddChatMessage("System", $"{spectator.NickName} left");
    }
    
    #endregion
    
    #region Public Methods
    
    public void ShowSpectatorUI()
    {
        if (spectatorPanel != null)
            spectatorPanel.SetActive(true);
    }
    
    public void HideSpectatorUI()
    {
        if (spectatorPanel != null)
            spectatorPanel.SetActive(false);
    }
    
    public void ToggleSpectatorUI()
    {
        if (spectatorPanel != null)
        {
            bool isActive = spectatorPanel.activeSelf;
            if (isActive)
                HideSpectatorUI();
            else
                ShowSpectatorUI();
        }
    }
    
    public void ToggleChat()
    {
        if (chatPanel != null)
        {
            chatPanel.SetActive(!chatPanel.activeSelf);
            
            if (chatPanel.activeSelf && chatInputField != null)
            {
                chatInputField.Select();
                chatInputField.ActivateInputField();
            }
        }
    }
    
    #endregion
} 