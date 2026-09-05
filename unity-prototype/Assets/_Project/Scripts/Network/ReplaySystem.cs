using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using Photon.Realtime;

[System.Serializable]
public class ReplayFrame
{
    public int frameNumber;
    public float timestamp;
    public Dictionary<int, PlayerInputData> playerInputs;
    public Dictionary<int, CharacterState> characterStates;
    public GameState gameState;
    public List<NetworkEvent> networkEvents;
    
    public ReplayFrame()
    {
        playerInputs = new Dictionary<int, PlayerInputData>();
        characterStates = new Dictionary<int, CharacterState>();
        networkEvents = new List<NetworkEvent>();
    }
}

[System.Serializable]
public class ReplayData
{
    public string replayId;
    public string matchId;
    public string roomName;
    public DateTime timestamp;
    public float duration;
    public int totalFrames;
    public int frameRate;
    public List<PlayerInfo> players;
    public List<ReplayFrame> frames;
    public GameSettings gameSettings;
    public string version;
    
    public ReplayData()
    {
        replayId = Guid.NewGuid().ToString();
        timestamp = DateTime.Now;
        players = new List<PlayerInfo>();
        frames = new List<ReplayFrame>();
        version = "1.0.0";
    }
}

[System.Serializable]
public class GameSettings
{
    public int maxRounds;
    public float roundTime;
    public bool rageModeEnabled;
    public string stageName;
    public bool spectatorsAllowed;
}

[System.Serializable]
public class NetworkEvent
{
    public int frameNumber;
    public string eventType;
    public string eventData;
    public float timestamp;
}

public class ReplaySystem : MonoBehaviourPunCallbacks
{
    [Header("Replay Settings")]
    public bool autoRecordMatches = true;
    public bool saveReplaysToFile = true;
    public string replaySavePath = "Replays/";
    public int maxReplayDuration = 300; // 5 minutes
    public float replayFrameRate = 60f;
    
    [Header("Replay UI")]
    public GameObject replayPanelPrefab;
    public GameObject replayListItemPrefab;
    public GameObject replayControlsPrefab;
    public Transform replayUIParent;
    
    [Header("Analysis Settings")]
    public bool enableFrameAnalysis = true;
    public bool recordNetworkEvents = true;
    public bool recordInputHistory = true;
    public bool recordCharacterStates = true;
    
    // Recording state
    private bool isRecording = false;
    private ReplayData currentReplay;
    private float recordingStartTime;
    private int currentFrame = 0;
    private float frameTimer = 0f;
    
    // Playback state
    private bool isPlaying = false;
    private ReplayData loadedReplay;
    private int playbackFrame = 0;
    private float playbackTimer = 0f;
    private float playbackSpeed = 1f;
    private bool isPaused = false;
    
    // UI references
    private GameObject replayPanel;
    private GameObject replayControls;
    private List<GameObject> replayListItems = new List<GameObject>();
    
    // Events
    public System.Action<ReplayData> OnReplayStarted;
    public System.Action<ReplayData> OnReplayFinished;
    public System.Action<ReplayData> OnReplayLoaded;
    public System.Action OnReplayStopped;
    public System.Action<int> OnFrameChanged;
    
    // Analysis data
    private Dictionary<int, List<PlayerInputData>> inputHistory = new Dictionary<int, List<PlayerInputData>>();
    private Dictionary<int, List<CharacterState>> stateHistory = new Dictionary<int, List<CharacterState>>();
    private List<NetworkEvent> networkEventHistory = new List<NetworkEvent>();
    
    private void Start()
    {
        // Create replay directory if it doesn't exist
        if (saveReplaysToFile && !Directory.Exists(replaySavePath))
        {
            Directory.CreateDirectory(replaySavePath);
        }
        
        // Subscribe to network events
        if (NetworkGameManager.Instance != null)
        {
            NetworkGameManager.Instance.OnGameStarted += StartRecording;
            NetworkGameManager.Instance.OnGameEnded += StopRecording;
        }
    }
    
    private void Update()
    {
        if (isRecording)
        {
            UpdateRecording();
        }
        
        if (isPlaying)
        {
            UpdatePlayback();
        }
    }
    
    #region Recording Methods
    
