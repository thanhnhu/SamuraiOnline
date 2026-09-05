using UnityEngine;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;

public class NetworkPlayer : MonoBehaviourPun, IPunObservable
{
    [Header("Network Settings")]
    public bool isLocalPlayer = false;
    public int playerId = -1;
    public float networkUpdateRate = 60f;
    public bool useInterpolation = true;
    public bool useExtrapolation = true;
    
    [Header("Rollback Settings")]
    public int maxRollbackFrames = 7;
    public int inputDelay = 2;
    public bool useRollbackNetcode = true;
    
    [Header("Synchronization")]
    public bool syncPosition = true;
    public bool syncRotation = true;
    public bool syncHealth = true;
    public bool syncState = true;
    public bool syncInput = true;
    
    [Header("Debug")]
    public bool showNetworkInfo = false;
    public bool logNetworkEvents = false;
    
    // Network state
    private Vector3 networkPosition;
    private Quaternion networkRotation;
    private float networkHealth;
    private CharacterState networkState;
    private PlayerInputData networkInput;
    
    // Rollback system
    private Queue<GameState> gameStateHistory = new Queue<GameState>();
    private Queue<PlayerInputData> inputHistory = new Queue<PlayerInputData>();
    private int currentFrame = 0;
    private int lastConfirmedFrame = 0;
    
    // Interpolation
    private Vector3 targetPosition;
    private Quaternion targetRotation;
    private float interpolationTime = 0f;
    private float interpolationDuration = 0.1f;
    
    // Components
    private PlayerCharacter character;
    private CharacterInputHandler inputHandler;
    private Rigidbody2D rb;
    
    // Events
    public System.Action<int> OnPlayerIdAssigned;
    public System.Action<PlayerInputData> OnInputReceived;
    public System.Action<GameState> OnStateReceived;
    public System.Action OnRollback;

    private void Awake()
    {
        character = GetComponent<PlayerCharacter>();
        inputHandler = GetComponent<CharacterInputHandler>();
        rb = GetComponent<Rigidbody2D>();
        
        // Initialize network state
        networkPosition = transform.position;
        networkRotation = transform.rotation;
        networkHealth = character != null ? character.currentHealth : 100f;
        networkState = CharacterState.Idle;
        networkInput = new PlayerInputData();
    }

    private void Start()
    {
        if (photonView.IsMine)
        {
            isLocalPlayer = true;
            playerId = photonView.Owner.ActorNumber;
            OnPlayerIdAssigned?.Invoke(playerId);
            
            if (logNetworkEvents)
                Debug.Log($"Local player initialized with ID: {playerId}");
        }
        else
        {
            // Remote player - disable local input
            if (inputHandler != null)
            {
                inputHandler.enabled = false;
            }
        }
    }

    private void Update()
    {
        if (isLocalPlayer)
        {
            HandleLocalPlayer();
        }
        else
        {
            HandleRemotePlayer();
        }
    }

    private void HandleLocalPlayer()
    {
        // Capture current input
        PlayerInputData currentInput = CaptureInput();
        
        // Store input in history
        inputHistory.Enqueue(currentInput);
        if (inputHistory.Count > maxRollbackFrames + inputDelay)
        {
            inputHistory.Dequeue();
        }
        
        // Send input to other players
        if (Time.frameCount % (int)(60f / networkUpdateRate) == 0)
        {
            photonView.RPC("RPC_SendInput", RpcTarget.Others, currentInput, currentFrame);
        }
        
        // Update current frame
        currentFrame++;
    }

