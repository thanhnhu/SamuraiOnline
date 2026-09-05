using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;

public class ReplayUI : MonoBehaviour
{
    [Header("UI References")]
    public GameObject replayPanel;
    public GameObject replayListPanel;
    public GameObject replayPlaybackPanel;
    public GameObject replayAnalysisPanel;
    
    [Header("Replay List UI")]
    public Transform replayListContent;
    public GameObject replayListItemPrefab;
    public Button refreshButton;
    public Button createReplayButton;
    public TMP_InputField searchInput;
    
    [Header("Playback Controls")]
    public Button playButton;
    public Button pauseButton;
    public Button stopButton;
    public Button stepForwardButton;
    public Button stepBackwardButton;
    public Slider timelineSlider;
    public TMP_Text currentTimeText;
    public TMP_Text totalTimeText;
    public TMP_Text currentFrameText;
    public TMP_Text totalFramesText;
    
    [Header("Playback Speed")]
    public Slider speedSlider;
    public TMP_Text speedText;
    public Button speed0_25xButton;
    public Button speed0_5xButton;
    public Button speed1xButton;
    public Button speed2xButton;
    public Button speed4xButton;
    
    [Header("Camera Controls")]
    public Button cameraFollowP1Button;
    public Button cameraFollowP2Button;
    public Button cameraFreeButton;
    public Button cameraZoomInButton;
    public Button cameraZoomOutButton;
    public Slider cameraZoomSlider;
    
    [Header("Analysis Tools")]
    public Button showInputsButton;
    public Button showHitboxesButton;
    public Button showFrameDataButton;
    public Button exportAnalysisButton;
    public Toggle frameAdvanceToggle;
    public Toggle slowMotionToggle;
    
    [Header("Replay Info")]
    public TMP_Text replayTitleText;
    public TMP_Text replayDateText;
    public TMP_Text replayDurationText;
    public TMP_Text replayPlayersText;
    public TMP_Text replaySettingsText;
    
    [Header("Player Info")]
    public Transform playerInfoContent;
    public GameObject playerInfoPrefab;
    
    [Header("Network Info")]
    public TMP_Text pingText;
    public TMP_Text latencyText;
    public TMP_Text packetLossText;
    public TMP_Text frameRateText;
    
    // Private variables
    private ReplaySystem replaySystem;
    private List<GameObject> replayListItems = new List<GameObject>();
    private List<GameObject> playerInfoItems = new List<GameObject>();
    private bool isTimelineDragging = false;
    private float lastUpdateTime = 0f;
    
    private void Start()
    {
        replaySystem = FindObjectOfType<ReplaySystem>();
        if (replaySystem == null)
        {
            Debug.LogError("ReplaySystem not found in scene!");
            return;
        }
        
        SetupUI();
        SetupEventListeners();
        
        // Subscribe to replay system events
        replaySystem.OnReplayLoaded += OnReplayLoaded;
        replaySystem.OnReplayStarted += OnReplayStarted;
        replaySystem.OnReplayStopped += OnReplayStopped;
        replaySystem.OnFrameChanged += OnFrameChanged;
    }
    
    private void Update()
    {
        if (replaySystem != null && replaySystem.IsPlaying && !isTimelineDragging)
        {
            UpdateTimeline();
        }
        
        // Update network info
        UpdateNetworkInfo();
    }
    
    #region UI Setup
    
    private void SetupUI()
    {
        // Set initial states
        if (replayPlaybackPanel != null)
            replayPlaybackPanel.SetActive(false);
        
        if (replayAnalysisPanel != null)
            replayAnalysisPanel.SetActive(false);
        
        // Setup timeline slider
        if (timelineSlider != null)
        {
            timelineSlider.minValue = 0f;
            timelineSlider.maxValue = 1f;
            timelineSlider.value = 0f;
        }
        
        // Setup speed slider
        if (speedSlider != null)
        {
            speedSlider.minValue = 0.1f;
            speedSlider.maxValue = 4f;
            speedSlider.value = 1f;
        }
        
        // Setup camera zoom slider
        if (cameraZoomSlider != null)
        {
            cameraZoomSlider.minValue = 5f;
            cameraZoomSlider.maxValue = 15f;
            cameraZoomSlider.value = 10f;
        }
    }
    