    public void StartRecording()
    {
        if (isRecording) return;
        
        currentReplay = new ReplayData();
        currentReplay.matchId = PhotonNetwork.CurrentRoom.Name;
        currentReplay.roomName = PhotonNetwork.CurrentRoom.Name;
        currentReplay.frameRate = Mathf.RoundToInt(replayFrameRate);
        
        // Record game settings
        currentReplay.gameSettings = new GameSettings();
        if (NetworkGameManager.Instance != null)
        {
            currentReplay.gameSettings.maxRounds = NetworkGameManager.Instance.maxRounds;
            currentReplay.gameSettings.roundTime = NetworkGameManager.Instance.roundTime;
            currentReplay.gameSettings.rageModeEnabled = true; // Default
            currentReplay.gameSettings.spectatorsAllowed = true; // Default
        }
        
        // Record player information
        foreach (Player player in PhotonNetwork.PlayerList)
        {
            PlayerInfo playerInfo = new PlayerInfo();
            playerInfo.actorId = player.ActorNumber;
            playerInfo.playerName = player.NickName;
            
            if (player.CustomProperties.ContainsKey("CharacterName"))
            {
                playerInfo.characterName = (string)player.CustomProperties["CharacterName"];
            }
            
            if (player.CustomProperties.ContainsKey("SkillLevel"))
            {
                playerInfo.skillLevel = (int)player.CustomProperties["SkillLevel"];
            }
            
            if (player.CustomProperties.ContainsKey("Region"))
            {
                playerInfo.region = (string)player.CustomProperties["Region"];
            }
            
            currentReplay.players.Add(playerInfo);
        }
        
        isRecording = true;
        recordingStartTime = Time.time;
        currentFrame = 0;
        frameTimer = 0f;
        
        // Initialize analysis data
        inputHistory.Clear();
        stateHistory.Clear();
        networkEventHistory.Clear();
        
        Debug.Log("Started recording replay");
    }
    
    public void StopRecording()
    {
        if (!isRecording) return;
        
        isRecording = false;
        currentReplay.duration = Time.time - recordingStartTime;
        currentReplay.totalFrames = currentFrame;
        
        // Save replay if enabled
        if (saveReplaysToFile)
        {
            SaveReplay(currentReplay);
        }
        
        OnReplayFinished?.Invoke(currentReplay);
        
        Debug.Log($"Stopped recording replay. Duration: {currentReplay.duration:F2}s, Frames: {currentFrame}");
    }
    
    private void UpdateRecording()
    {
        frameTimer += Time.deltaTime;
        
        if (frameTimer >= 1f / replayFrameRate)
        {
            frameTimer -= 1f / replayFrameRate;
            currentFrame++;
            
            // Record frame data
            ReplayFrame frame = new ReplayFrame();
            frame.frameNumber = currentFrame;
            frame.timestamp = Time.time - recordingStartTime;
            frame.gameState = GetCurrentGameState();
            
            // Record player inputs
            if (recordInputHistory)
            {
                foreach (Player player in PhotonNetwork.PlayerList)
                {
                    if (NetworkInput.Instance != null)
                    {
                        PlayerInputData input = NetworkInput.Instance.GetInputForFrame(currentFrame);
                        if (input != null)
                        {
                            frame.playerInputs[player.ActorNumber] = input;
                            
                            // Store in history
                            if (!inputHistory.ContainsKey(player.ActorNumber))
                            {
                                inputHistory[player.ActorNumber] = new List<PlayerInputData>();
                            }
                            inputHistory[player.ActorNumber].Add(input);
                        }
                    }
                }
            }
            
            // Record character states
            if (recordCharacterStates)
            {
                var networkPlayers = FindObjectsOfType<NetworkPlayer>();
                foreach (var networkPlayer in networkPlayers)
                {
                    if (networkPlayer.photonView != null)
                    {
                        BaseCharacter character = networkPlayer.GetComponent<BaseCharacter>();
                        if (character != null)
                        {
                            CharacterState state = character.GetCurrentState();
                            frame.characterStates[networkPlayer.photonView.Owner.ActorNumber] = state;
                            
                            // Store in history
                            if (!stateHistory.ContainsKey(networkPlayer.photonView.Owner.ActorNumber))
                            {
                                stateHistory[networkPlayer.photonView.Owner.ActorNumber] = new List<CharacterState>();
                            }
                            stateHistory[networkPlayer.photonView.Owner.ActorNumber].Add(state);
                        }
                    }
                }
            }
            
            // Record network events
            if (recordNetworkEvents && networkEventHistory.Count > 0)
            {
                frame.networkEvents.AddRange(networkEventHistory);
                networkEventHistory.Clear();
            }
            
            currentReplay.frames.Add(frame);
            
            // Check if we've exceeded max duration
            if (currentReplay.duration > maxReplayDuration)
            {
                StopRecording();
            }
        }
    }
    
