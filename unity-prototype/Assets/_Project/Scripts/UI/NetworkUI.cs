using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Photon.Pun;

public class NetworkUI : MonoBehaviour
{
    [Header("Connection UI")]
    public GameObject connectionPanel;
    public TextMeshProUGUI connectionStatusText;
    public Button connectButton;
    public Button disconnectButton;
    public Slider connectionProgressBar;
    
    [Header("Matchmaking UI")]
    public GameObject matchmakingPanel;
    public Button findMatchButton;
    public Button createPrivateMatchButton;
    public Button joinPrivateMatchButton;
    public Button stopMatchmakingButton;
    public TextMeshProUGUI searchStatusText;
    public Slider searchProgressBar;
    public TextMeshProUGUI searchTimeText;
    
    [Header("Room UI")]
    public GameObject roomPanel;
    public TextMeshProUGUI roomNameText;
    public TextMeshProUGUI playerCountText;
    public TextMeshProUGUI roomStatusText;
    public Button leaveRoomButton;
    public Button startGameButton;
    public Button readyButton;
    
    [Header("Player Settings")]
    public TMP_Dropdown characterDropdown;
    public TMP_Dropdown skillLevelDropdown;
    public TMP_Dropdown regionDropdown;
    public Toggle crossRegionToggle;
    public Toggle rankedMatchToggle;
    
    [Header("Private Match")]
    public GameObject privateMatchPanel;
    public TMP_InputField roomNameInput;
    public TMP_InputField passwordInput;
    public Button createRoomButton;
    public Button joinRoomButton;
    
    [Header("Network Info")]
    public GameObject networkInfoPanel;
    public TextMeshProUGUI pingText;
    public TextMeshProUGUI latencyText;
    public TextMeshProUGUI packetLossText;
    public TextMeshProUGUI frameRateText;
    
    [Header("Debug")]
    public bool showDebugInfo = false;
    public TextMeshProUGUI debugText;
    
    private NetworkManager networkManager;
    private MatchmakingManager matchmakingManager;
    private float lastUpdateTime = 0f;
    private float updateInterval = 0.5f;

    private void Awake()
    {
        networkManager = NetworkManager.Instance;
        matchmakingManager = MatchmakingManager.Instance;
        
        SetupUI();
        SetupEventListeners();
    }

    private void Start()
    {
        UpdateUI();
    }

    private void Update()
    {
        if (Time.time - lastUpdateTime > updateInterval)
        {
            UpdateUI();
            lastUpdateTime = Time.time;
        }
    }

    private void SetupUI()
    {
        // Initialize dropdowns
        SetupCharacterDropdown();
        SetupSkillLevelDropdown();
        SetupRegionDropdown();
        
        // Set default values
        if (crossRegionToggle != null)
            crossRegionToggle.isOn = true;
        
        if (rankedMatchToggle != null)
            rankedMatchToggle.isOn = false;
    }

    private void SetupEventListeners()
    {
        // Connection buttons
        if (connectButton != null)
            connectButton.onClick.AddListener(OnConnectClicked);
        
        if (disconnectButton != null)
            disconnectButton.onClick.AddListener(OnDisconnectClicked);
        
        // Matchmaking buttons
        if (findMatchButton != null)
            findMatchButton.onClick.AddListener(OnFindMatchClicked);
        
        if (createPrivateMatchButton != null)
            createPrivateMatchButton.onClick.AddListener(OnCreatePrivateMatchClicked);
        
        if (joinPrivateMatchButton != null)
            joinPrivateMatchButton.onClick.AddListener(OnJoinPrivateMatchClicked);
        
        if (stopMatchmakingButton != null)
            stopMatchmakingButton.onClick.AddListener(OnStopMatchmakingClicked);
        
        // Room buttons
        if (leaveRoomButton != null)
            leaveRoomButton.onClick.AddListener(OnLeaveRoomClicked);
        
        if (startGameButton != null)
            startGameButton.onClick.AddListener(OnStartGameClicked);
        
        if (readyButton != null)
            readyButton.onClick.AddListener(OnReadyClicked);
        
        // Private match buttons
        if (createRoomButton != null)
            createRoomButton.onClick.AddListener(OnCreateRoomClicked);
        
        if (joinRoomButton != null)
            joinRoomButton.onClick.AddListener(OnJoinRoomClicked);
        
        // Dropdowns
        if (characterDropdown != null)
            characterDropdown.onValueChanged.AddListener(OnCharacterChanged);
        
        if (skillLevelDropdown != null)
            skillLevelDropdown.onValueChanged.AddListener(OnSkillLevelChanged);
        
        if (regionDropdown != null)
            regionDropdown.onValueChanged.AddListener(OnRegionChanged);
        
        if (crossRegionToggle != null)
            crossRegionToggle.onValueChanged.AddListener(OnCrossRegionChanged);
        
        if (rankedMatchToggle != null)
            rankedMatchToggle.onValueChanged.AddListener(OnRankedMatchChanged);
        
        // Network events
        if (networkManager != null)
        {
            networkManager.OnConnectedToServer += OnConnectedToServer;
            networkManager.OnDisconnectedFromServer += OnDisconnectedFromServer;
            networkManager.OnRoomJoined += OnJoinedRoom;
            networkManager.OnRoomLeft += OnLeftRoom;
            networkManager.OnPlayerJoined += OnPlayerJoined;
            networkManager.OnPlayerLeft += OnPlayerLeft;
            networkManager.OnGameStart += OnGameStart;
        }
        
        // Matchmaking events
        if (matchmakingManager != null)
        {
            matchmakingManager.OnSearchStarted += OnSearchStarted;
            matchmakingManager.OnSearchStopped += OnSearchStopped;
            matchmakingManager.OnMatchReady += OnMatchReady;
            matchmakingManager.OnMatchmakingError += OnMatchmakingError;
        }
    }