    private void SetupEventListeners()
    {
        // Replay list buttons
        if (refreshButton != null)
            refreshButton.onClick.AddListener(RefreshReplayList);
        
        if (createReplayButton != null)
            createReplayButton.onClick.AddListener(CreateNewReplay);
        
        if (searchInput != null)
            searchInput.onValueChanged.AddListener(OnSearchInputChanged);
        
        // Playback control buttons
        if (playButton != null)
            playButton.onClick.AddListener(PlayReplay);
        
        if (pauseButton != null)
            pauseButton.onClick.AddListener(PauseReplay);
        
        if (stopButton != null)
            stopButton.onClick.AddListener(StopReplay);
        
        if (stepForwardButton != null)
            stepForwardButton.onClick.AddListener(StepForward);
        
        if (stepBackwardButton != null)
            stepBackwardButton.onClick.AddListener(StepBackward);
        
        // Timeline slider
        if (timelineSlider != null)
        {
            timelineSlider.onValueChanged.AddListener(OnTimelineValueChanged);
            // timelineSlider.onBeginDrag.AddListener((data) => isTimelineDragging = true);
            // timelineSlider.onEndDrag.AddListener((data) => isTimelineDragging = false);
        }
        
        // Speed control buttons
        if (speed0_25xButton != null)
            speed0_25xButton.onClick.AddListener(() => SetPlaybackSpeed(0.25f));
        
        if (speed0_5xButton != null)
            speed0_5xButton.onClick.AddListener(() => SetPlaybackSpeed(0.5f));
        
        if (speed1xButton != null)
            speed1xButton.onClick.AddListener(() => SetPlaybackSpeed(1f));
        
        if (speed2xButton != null)
            speed2xButton.onClick.AddListener(() => SetPlaybackSpeed(2f));
        
        if (speed4xButton != null)
            speed4xButton.onClick.AddListener(() => SetPlaybackSpeed(4f));
        
        if (speedSlider != null)
            speedSlider.onValueChanged.AddListener(SetPlaybackSpeed);
        
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
        
        if (cameraZoomSlider != null)
            cameraZoomSlider.onValueChanged.AddListener(SetCameraZoom);
        
        // Analysis tool buttons
        if (showInputsButton != null)
            showInputsButton.onClick.AddListener(ToggleInputDisplay);
        
        if (showHitboxesButton != null)
            showHitboxesButton.onClick.AddListener(ToggleHitboxDisplay);
        
        if (showFrameDataButton != null)
            showFrameDataButton.onClick.AddListener(ToggleFrameDataDisplay);
        
        if (exportAnalysisButton != null)
            exportAnalysisButton.onClick.AddListener(ExportAnalysis);
        
        if (frameAdvanceToggle != null)
            frameAdvanceToggle.onValueChanged.AddListener(SetFrameAdvance);
        
        if (slowMotionToggle != null)
            slowMotionToggle.onValueChanged.AddListener(SetSlowMotion);
    }
    
    #endregion
    
    #region Replay List Management
    
    public void RefreshReplayList()
    {
        if (replaySystem == null || replayListContent == null) return;
        
        // Clear existing items
        foreach (GameObject item in replayListItems)
        {
            if (item != null)
            {
                Destroy(item);
            }
        }
        replayListItems.Clear();
        
        // Load all replays
        List<ReplayData> replays = replaySystem.LoadAllReplays();
        
        // Filter by search term
        string searchTerm = searchInput != null ? searchInput.text.ToLower() : "";
        if (!string.IsNullOrEmpty(searchTerm))
        {
            replays = replays.FindAll(r => 
                r.players.Exists(p => p.playerName.ToLower().Contains(searchTerm)) ||
                r.roomName.ToLower().Contains(searchTerm)
            );
        }
        
        // Create list items
        foreach (ReplayData replay in replays)
        {
            CreateReplayListItem(replay);
        }
    }
    