    private GameState GetCurrentGameState()
    {
        GameState state = new GameState();
        
        if (NetworkGameManager.Instance != null)
        {
            state.currentRound = NetworkGameManager.Instance.currentRound;
            state.roundTime = NetworkGameManager.Instance.roundTime;
            state.gamePhase = NetworkGameManager.Instance.currentPhase;
        }
        
        return state;
    }
    
    public void RecordNetworkEvent(string eventType, string eventData)
    {
        if (!isRecording || !recordNetworkEvents) return;
        
        NetworkEvent networkEvent = new NetworkEvent();
        networkEvent.frameNumber = currentFrame;
        networkEvent.eventType = eventType;
        networkEvent.eventData = eventData;
        networkEvent.timestamp = Time.time - recordingStartTime;
        
        networkEventHistory.Add(networkEvent);
    }
    
    #endregion
    
    #region Playback Methods
    
    public void LoadReplay(string replayId)
    {
        string filePath = Path.Combine(replaySavePath, $"{replayId}.json");
        
        if (File.Exists(filePath))
        {
            try
            {
                string json = File.ReadAllText(filePath);
                loadedReplay = JsonUtility.FromJson<ReplayData>(json);
                OnReplayLoaded?.Invoke(loadedReplay);
                
                Debug.Log($"Loaded replay: {loadedReplay.replayId}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load replay: {e.Message}");
            }
        }
        else
        {
            Debug.LogError($"Replay file not found: {filePath}");
        }
    }
    
    public void PlayReplay()
    {
        if (loadedReplay == null) return;
        
        isPlaying = true;
        isPaused = false;
        playbackFrame = 0;
        playbackTimer = 0f;
        playbackSpeed = 1f;
        
        // Restore game state to first frame
        RestoreFrame(0);
        
        OnReplayStarted?.Invoke(loadedReplay);
        
        Debug.Log($"Started playing replay: {loadedReplay.replayId}");
    }
    
    public void PauseReplay()
    {
        isPaused = true;
    }
    
    public void ResumeReplay()
    {
        isPaused = false;
    }
    
    public void StopReplay()
    {
        isPlaying = false;
        isPaused = false;
        loadedReplay = null;
        
        OnReplayStopped?.Invoke();
        
        Debug.Log("Stopped replay playback");
    }
    
    public void SetPlaybackSpeed(float speed)
    {
        playbackSpeed = Mathf.Clamp(speed, 0.1f, 4f);
    }
    
    public void SeekToFrame(int frame)
    {
        if (loadedReplay == null) return;
        
        frame = Mathf.Clamp(frame, 0, loadedReplay.frames.Count - 1);
        playbackFrame = frame;
        playbackTimer = frame / replayFrameRate;
        
        RestoreFrame(frame);
        OnFrameChanged?.Invoke(frame);
    }
    
    public void SeekToTime(float time)
    {
        if (loadedReplay == null) return;
        
        int frame = Mathf.RoundToInt(time * replayFrameRate);
        SeekToFrame(frame);
    }
    
    private void UpdatePlayback()
    {
        if (isPaused || loadedReplay == null) return;
        
        playbackTimer += Time.deltaTime * playbackSpeed;
        
        int targetFrame = Mathf.RoundToInt(playbackTimer * replayFrameRate);
        
        if (targetFrame != playbackFrame && targetFrame < loadedReplay.frames.Count)
        {
            playbackFrame = targetFrame;
            RestoreFrame(playbackFrame);
            OnFrameChanged?.Invoke(playbackFrame);
        }
        
        // Check if replay is finished
        if (playbackFrame >= loadedReplay.frames.Count - 1)
        {
            StopReplay();
        }
    }
    