    private void SetupCharacterDropdown()
    {
        if (characterDropdown == null) return;
        
        characterDropdown.ClearOptions();
        List<string> options = new List<string>
        {
            "Samurai",
            "Ninja", 
            "Monk",
            "Warrior",
            "Archer"
        };
        
        characterDropdown.AddOptions(options);
        characterDropdown.value = 0;
    }

    private void SetupSkillLevelDropdown()
    {
        if (skillLevelDropdown == null) return;
        
        skillLevelDropdown.ClearOptions();
        List<string> options = new List<string>();
        
        for (int i = 1; i <= 10; i++)
        {
            options.Add($"Level {i}");
        }
        
        skillLevelDropdown.AddOptions(options);
        skillLevelDropdown.value = 0;
    }

    private void SetupRegionDropdown()
    {
        if (regionDropdown == null) return;
        
        regionDropdown.ClearOptions();
        List<string> options = new List<string>
        {
            "US",
            "EU", 
            "Asia",
            "Global"
        };
        
        regionDropdown.AddOptions(options);
        regionDropdown.value = 0;
    }

    private void UpdateUI()
    {
        UpdateConnectionUI();
        UpdateMatchmakingUI();
        UpdateRoomUI();
        UpdateNetworkInfo();
        UpdateDebugInfo();
    }

    private void UpdateConnectionUI()
    {
        if (networkManager == null) return;
        
        if (connectionStatusText != null)
        {
            connectionStatusText.text = $"Status: {networkManager.currentState}";
        }
        
        if (connectButton != null)
        {
            connectButton.interactable = !networkManager.isConnected;
        }
        
        if (disconnectButton != null)
        {
            disconnectButton.interactable = networkManager.isConnected;
        }
        
        if (connectionProgressBar != null)
        {
            connectionProgressBar.gameObject.SetActive(networkManager.currentState == NetworkState.Connecting);
        }
    }

    private void UpdateMatchmakingUI()
    {
        if (matchmakingManager == null) return;
        
        bool isSearching = matchmakingManager.currentState == MatchmakingState.Searching;
        
        if (findMatchButton != null)
        {
            findMatchButton.interactable = networkManager.isConnected && !isSearching;
        }
        
        if (stopMatchmakingButton != null)
        {
            stopMatchmakingButton.interactable = isSearching;
        }
        
        if (searchStatusText != null)
        {
            searchStatusText.text = $"Searching... ({matchmakingManager.GetSearchAttempts() + 1}/{matchmakingManager.maxSearchAttempts})";
            searchStatusText.gameObject.SetActive(isSearching);
        }
        
        if (searchProgressBar != null)
        {
            searchProgressBar.gameObject.SetActive(isSearching);
            if (isSearching)
            {
                float progress = matchmakingManager.GetSearchTime() / matchmakingManager.searchTimeout;
                searchProgressBar.value = progress;
            }
        }
        
        if (searchTimeText != null)
        {
            searchTimeText.text = $"Time: {matchmakingManager.GetSearchTime():F1}s";
            searchTimeText.gameObject.SetActive(isSearching);
        }
    }

    private void UpdateRoomUI()
    {
        if (networkManager == null) return;
        
        bool inRoom = networkManager.isInRoom;
        
        if (roomPanel != null)
        {
            roomPanel.SetActive(inRoom);
        }
        
        if (inRoom)
        {
            if (roomNameText != null)
            {
                roomNameText.text = $"Room: {networkManager.currentRoomName}";
            }
            
            if (playerCountText != null)
            {
                playerCountText.text = $"Players: {networkManager.GetPlayerCount()}/{networkManager.maxPlayersPerRoom}";
            }
            
            if (startGameButton != null)
            {
                startGameButton.interactable = networkManager.IsMasterClient() && networkManager.GetPlayerCount() >= 2;
            }
        }
    }

    private void UpdateNetworkInfo()
    {
        if (networkInfoPanel == null) return;
        
        if (pingText != null)
        {
            pingText.text = $"Ping: {PhotonNetwork.GetPing()}ms";
        }
        
        if (frameRateText != null)
        {
            frameRateText.text = $"FPS: {Mathf.RoundToInt(1f / Time.deltaTime)}";
        }
    }