    private void CreateReplayListItem(ReplayData replay)
    {
        if (replayListItemPrefab == null || replayListContent == null) return;
        
        GameObject listItem = Instantiate(replayListItemPrefab, replayListContent);
        
        // Set up text components
        var titleText = listItem.transform.Find("TitleText")?.GetComponent<TMP_Text>();
        if (titleText != null)
        {
            titleText.text = $"Match {replay.matchId}";
        }
        
        var infoText = listItem.transform.Find("InfoText")?.GetComponent<TMP_Text>();
        if (infoText != null)
        {
            string playerNames = "";
            foreach (var player in replay.players)
            {
                if (playerNames.Length > 0) playerNames += " vs ";
                playerNames += player.playerName;
            }
            infoText.text = $"{playerNames} - {replay.duration:F1}s";
        }
        
        var dateText = listItem.transform.Find("DateText")?.GetComponent<TMP_Text>();
        if (dateText != null)
        {
            dateText.text = replay.timestamp.ToString("MM/dd/yyyy HH:mm");
        }
        
        // Set up buttons
        var playButton = listItem.transform.Find("PlayButton")?.GetComponent<Button>();
        if (playButton != null)
        {
            string replayId = replay.replayId;
            playButton.onClick.AddListener(() => LoadAndPlayReplay(replayId));
        }
        
        var deleteButton = listItem.transform.Find("DeleteButton")?.GetComponent<Button>();
        if (deleteButton != null)
        {
            string replayId = replay.replayId;
            deleteButton.onClick.AddListener(() => DeleteReplay(replayId));
        }
        
        replayListItems.Add(listItem);
    }
    
    private void OnSearchInputChanged(string searchTerm)
    {
        RefreshReplayList();
    }
    
    private void CreateNewReplay()
    {
        // This could open a dialog to create a new replay from current game state
        Debug.Log("Create new replay functionality not implemented yet");
    }
    
    #endregion
    
    #region Playback Controls
    
    private void LoadAndPlayReplay(string replayId)
    {
        if (replaySystem == null) return;
        
        replaySystem.LoadReplay(replayId);
        replaySystem.PlayReplay();
        
        ShowPlaybackPanel();
    }
    
    private void PlayReplay()
    {
        if (replaySystem == null) return;
        
        if (replaySystem.IsPaused)
        {
            replaySystem.ResumeReplay();
        }
        else
        {
            replaySystem.PlayReplay();
        }
        
        UpdatePlaybackButtons();
    }
    
    private void PauseReplay()
    {
        if (replaySystem == null) return;
        
        replaySystem.PauseReplay();
        UpdatePlaybackButtons();
    }
    
    private void StopReplay()
    {
        if (replaySystem == null) return;
        
        replaySystem.StopReplay();
        ShowReplayListPanel();
    }
    
    private void StepForward()
    {
        if (replaySystem == null || replaySystem.LoadedReplay == null) return;
        
        int nextFrame = replaySystem.CurrentFrame + 1;
        if (nextFrame < replaySystem.LoadedReplay.frames.Count)
        {
            replaySystem.SeekToFrame(nextFrame);
        }
    }
    
    private void StepBackward()
    {
        if (replaySystem == null) return;
        
        int prevFrame = replaySystem.CurrentFrame - 1;
        if (prevFrame >= 0)
        {
            replaySystem.SeekToFrame(prevFrame);
        }
    }
    
    private void SetPlaybackSpeed(float speed)
    {
        if (replaySystem == null) return;
        
        replaySystem.SetPlaybackSpeed(speed);
        
        if (speedText != null)
        {
            speedText.text = $"{speed:F1}x";
        }
    }
    
    private void OnTimelineValueChanged(float value)
    {
        if (replaySystem == null || replaySystem.LoadedReplay == null) return;
        
        float targetTime = value * replaySystem.LoadedReplay.duration;
        replaySystem.SeekToTime(targetTime);
    }
    
    private void UpdateTimeline()
    {
        if (timelineSlider == null || replaySystem == null || replaySystem.LoadedReplay == null) return;
        
        float progress = replaySystem.PlaybackProgress;
        timelineSlider.value = progress;
        
        // Update time texts
        if (currentTimeText != null)
        {
            float currentTime = (float)replaySystem.CurrentFrame / replaySystem.LoadedReplay.frameRate;
            currentTimeText.text = $"{currentTime:F1}s";
        }
        
        if (totalTimeText != null)
        {
            totalTimeText.text = $"{replaySystem.LoadedReplay.duration:F1}s";
        }
        
        if (currentFrameText != null)
        {
            currentFrameText.text = replaySystem.CurrentFrame.ToString();
        }
        
        if (totalFramesText != null)
        {
            totalFramesText.text = replaySystem.LoadedReplay.frames.Count.ToString();
        }
    }
    
    private void UpdatePlaybackButtons()
    {
        if (playButton == null || pauseButton == null) return;
        
        bool isPlaying = replaySystem != null && replaySystem.IsPlaying && !replaySystem.IsPaused;
        
        playButton.gameObject.SetActive(!isPlaying);
        pauseButton.gameObject.SetActive(isPlaying);
    }
    
    #endregion
    
    #region Camera Controls
    