    private void RestoreFrame(int frameIndex)
    {
        if (loadedReplay == null || frameIndex >= loadedReplay.frames.Count) return;
        
        ReplayFrame frame = loadedReplay.frames[frameIndex];
        
        // Restore character states
        foreach (var kvp in frame.characterStates)
        {
            var networkPlayers = FindObjectsOfType<NetworkPlayer>();
            foreach (var networkPlayer in networkPlayers)
            {
                if (networkPlayer.photonView != null && networkPlayer.photonView.Owner.ActorNumber == kvp.Key)
                {
                    BaseCharacter character = networkPlayer.GetComponent<BaseCharacter>();
                    if (character != null)
                    {
                        character.RestoreState(kvp.Value);
                        break;
                    }
                }
            }
        }
        
        // Restore game state
        if (NetworkGameManager.Instance != null)
        {
            NetworkGameManager.Instance.RestoreGameState(frame.gameState);
        }
    }
    
    #endregion
    
    #region File Operations
    
    public void SaveReplay(ReplayData replay)
    {
        if (!saveReplaysToFile) return;
        
        try
        {
            string json = JsonUtility.ToJson(replay, true);
            string filePath = Path.Combine(replaySavePath, $"{replay.replayId}.json");
            File.WriteAllText(filePath, json);
            
            Debug.Log($"Saved replay to: {filePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to save replay: {e.Message}");
        }
    }
    
    public List<ReplayData> LoadAllReplays()
    {
        List<ReplayData> replays = new List<ReplayData>();
        
        if (!Directory.Exists(replaySavePath)) return replays;
        
        string[] files = Directory.GetFiles(replaySavePath, "*.json");
        
        foreach (string file in files)
        {
            try
            {
                string json = File.ReadAllText(file);
                ReplayData replay = JsonUtility.FromJson<ReplayData>(json);
                replays.Add(replay);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load replay from {file}: {e.Message}");
            }
        }
        
        // Sort by timestamp (newest first)
        replays.Sort((a, b) => b.timestamp.CompareTo(a.timestamp));
        
        return replays;
    }
    
    public void DeleteReplay(string replayId)
    {
        string filePath = Path.Combine(replaySavePath, $"{replayId}.json");
        
        if (File.Exists(filePath))
        {
            try
            {
                File.Delete(filePath);
                Debug.Log($"Deleted replay: {replayId}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to delete replay: {e.Message}");
            }
        }
    }
    
    #endregion
    
    #region Analysis Methods
    
    public ReplayAnalysis AnalyzeReplay(ReplayData replay)
    {
        ReplayAnalysis analysis = new ReplayAnalysis();
        
        if (replay == null || replay.frames.Count == 0) return analysis;
        
        // Basic statistics
        analysis.totalFrames = replay.frames.Count;
        analysis.duration = replay.duration;
        analysis.averageFrameRate = replay.frames.Count / replay.duration;
        
        // Player statistics
        foreach (PlayerInfo player in replay.players)
        {
            PlayerStats playerStats = new PlayerStats();
            playerStats.playerId = player.actorId;
            playerStats.playerName = player.playerName;
            
            // Count inputs
            int totalInputs = 0;
            int attackInputs = 0;
            int movementInputs = 0;
            
            foreach (ReplayFrame frame in replay.frames)
            {
                if (frame.playerInputs.ContainsKey(player.actorId))
                {
                    PlayerInputData input = frame.playerInputs[player.actorId];
                    totalInputs++;
                    
                    if (input.lightAttackInput || input.mediumAttackInput || input.heavyAttackInput || input.specialInput)
                    {
                        attackInputs++;
                    }
                    
                    if (Mathf.Abs(input.horizontalInput) > 0.1f || Mathf.Abs(input.verticalInput) > 0.1f || input.jumpInput)
                    {
                        movementInputs++;
                    }
                }
            }
            
            playerStats.totalInputs = totalInputs;
            playerStats.attackInputs = attackInputs;
            playerStats.movementInputs = movementInputs;
            playerStats.inputsPerSecond = totalInputs / replay.duration;
            
            analysis.playerStats.Add(playerStats);
        }
        
        // Network analysis
        int totalNetworkEvents = 0;
        foreach (ReplayFrame frame in replay.frames)
        {
            totalNetworkEvents += frame.networkEvents.Count;
        }
        
        analysis.totalNetworkEvents = totalNetworkEvents;
        analysis.networkEventsPerSecond = totalNetworkEvents / replay.duration;
        
        return analysis;
    }
    
