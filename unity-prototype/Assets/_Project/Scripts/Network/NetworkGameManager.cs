using UnityEngine;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;

public class NetworkGameManager : MonoBehaviourPun
{
    public static NetworkGameManager Instance { get; private set; }
    
    [Header("Game Settings")]
    public float gameStartDelay = 3f;
    public bool useDeterministicPhysics = true;
    public int fixedUpdateRate = 60;
    public bool pauseOnDisconnect = true;
    public int maxRounds = 3;
    public float roundTime = 99f;
    
    [Header("Rollback Settings")]
    public int maxRollbackFrames = 7;
    public int inputDelay = 2;
    public bool useRollbackNetcode = true;
    public float rollbackThreshold = 0.1f;
    
    [Header("Synchronization")]
    public bool syncGameState = true;
    public bool syncInputs = true;
    public bool syncAnimations = true;
    public float syncInterval = 0.016f; // ~60fps
    
    [Header("Debug")]
    public bool showNetworkDebug = false;
    public bool logNetworkEvents = false;
    
    // Game state
    private GameState currentGameState;
    private Queue<GameState> gameStateHistory = new Queue<GameState>();
    private Dictionary<int, PlayerInputData> playerInputs = new Dictionary<int, PlayerInputData>();
    private Dictionary<int, PlayerCharacter> networkPlayers = new Dictionary<int, PlayerCharacter>();
    
    // Timing
    private int currentFrame = 0;
    private int lastSyncedFrame = 0;
    private float lastSyncTime = 0f;
    private float gameStartTime = 0f;
    
    // Components
    private CharacterManager characterManager;
    private NetworkManager networkManager;
    
    // Events
    public System.Action OnGameStarted;
    public System.Action OnGameEnded;
    public System.Action<int> OnPlayerJoined;
    public System.Action<int> OnPlayerLeft;
    public System.Action<GameState> OnGameStateSynced;
    public System.Action OnRollbackPerformed;

    // Game progress
    public int currentRound = 1;
    public GamePhase currentPhase = GamePhase.WaitingForPlayers;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        characterManager = FindObjectOfType<CharacterManager>();
        networkManager = NetworkManager.Instance;
        