    private void SetCameraTarget(int playerId)
    {
        // This would interface with the SpectatorManager or camera system
        Debug.Log($"Set camera target to player {playerId}");
    }
    
    private void ZoomIn()
    {
        if (cameraZoomSlider != null)
        {
            cameraZoomSlider.value = Mathf.Max(cameraZoomSlider.value - 1f, cameraZoomSlider.minValue);
        }
    }
    
    private void ZoomOut()
    {
        if (cameraZoomSlider != null)
        {
            cameraZoomSlider.value = Mathf.Min(cameraZoomSlider.value + 1f, cameraZoomSlider.maxValue);
        }
    }
    
    private void SetCameraZoom(float zoom)
    {
        // This would interface with the camera system
        Debug.Log($"Set camera zoom to {zoom}");
    }
    
    #endregion
    
    #region Analysis Tools
    
    private void ToggleInputDisplay()
    {
        // Toggle input display overlay
        Debug.Log("Toggle input display");
    }
    
    private void ToggleHitboxDisplay()
    {
        // Toggle hitbox visualization
        Debug.Log("Toggle hitbox display");
    }
    
    private void ToggleFrameDataDisplay()
    {
        // Toggle frame data overlay
        Debug.Log("Toggle frame data display");
    }
    
    private void ExportAnalysis()
    {
        if (replaySystem == null || replaySystem.LoadedReplay == null) return;
        
        ReplayAnalysis analysis = replaySystem.AnalyzeReplay(replaySystem.LoadedReplay);
        
        // Create analysis report
        string report = CreateAnalysisReport(analysis);
        
        // Save to file
        string fileName = $"Analysis_{replaySystem.LoadedReplay.replayId}_{System.DateTime.Now:yyyyMMdd_HHmmss}.txt";
        string filePath = System.IO.Path.Combine(Application.persistentDataPath, fileName);
        
        try
        {
            System.IO.File.WriteAllText(filePath, report);
            Debug.Log($"Analysis exported to: {filePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to export analysis: {e.Message}");
        }
    }
    
    private string CreateAnalysisReport(ReplayAnalysis analysis)
    {
        System.Text.StringBuilder report = new System.Text.StringBuilder();
        
        report.AppendLine("=== REPLAY ANALYSIS REPORT ===");
        report.AppendLine($"Total Frames: {analysis.totalFrames}");
        report.AppendLine($"Duration: {analysis.duration:F2} seconds");
        report.AppendLine($"Average Frame Rate: {analysis.averageFrameRate:F1} FPS");
        report.AppendLine($"Network Events: {analysis.totalNetworkEvents} ({analysis.networkEventsPerSecond:F1}/s)");
        report.AppendLine();
        
        report.AppendLine("=== PLAYER STATISTICS ===");
        foreach (var playerStats in analysis.playerStats)
        {
            report.AppendLine($"Player: {playerStats.playerName}");
            report.AppendLine($"  Total Inputs: {playerStats.totalInputs}");
            report.AppendLine($"  Attack Inputs: {playerStats.attackInputs}");
            report.AppendLine($"  Movement Inputs: {playerStats.movementInputs}");
            report.AppendLine($"  Inputs/Second: {playerStats.inputsPerSecond:F1}");
            report.AppendLine();
        }
        
        return report.ToString();
    }
    
    private void SetFrameAdvance(bool enabled)
    {
        // Enable/disable frame advance mode
        Debug.Log($"Frame advance: {enabled}");
    }
    
    private void SetSlowMotion(bool enabled)
    {
        if (replaySystem == null) return;
        
        if (enabled)
        {
            replaySystem.SetPlaybackSpeed(0.25f);
        }
        else
        {
            replaySystem.SetPlaybackSpeed(1f);
        }
    }
    
    #endregion
    
    #region Panel Management
    
    private void ShowReplayListPanel()
    {
        if (replayListPanel != null)
            replayListPanel.SetActive(true);
        
        if (replayPlaybackPanel != null)
            replayPlaybackPanel.SetActive(false);
        
        if (replayAnalysisPanel != null)
            replayAnalysisPanel.SetActive(false);
        
        RefreshReplayList();
    }
    
    private void ShowPlaybackPanel()
    {
        if (replayListPanel != null)
            replayListPanel.SetActive(false);
        
        if (replayPlaybackPanel != null)
            replayPlaybackPanel.SetActive(true);
        
        if (replayAnalysisPanel != null)
            replayAnalysisPanel.SetActive(false);
        
        UpdateReplayInfo();
        UpdatePlayerInfo();
    }
    