    private void HandleRemotePlayer()
    {
        if (useInterpolation)
        {
            // Interpolate position and rotation
            interpolationTime += Time.deltaTime;
            float t = interpolationTime / interpolationDuration;
            
            if (t <= 1f)
            {
                transform.position = Vector3.Lerp(transform.position, targetPosition, t);
                transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, t);
            }
        }
    }

    private PlayerInputData CaptureInput()
    {
        if (inputHandler == null) return new PlayerInputData();
        
        return new PlayerInputData
        {
            frame = currentFrame,
            horizontalInput = inputHandler.GetHorizontalInput(),
            verticalInput = inputHandler.GetVerticalInput(),
            jumpInput = inputHandler.GetJumpInput(),
            lightAttackInput = inputHandler.GetLightAttackInput(),
            mediumAttackInput = inputHandler.GetMediumAttackInput(),
            heavyAttackInput = inputHandler.GetHeavyAttackInput(),
            blockInput = inputHandler.GetBlockInput(),
            specialInput = inputHandler.GetSpecialInput(),
            timestamp = Time.time
        };
    }

    public void SendGameState(GameState state)
    {
        if (!isLocalPlayer) return;
        
        // Store state in history for rollback
        gameStateHistory.Enqueue(state);
        if (gameStateHistory.Count > maxRollbackFrames)
        {
            gameStateHistory.Dequeue();
        }
        
        // Send state to other players
        photonView.RPC("RPC_SendGameState", RpcTarget.Others, state);
    }

    public void RollbackToFrame(int frame)
    {
        if (!useRollbackNetcode) return;
        
        // Find the state at the specified frame
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
            OnRollback?.Invoke();
            
            if (logNetworkEvents)
                Debug.Log($"Rolled back to frame {frame}");
        }
    }

    private void ApplyGameState(GameState state)
    {
        if (character == null) return;
        
        // Apply position and rotation
        if (syncPosition)
        {
            transform.position = state.position;
        }
        
        if (syncRotation)
        {
            transform.rotation = state.rotation;
        }
        
        // Apply character state
        if (syncHealth)
        {
            character.currentHealth = state.health;
        }
        
        if (syncState)
        {
            character.SetState(state.characterState);
        }
        
        // Apply physics
        if (rb != null)
        {
            rb.linearVelocity = state.velocity;
        }
    }

    // RPC Methods
    [PunRPC]
    private void RPC_SendInput(PlayerInputData input, int frame)
    {
        if (isLocalPlayer) return; // Don't process our own input
        
        // Store input for processing
        networkInput = input;
        OnInputReceived?.Invoke(input);
        
        if (logNetworkEvents)
            Debug.Log($"Received input from frame {frame}");
    }

    [PunRPC]
    private void RPC_SendGameState(GameState state)
    {
        if (isLocalPlayer) return; // Don't process our own state
        
        // Update network state
        networkPosition = state.position;
        networkRotation = state.rotation;
        networkHealth = state.health;
        networkState = state.characterState;
        
        // Update target for interpolation
        targetPosition = state.position;
        targetRotation = state.rotation;
        interpolationTime = 0f;
        
        OnStateReceived?.Invoke(state);
        
        if (logNetworkEvents)
            Debug.Log($"Received game state from frame {state.frame}");
    }

    [PunRPC]
    private void RPC_RequestRollback(int frame)
    {
        if (!isLocalPlayer) return;
        
        // Request rollback from the sender
        photonView.RPC("RPC_RollbackToFrame", RpcTarget.All, frame);
    }

    [PunRPC]
    private void RPC_RollbackToFrame(int frame)
    {
        RollbackToFrame(frame);
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Send data
            if (syncPosition)
                stream.SendNext(transform.position);
            
            if (syncRotation)
                stream.SendNext(transform.rotation);
            
            if (syncHealth && character != null)
                stream.SendNext(character.currentHealth);
            
            if (syncState && character != null)
                stream.SendNext(character.GetStateData().currentState);
            
            if (rb != null)
                stream.SendNext(rb.linearVelocity);
        }
        else
        {
            // Receive data
            if (syncPosition)
                networkPosition = (Vector3)stream.ReceiveNext();
            
            if (syncRotation)
                networkRotation = (Quaternion)stream.ReceiveNext();
            
            if (syncHealth)
                networkHealth = (float)stream.ReceiveNext();
            
            if (syncState)
                networkState = (CharacterState)stream.ReceiveNext();
            
            if (rb != null)
                rb.linearVelocity = (Vector2)stream.ReceiveNext();
            
            // Update target for interpolation
            targetPosition = networkPosition;
            targetRotation = networkRotation;
            interpolationTime = 0f;
        }
    }

    public void SetNetworkUpdateRate(float rate)
    {
        networkUpdateRate = rate;
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

    public void EnableInterpolation(bool enable)
    {
        useInterpolation = enable;
    }

    public void EnableExtrapolation(bool enable)
    {
        useExtrapolation = enable;
    }

    public PlayerInputData GetLastInput()
    {
        return networkInput;
    }

    public GameState GetCurrentGameState()
    {
        if (character == null) return new GameState();
        
        return new GameState
        {
            frame = currentFrame,
            position = transform.position,
            rotation = transform.rotation,
            velocity = rb != null ? rb.linearVelocity : Vector2.zero,
            health = character.currentHealth,
            characterState = character.GetStateData().currentState,
            timestamp = Time.time
        };
    }

    public float GetNetworkLatency()
    {
        // This would calculate actual network latency
        // For now, return a placeholder value
        return 0.05f; // 50ms
    }

    public int GetCurrentFrame()
    {
        return currentFrame;
    }

    public int GetLastConfirmedFrame()
    {
        return lastConfirmedFrame;
    }

    private void OnGUI()
    {
        if (!showNetworkInfo) return;
        
        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        GUILayout.Label($"Player ID: {playerId}");
        GUILayout.Label($"Is Local: {isLocalPlayer}");
        GUILayout.Label($"Current Frame: {currentFrame}");
        GUILayout.Label($"Network Health: {networkHealth:F1}");
        GUILayout.Label($"Network State: {networkState}");
        GUILayout.Label($"Input History: {inputHistory.Count}");
        GUILayout.Label($"State History: {gameStateHistory.Count}");
        GUILayout.EndArea();
    }
}

[System.Serializable]
public class PlayerInputData
{
    public int frame;
    public float horizontalInput;
    public float verticalInput;
    public bool jumpInput;
    public bool lightAttackInput;
    public bool mediumAttackInput;
    public bool heavyAttackInput;
    public bool blockInput;
    public bool specialInput;
    public float timestamp;
}

 