    public List<ReplayFrame> GetFramesInTimeRange(float startTime, float endTime)
    {
        List<ReplayFrame> frames = new List<ReplayFrame>();
        
        if (loadedReplay == null) return frames;
        
        foreach (ReplayFrame frame in loadedReplay.frames)
        {
            if (frame.timestamp >= startTime && frame.timestamp <= endTime)
            {
                frames.Add(frame);
            }
        }
        
        return frames;
    }
    
    public PlayerInputData GetPlayerInputAtTime(int playerId, float time)
    {
        if (loadedReplay == null) return null;
        
        int frameIndex = Mathf.RoundToInt(time * replayFrameRate);
        
        if (frameIndex >= 0 && frameIndex < loadedReplay.frames.Count)
        {
            ReplayFrame frame = loadedReplay.frames[frameIndex];
            if (frame.playerInputs.ContainsKey(playerId))
            {
                return frame.playerInputs[playerId];
            }
        }
        
        return null;
    }
    
    #endregion
    
    #region UI Methods
    
    public void ShowReplayUI()
    {
        if (replayPanelPrefab == null || replayUIParent == null) return;
        
        replayPanel = Instantiate(replayPanelPrefab, replayUIParent);
        CreateReplayList();
    }
    
    public void HideReplayUI()
    {
        if (replayPanel != null)
        {
            Destroy(replayPanel);
            replayPanel = null;
        }
        
        if (replayControls != null)
        {
            Destroy(replayControls);
            replayControls = null;
        }
    }
    
    private void CreateReplayList()
    {
        if (replayPanel == null || replayListItemPrefab == null) return;
        
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
        List<ReplayData> replays = LoadAllReplays();
        
        // Create list items
        Transform listParent = replayPanel.transform.Find("ReplayList");
        if (listParent != null)
        {
            foreach (ReplayData replay in replays)
            {
                GameObject listItem = Instantiate(replayListItemPrefab, listParent);
                var textComponent = listItem.GetComponentInChildren<TextMeshProUGUI>();
                if (textComponent != null)
                {
                    textComponent.text = $"{replay.players.Count} Players - {replay.duration:F1}s - {replay.timestamp:MM/dd/yyyy HH:mm}";
                }
                
                // Add click handler
                var button = listItem.GetComponent<Button>();
                if (button != null)
                {
                    string replayId = replay.replayId; // Capture for lambda
                    button.onClick.AddListener(() => LoadReplay(replayId));
                }
                
                replayListItems.Add(listItem);
            }
        }
    }
    
    public void ShowReplayControls()
    {
        if (replayControlsPrefab == null || replayUIParent == null) return;
        
        replayControls = Instantiate(replayControlsPrefab, replayUIParent);
    }
    
    #endregion
    
    #region Public Properties
    
    public bool IsRecording => isRecording;
    public bool IsPlaying => isPlaying;
    public bool IsPaused => isPaused;
    public ReplayData CurrentReplay => currentReplay;
    public ReplayData LoadedReplay => loadedReplay;
    public int CurrentFrame => isPlaying ? playbackFrame : currentFrame;
    public float PlaybackSpeed => playbackSpeed;
    public float PlaybackProgress => loadedReplay != null ? (float)playbackFrame / loadedReplay.frames.Count : 0f;
    
    #endregion
}

[System.Serializable]
public class ReplayAnalysis
{
    public int totalFrames;
    public float duration;
    public float averageFrameRate;
    public List<PlayerStats> playerStats;
    public int totalNetworkEvents;
    public float networkEventsPerSecond;
    
    public ReplayAnalysis()
    {
        playerStats = new List<PlayerStats>();
    }
}

[System.Serializable]
public class PlayerStats
{
    public int playerId;
    public string playerName;
    public int totalInputs;
    public int attackInputs;
    public int movementInputs;
    public float inputsPerSecond;
} 