    private void ShowAnalysisPanel()
    {
        if (replayListPanel != null)
            replayListPanel.SetActive(false);
        
        if (replayPlaybackPanel != null)
            replayPlaybackPanel.SetActive(false);
        
        if (replayAnalysisPanel != null)
            replayAnalysisPanel.SetActive(true);
    }
    
    #endregion
    
    #region Info Updates
    
    private void UpdateReplayInfo()
    {
        if (replaySystem == null || replaySystem.LoadedReplay == null) return;
        
        var replay = replaySystem.LoadedReplay;
        
        if (replayTitleText != null)
            replayTitleText.text = $"Match {replay.matchId}";
        
        if (replayDateText != null)
            replayDateText.text = replay.timestamp.ToString("MM/dd/yyyy HH:mm:ss");
        
        if (replayDurationText != null)
            replayDurationText.text = $"{replay.duration:F1} seconds";
        
        if (replayPlayersText != null)
            replayPlayersText.text = $"{replay.players.Count} Players";
        
        if (replaySettingsText != null)
        {
            var settings = replay.gameSettings;
            replaySettingsText.text = $"Rounds: {settings.maxRounds}, Time: {settings.roundTime}s";
        }
    }
    
    private void UpdatePlayerInfo()
    {
        if (replaySystem == null || replaySystem.LoadedReplay == null || playerInfoContent == null) return;
        
        // Clear existing items
        foreach (GameObject item in playerInfoItems)
        {
            if (item != null)
            {
                Destroy(item);
            }
        }
        playerInfoItems.Clear();
        
        // Create player info items
        foreach (var player in replaySystem.LoadedReplay.players)
        {
            if (playerInfoPrefab != null)
            {
                GameObject playerInfo = Instantiate(playerInfoPrefab, playerInfoContent);
                
                var nameText = playerInfo.transform.Find("NameText")?.GetComponent<TMP_Text>();
                if (nameText != null)
                    nameText.text = player.playerName;
                
                var characterText = playerInfo.transform.Find("CharacterText")?.GetComponent<TMP_Text>();
                if (characterText != null)
                    characterText.text = player.characterName;
                
                var skillText = playerInfo.transform.Find("SkillText")?.GetComponent<TMP_Text>();
                if (skillText != null)
                    skillText.text = $"Skill: {player.skillLevel}";
                
                playerInfoItems.Add(playerInfo);
            }
        }
    }
    
    private void UpdateNetworkInfo()
    {
        if (pingText != null)
            pingText.text = $"Ping: {PhotonNetwork.GetPing()}ms";
        
        if (latencyText != null)
            latencyText.text = $"Latency: {PhotonNetwork.NetworkClientState}";
        
        if (packetLossText != null)
            packetLossText.text = $"Packet Loss: 0%"; // Would need to calculate this
        
        if (frameRateText != null)
            frameRateText.text = $"FPS: {Mathf.RoundToInt(1f / Time.deltaTime)}";
    }
    
    #endregion
    
    #region Event Handlers
    
    private void OnReplayLoaded(ReplayData replay)
    {
        UpdateReplayInfo();
        UpdatePlayerInfo();
    }
    
    private void OnReplayStarted(ReplayData replay)
    {
        ShowPlaybackPanel();
        UpdatePlaybackButtons();
    }
    
    private void OnReplayStopped()
    {
        ShowReplayListPanel();
        UpdatePlaybackButtons();
    }
    
    private void OnFrameChanged(int frame)
    {
        UpdateTimeline();
    }
    
    private void DeleteReplay(string replayId)
    {
        if (replaySystem == null) return;
        
        // Show confirmation dialog
        if (UnityEditor.EditorUtility.DisplayDialog("Delete Replay", 
            "Are you sure you want to delete this replay?", "Delete", "Cancel"))
        {
            replaySystem.DeleteReplay(replayId);
            RefreshReplayList();
        }
    }
    
    #endregion
    
    #region Public Methods
    
    public void ShowReplayUI()
    {
        if (replayPanel != null)
            replayPanel.SetActive(true);
        
        ShowReplayListPanel();
    }
    
    public void HideReplayUI()
    {
        if (replayPanel != null)
            replayPanel.SetActive(false);
    }
    
    public void ToggleReplayUI()
    {
        if (replayPanel != null)
        {
            bool isActive = replayPanel.activeSelf;
            if (isActive)
                HideReplayUI();
            else
                ShowReplayUI();
        }
    }
    
    #endregion
} 