    private void UpdateDebugInfo()
    {
        if (!showDebugInfo || debugText == null) return;
        
        string debug = "";
        debug += $"Network State: {networkManager?.currentState}\n";
        debug += $"Matchmaking State: {matchmakingManager?.currentState}\n";
        debug += $"Connected: {networkManager?.isConnected}\n";
        debug += $"In Room: {networkManager?.isInRoom}\n";
        debug += $"Player Count: {networkManager?.GetPlayerCount()}\n";
        debug += $"Is Master: {networkManager?.IsMasterClient()}\n";
        
        debugText.text = debug;
    }

    // Button Event Handlers
    private void OnConnectClicked()
    {
        networkManager?.ConnectToServer();
    }

    private void OnDisconnectClicked()
    {
        networkManager?.DisconnectFromServer();
    }

    private void OnFindMatchClicked()
    {
        matchmakingManager?.StartMatchmaking();
    }

    private void OnCreatePrivateMatchClicked()
    {
        if (privateMatchPanel != null)
        {
            privateMatchPanel.SetActive(true);
        }
    }

    private void OnJoinPrivateMatchClicked()
    {
        if (privateMatchPanel != null)
        {
            privateMatchPanel.SetActive(true);
        }
    }

    private void OnStopMatchmakingClicked()
    {
        matchmakingManager?.StopMatchmaking();
    }

    private void OnLeaveRoomClicked()
    {
        networkManager?.LeaveRoom();
    }

    private void OnStartGameClicked()
    {
        networkManager?.StartGame();
    }

    private void OnReadyClicked()
    {
        networkManager?.SetPlayerReady(true);
    }

    private void OnCreateRoomClicked()
    {
        if (roomNameInput != null && !string.IsNullOrEmpty(roomNameInput.text))
        {
            string password = passwordInput != null ? passwordInput.text : "";
            matchmakingManager?.CreatePrivateMatch(roomNameInput.text, password);
            privateMatchPanel.SetActive(false);
        }
    }

    private void OnJoinRoomClicked()
    {
        if (roomNameInput != null && !string.IsNullOrEmpty(roomNameInput.text))
        {
            string password = passwordInput != null ? passwordInput.text : "";
            matchmakingManager?.JoinPrivateMatch(roomNameInput.text, password);
            privateMatchPanel.SetActive(false);
        }
    }

    // Dropdown Event Handlers
    private void OnCharacterChanged(int value)
    {
        matchmakingManager?.SetPreferredCharacter(value);
    }

    private void OnSkillLevelChanged(int value)
    {
        matchmakingManager?.SetSkillLevel(value + 1);
    }

    private void OnRegionChanged(int value)
    {
        string[] regions = { "US", "EU", "Asia", "Global" };
        if (value < regions.Length)
        {
            matchmakingManager?.SetRegion(regions[value]);
        }
    }

    private void OnCrossRegionChanged(bool value)
    {
        matchmakingManager?.SetAllowCrossRegion(value);
    }

    private void OnRankedMatchChanged(bool value)
    {
        matchmakingManager?.SetRankedMatch(value);
    }

    // Network Event Handlers
    private void OnConnectedToServer()
    {
        Debug.Log("Connected to server - UI updated");
    }

    private void OnDisconnectedFromServer()
    {
        Debug.Log("Disconnected from server - UI updated");
    }

    private void OnJoinedRoom()
    {
        Debug.Log("Joined room - UI updated");
    }

    private void OnLeftRoom()
    {
        Debug.Log("Left room - UI updated");
    }

    private void OnPlayerJoined(PlayerInfo player)
    {
        Debug.Log($"Player joined: {player.playerName}");
    }

    private void OnPlayerLeft(PlayerInfo player)
    {
        Debug.Log($"Player left: {player.playerName}");
    }

    private void OnGameStart()
    {
        Debug.Log("Game started - UI updated");
    }

    // Matchmaking Event Handlers
    private void OnSearchStarted()
    {
        Debug.Log("Search started - UI updated");
    }

    private void OnSearchStopped()
    {
        Debug.Log("Search stopped - UI updated");
    }

    private void OnMatchReady()
    {
        Debug.Log("Match ready - UI updated");
    }

    private void OnMatchmakingError(string error)
    {
        Debug.LogError($"Matchmaking error: {error}");
        // Could show a popup or notification here
    }

    public void ShowPanel(string panelName)
    {
        // Hide all panels first
        if (connectionPanel != null) connectionPanel.SetActive(false);
        if (matchmakingPanel != null) matchmakingPanel.SetActive(false);
        if (roomPanel != null) roomPanel.SetActive(false);
        if (privateMatchPanel != null) privateMatchPanel.SetActive(false);
        if (networkInfoPanel != null) networkInfoPanel.SetActive(false);
        
        // Show the requested panel
        switch (panelName.ToLower())
        {
            case "connection":
                if (connectionPanel != null) connectionPanel.SetActive(true);
                break;
            case "matchmaking":
                if (matchmakingPanel != null) matchmakingPanel.SetActive(true);
                break;
            case "room":
                if (roomPanel != null) roomPanel.SetActive(true);
                break;
            case "private":
                if (privateMatchPanel != null) privateMatchPanel.SetActive(true);
                break;
            case "info":
                if (networkInfoPanel != null) networkInfoPanel.SetActive(true);
                break;
        }
    }
} 