        // Set up deterministic physics
        if (useDeterministicPhysics)
        {
            Time.fixedDeltaTime = 1f / fixedUpdateRate;
            Random.InitState(42); // Fixed seed for deterministic behavior
        }
    }

    private void Start()
    {
        if (photonView.IsMine)
        {
            // Initialize game state
            currentGameState = new GameState
            {
                frame = 0,
                timestamp = Time.time
            };
            
            // Subscribe to network events
            if (networkManager != null)
            {
                networkManager.OnGameStart += OnNetworkGameStart;
                networkManager.OnGameEnd += OnNetworkGameEnd;
                networkManager.OnPlayerJoined += OnNetworkPlayerJoined;
                networkManager.OnPlayerLeft += OnNetworkPlayerLeft;
            }
        }
    }

    private void Update()
    {
        if (!photonView.IsMine) return;
        
        // Update frame counter
        currentFrame++;
        
        // Sync game state periodically
        if (Time.time - lastSyncTime > syncInterval)
        {
            SyncGameState();
            lastSyncTime = Time.time;
        }
    }

    private void FixedUpdate()
    {
        if (!photonView.IsMine) return;
        
        // Update game state
        UpdateGameState();
        
        // Check for rollback conditions
        if (useRollbackNetcode)
        {
            CheckForRollback();
        }
    }

    private void UpdateGameState()
    {
        // Capture current game state
        currentGameState = CaptureCurrentGameState();
        currentGameState.frame = currentFrame;
        currentGameState.timestamp = Time.time;
        
        // Store in history for rollback
        gameStateHistory.Enqueue(currentGameState);
        if (gameStateHistory.Count > maxRollbackFrames)
        {
            gameStateHistory.Dequeue();
        }
        
        // Apply inputs to characters
        ApplyInputsToCharacters();
    }

    private GameState CaptureCurrentGameState()
    {
        GameState state = new GameState
        {
            frame = currentFrame,
            timestamp = Time.time,
            currentRound = currentRound,
            roundTime = roundTime,
            currentTime = Time.time - gameStartTime,
            isPaused = Time.timeScale == 0f,
            isGameOver = currentPhase == GamePhase.MatchEnd,
            gamePhase = currentPhase
        };
        
        // Capture character states
        if (characterManager != null)
        {
            PlayerCharacter p1 = characterManager.GetPlayer1();
            PlayerCharacter p2 = characterManager.GetPlayer2();
            
            if (p1 != null)
            {
                state.player1State = new CharacterNetworkState
                {
                    position = p1.transform.position,
                    rotation = p1.transform.rotation,
                    velocity = p1.GetComponent<Rigidbody2D>()?.linearVelocity ?? Vector2.zero,
                    health = p1.currentHealth,
                    characterState = p1.GetStateData().currentState,
                    specialMeter = p1.specialMeter,
                    rageMeter = p1.rageMeter
                };
            }
            
            if (p2 != null)
            {
                state.player2State = new CharacterNetworkState
                {
                    position = p2.transform.position,
                    rotation = p2.transform.rotation,
                    velocity = p2.GetComponent<Rigidbody2D>()?.linearVelocity ?? Vector2.zero,
                    health = p2.currentHealth,
                    characterState = p2.GetStateData().currentState,
                    specialMeter = p2.specialMeter,
                    rageMeter = p2.rageMeter
                };
            }
        }
        
        return state;
    }

    private void ApplyInputsToCharacters()
    {
        // Apply inputs with delay for rollback netcode
        int targetFrame = currentFrame - inputDelay;
        
        foreach (var kvp in playerInputs)
        {
            int playerId = kvp.Key;
            PlayerInputData input = kvp.Value;
            
            if (input.frame == targetFrame)
            {
                PlayerCharacter character = GetPlayerById(playerId);
                if (character != null)
                {
                    ApplyInputToCharacter(character, input);
                }
            }
        }
    }

    private void ApplyInputToCharacter(PlayerCharacter character, PlayerInputData input)
    {
        // Apply movement input
        CharacterInputHandler inputHandler = character.GetComponent<CharacterInputHandler>();
        if (inputHandler != null)
        {
            // This would normally be done through the input handler
            // For network synchronization, we apply it directly
            character.HorizontalInput = input.horizontalInput;
            character.VerticalInput = input.verticalInput;
            
            if (input.jumpInput)
            {
                character.Jump();
            }
            
            if (input.lightAttackInput)
            {
                character.PerformAttack(AttackType.Light);
            }
            else if (input.mediumAttackInput)
            {
                character.PerformAttack(AttackType.Medium);
            }
            else if (input.heavyAttackInput)
            {
                character.PerformAttack(AttackType.Heavy);
            }
            
            if (input.blockInput)
            {
                character.StartBlocking();
            }
            else
            {
                character.StopBlocking();
            }
            
            if (input.specialInput)
            {
                character.UseSpecialAttack();
            }
        }
    }

    private void CheckForRollback()
    {
        // Check if we need to rollback due to input inconsistencies
        foreach (var kvp in playerInputs)
        {
            PlayerInputData input = kvp.Value;
            
            // If we're missing inputs for recent frames, request rollback
            if (currentFrame - input.frame > maxRollbackFrames)
            {
                RequestRollback(input.frame);
                break;
            }
        }
    }

    private void RequestRollback(int frame)
    {
        if (!useRollbackNetcode) return;
        
        Debug.Log($"Requesting rollback to frame {frame}");
        photonView.RPC("RPC_RequestRollback", RpcTarget.All, frame);
    }

    private void PerformRollback(int frame)
    {
        if (!useRollbackNetcode) return;
        
        // Find the game state at the specified frame
        GameState targetState = null;
        foreach (var state in gameStateHistory)
        {
            if (state.frame == frame)
            {
                targetState = state;
                break;
            }
        }
        
        if (targetState != null)
        {
            // Apply the state
            ApplyGameState(targetState);
            OnRollbackPerformed?.Invoke();
            
            if (logNetworkEvents)
            {
                Debug.Log($"Rolled back to frame {frame}");
            }
        }
    }

    private void ApplyGameState(GameState state)
    {
        if (characterManager == null) return;
        
        // Apply player 1 state
        if (state.player1State != null)
        {
            PlayerCharacter p1 = characterManager.GetPlayer1();
            if (p1 != null)
            {
                ApplyCharacterState(p1, state.player1State);
            }
        }
        
        // Apply player 2 state
        if (state.player2State != null)
        {
            PlayerCharacter p2 = characterManager.GetPlayer2();
            if (p2 != null)
            {
                ApplyCharacterState(p2, state.player2State);
            }
        }
    }

    private void ApplyCharacterState(PlayerCharacter character, CharacterNetworkState state)
    {
        // Apply position and rotation
        character.transform.position = state.position;
        character.transform.rotation = state.rotation;
        
        // Apply physics
        Rigidbody2D rb = character.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = state.velocity;
        }
        
        // Apply character state
        character.currentHealth = state.health;
        character.specialMeter = state.specialMeter;
        character.rageMeter = state.rageMeter;
        character.SetState(state.characterState);
    }

    private void SyncGameState()
    {
        if (!syncGameState) return;
        
        // Send current game state to other players
        photonView.RPC("RPC_SyncGameState", RpcTarget.Others, currentGameState);
        lastSyncedFrame = currentFrame;
    }

    public void ReceivePlayerInput(int playerId, PlayerInputData input)
    {
        // Store the input
        playerInputs[playerId] = input;
        
        if (logNetworkEvents)
        {
            Debug.Log($"Received input from player {playerId} for frame {input.frame}");
        }
    }

    private PlayerCharacter GetPlayerById(int playerId)
    {
        if (characterManager == null) return null;
        
        // This would need to be implemented based on how players are identified
        // For now, assume player 1 is local and player 2 is remote
        if (playerId == PhotonNetwork.LocalPlayer.ActorNumber)
        {
            return characterManager.GetPlayer1();
        }
        else
        {
            return characterManager.GetPlayer2();
        }
    }

    // Network Event Handlers
    private void OnNetworkGameStart()
    {
        gameStartTime = Time.time;
        OnGameStarted?.Invoke();
        
        if (logNetworkEvents)
        {
            Debug.Log("Network game started");
        }
    }

    private void OnNetworkGameEnd()
    {
        OnGameEnded?.Invoke();
        
        if (logNetworkEvents)
        {
            Debug.Log("Network game ended");
        }
    }

    private void OnNetworkPlayerJoined(PlayerInfo player)
    {
        OnPlayerJoined?.Invoke(player.actorNumber);
        
        if (logNetworkEvents)
        {
            Debug.Log($"Player {player.playerName} joined the game");
        }
    }

    private void OnNetworkPlayerLeft(PlayerInfo player)
    {
        OnPlayerLeft?.Invoke(player.actorNumber);
        
        if (logNetworkEvents)
        {
            Debug.Log($"Player {player.playerName} left the game");
        }
    }

    // RPC Methods
    [PunRPC]
    private void RPC_SyncGameState(GameState state)
    {
        if (photonView.IsMine) return; // Don't process our own state
        
        // Apply the received game state
        ApplyGameState(state);
        OnGameStateSynced?.Invoke(state);
        
        if (logNetworkEvents)
        {
            Debug.Log($"Received game state for frame {state.frame}");
        }
    }

    [PunRPC]
    private void RPC_RequestRollback(int frame)
    {
        if (photonView.IsMine) return; // Don't process our own rollback request
        
        PerformRollback(frame);
    }

    [PunRPC]
    private void RPC_PlayerInput(int playerId, PlayerInputData input)
    {
        if (photonView.IsMine) return; // Don't process our own input
        
        ReceivePlayerInput(playerId, input);
    }

    public void SendPlayerInput(PlayerInputData input)
    {
        if (!photonView.IsMine) return;
        
        // Send input to other players
        photonView.RPC("RPC_PlayerInput", RpcTarget.Others, PhotonNetwork.LocalPlayer.ActorNumber, input);
        
        // Store locally
        ReceivePlayerInput(PhotonNetwork.LocalPlayer.ActorNumber, input);
    }

    public GameState GetCurrentGameState()
    {
        return currentGameState;
    }

    public int GetCurrentFrame()
    {
        return currentFrame;
    }

    public float GetGameTime()
    {
        return Time.time - gameStartTime;
    }

    public void SetRollbackSettings(int maxFrames, int delay)
    {
        maxRollbackFrames = maxFrames;
        inputDelay = delay;
    }

    public void EnableRollbackNetcode(bool enable)
    {
        useRollbackNetcode = enable;
    }

    public void SetSyncInterval(float interval)
    {
        syncInterval = interval;
    }

    public void RestoreGameState(GameState state)
    {
        if (state == null) return;
        
        // Restore game progress
        currentRound = state.currentRound;
        currentPhase = state.gamePhase;
        roundTime = state.roundTime;
        
        // Restore game state
        if (state.isPaused)
        {
            Time.timeScale = 0f;
        }
        else
        {
            Time.timeScale = 1f;
        }
        
        // Restore character states
        if (characterManager != null)
        {
            PlayerCharacter p1 = characterManager.GetPlayer1();
            PlayerCharacter p2 = characterManager.GetPlayer2();
            
            if (p1 != null && state.player1State != null)
            {
                ApplyCharacterState(p1, state.player1State);
            }
            
            if (p2 != null && state.player2State != null)
            {
                ApplyCharacterState(p2, state.player2State);
            }
        }
        
        // Update current frame
        currentFrame = state.frame;
        
        if (logNetworkEvents)
        {
            Debug.Log($"Restored game state to frame {state.frame}");
        }
    }

    private void OnGUI()
    {
        if (!showNetworkDebug) return;
        
        GUILayout.BeginArea(new Rect(10, 430, 300, 200));
        GUILayout.Label($"Current Frame: {currentFrame}");
        GUILayout.Label($"Last Synced: {lastSyncedFrame}");
        GUILayout.Label($"Game Time: {GetGameTime():F1}s");
        GUILayout.Label($"Player Inputs: {playerInputs.Count}");
        GUILayout.Label($"State History: {gameStateHistory.Count}");
        GUILayout.Label($"Rollback Enabled: {useRollbackNetcode}");
        GUILayout.EndArea();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}

[System.Serializable]
public class CharacterNetworkState
{
    public Vector3 position;
    public Quaternion rotation;
    public Vector2 velocity;
    public float health;
    public CharacterState characterState;
    public float specialMeter;
    public float rageMeter;
}

[System.Serializable]
public class GameState
{
    public int frame;
    public Vector3 position;
    public Quaternion rotation;
    public Vector2 velocity;
    public float health;
    public CharacterState characterState;
    public float timestamp;
    
    // Additional properties for replay system
    public int currentRound;
    public float roundTime;
    public float currentTime;
    public bool isPaused;
    public bool isGameOver;
    public GamePhase gamePhase;
    
    // Character states for network synchronization
    public CharacterNetworkState player1State;
    public CharacterNetworkState player2State;
}

public enum GamePhase
{
    WaitingForPlayers,
    RoundStart,
    RoundInProgress,
    RoundEnd,
    MatchEnd
} 