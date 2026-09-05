using UnityEngine;
using System.Collections.Generic;
using Photon.Pun;

public class NetworkInput : MonoBehaviourPun
{
    public static NetworkInput Instance { get; private set; }
    
    [Header("Input Settings")]
    public int inputBufferSize = 8;
    public float inputPredictionTime = 0.1f;
    public bool useInputPrediction = true;
    public bool useInputSmoothing = true;
    
    [Header("Rollback Settings")]
    public int maxRollbackFrames = 7;
    public int inputDelay = 2;
    public bool useRollbackNetcode = true;
    
    [Header("Debug")]
    public bool showInputDebug = false;
    public bool logInputEvents = false;
    
    // Input buffers
    private Queue<PlayerInputData> inputBuffer = new Queue<PlayerInputData>();
    private Queue<PlayerInputData> predictedInputs = new Queue<PlayerInputData>();
    private Dictionary<int, PlayerInputData> confirmedInputs = new Dictionary<int, PlayerInputData>();
    
    // Timing
    private int currentFrame = 0;
    private int lastConfirmedFrame = 0;
    private float lastInputTime = 0f;
    
    // Components
    private CharacterInputHandler inputHandler;
    private NetworkPlayer networkPlayer;
    
    // Events
    public System.Action<PlayerInputData> OnInputConfirmed;
    public System.Action<PlayerInputData> OnInputPredicted;
    public System.Action<int> OnRollbackRequested;

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
        
        inputHandler = GetComponent<CharacterInputHandler>();
        networkPlayer = GetComponent<NetworkPlayer>();
    }
    
    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Start()
    {
        if (photonView.IsMine)
        {
            // Initialize input buffer
            for (int i = 0; i < inputBufferSize; i++)
            {
                inputBuffer.Enqueue(new PlayerInputData());
            }
        }
    }

    private void Update()
    {
        if (!photonView.IsMine) return;
        
        // Capture and process input
        PlayerInputData currentInput = CaptureCurrentInput();
        ProcessInput(currentInput);
        
        // Update frame counter
        currentFrame++;
    }

    private PlayerInputData CaptureCurrentInput()
    {
        if (inputHandler == null) return new PlayerInputData();
        
        PlayerInputData input = new PlayerInputData
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
        
        return input;
    }

    private void ProcessInput(PlayerInputData input)
    {
        // Add to input buffer
        inputBuffer.Enqueue(input);
        if (inputBuffer.Count > inputBufferSize)
        {
            inputBuffer.Dequeue();
        }
        
        // Store as confirmed input
        confirmedInputs[currentFrame] = input;
        
        // Send to network
        if (networkPlayer != null)
        {
            networkPlayer.SendGameState(GetCurrentGameState());
        }
        
        // Predict future inputs if enabled
        if (useInputPrediction)
        {
            PredictFutureInputs(input);
        }
        
        if (logInputEvents)
        {
            Debug.Log($"Input captured at frame {currentFrame}: H={input.horizontalInput:F2}, V={input.verticalInput:F2}, J={input.jumpInput}, A={input.lightAttackInput}");
        }
    }

    private void PredictFutureInputs(PlayerInputData currentInput)
    {
        // Clear old predictions
        predictedInputs.Clear();
        
        // Predict next few frames based on current input
        for (int i = 1; i <= maxRollbackFrames; i++)
        {
            PlayerInputData predictedInput = new PlayerInputData
            {
                frame = currentFrame + i,
                horizontalInput = currentInput.horizontalInput,
                verticalInput = currentInput.verticalInput,
                jumpInput = false, // Don't predict jump inputs
                lightAttackInput = false, // Don't predict attack inputs
                mediumAttackInput = false,
                heavyAttackInput = false,
                blockInput = currentInput.blockInput, // Keep blocking state
                specialInput = false,
                timestamp = currentInput.timestamp + (i * Time.fixedDeltaTime)
            };
            
            predictedInputs.Enqueue(predictedInput);
        }
    }

    public PlayerInputData GetInputForFrame(int frame)
    {
        // First check confirmed inputs
        if (confirmedInputs.ContainsKey(frame))
        {
            return confirmedInputs[frame];
        }
        
        // Then check predicted inputs
        foreach (var predictedInput in predictedInputs)
        {
            if (predictedInput.frame == frame)
            {
                return predictedInput;
            }
        }
        
        // Return default input if not found
        return new PlayerInputData { frame = frame };
    }

    public void ConfirmInput(int frame, PlayerInputData input)
    {
        if (confirmedInputs.ContainsKey(frame))
        {
            // Check if the confirmed input matches our prediction
            PlayerInputData ourInput = confirmedInputs[frame];
            if (!InputsMatch(ourInput, input))
            {
                // Input mismatch - request rollback
                RequestRollback(frame);
            }
        }
        
        // Store the confirmed input
        confirmedInputs[frame] = input;
        lastConfirmedFrame = frame;
        
        OnInputConfirmed?.Invoke(input);
        
        if (logInputEvents)
        {
            Debug.Log($"Input confirmed for frame {frame}");
        }
    }

    private bool InputsMatch(PlayerInputData input1, PlayerInputData input2)
    {
        return Mathf.Abs(input1.horizontalInput - input2.horizontalInput) < 0.1f &&
               Mathf.Abs(input1.verticalInput - input2.verticalInput) < 0.1f &&
               input1.jumpInput == input2.jumpInput &&
               input1.lightAttackInput == input2.lightAttackInput &&
               input1.mediumAttackInput == input2.mediumAttackInput &&
               input1.heavyAttackInput == input2.heavyAttackInput &&
               input1.blockInput == input2.blockInput &&
               input1.specialInput == input2.specialInput;
    }

    private void RequestRollback(int frame)
    {
        if (!useRollbackNetcode) return;
        
        OnRollbackRequested?.Invoke(frame);
        
        if (logInputEvents)
        {
            Debug.Log($"Rollback requested for frame {frame}");
        }
    }

    public void RollbackToFrame(int frame)
    {
        // Remove all inputs after the rollback frame
        List<int> framesToRemove = new List<int>();
        foreach (var kvp in confirmedInputs)
        {
            if (kvp.Key > frame)
            {
                framesToRemove.Add(kvp.Key);
            }
        }
        
        foreach (int frameToRemove in framesToRemove)
        {
            confirmedInputs.Remove(frameToRemove);
        }
        
        // Clear predictions
        predictedInputs.Clear();
        
        if (logInputEvents)
        {
            Debug.Log($"Rolled back to frame {frame}");
        }
    }

    public PlayerInputData GetLatestInput()
    {
        if (confirmedInputs.Count == 0) return new PlayerInputData();
        
        int latestFrame = -1;
        foreach (int frame in confirmedInputs.Keys)
        {
            if (frame > latestFrame)
            {
                latestFrame = frame;
            }
        }
        
        return confirmedInputs[latestFrame];
    }

    public PlayerInputData GetInputWithDelay(int delayFrames)
    {
        int targetFrame = currentFrame - delayFrames;
        return GetInputForFrame(targetFrame);
    }

    public float GetInputLatency()
    {
        if (confirmedInputs.Count == 0) return 0f;
        
        float totalLatency = 0f;
        int count = 0;
        
        foreach (var kvp in confirmedInputs)
        {
            totalLatency += Time.time - kvp.Value.timestamp;
            count++;
        }
        
        return count > 0 ? totalLatency / count : 0f;
    }

    public int GetCurrentFrame()
    {
        return currentFrame;
    }

    public int GetLastConfirmedFrame()
    {
        return lastConfirmedFrame;
    }

    public int GetInputBufferSize()
    {
        return inputBuffer.Count;
    }

    public void SetInputDelay(int delay)
    {
        inputDelay = delay;
    }

    public void SetMaxRollbackFrames(int frames)
    {
        maxRollbackFrames = frames;
    }

    public void EnableInputPrediction(bool enable)
    {
        useInputPrediction = enable;
    }

    public void EnableInputSmoothing(bool enable)
    {
        useInputSmoothing = enable;
    }

    public void ClearInputBuffer()
    {
        inputBuffer.Clear();
        confirmedInputs.Clear();
        predictedInputs.Clear();
    }

    public static PlayerInputData FromInputManager(int playerId)
    {
        if (InputManager.Instance == null) return new PlayerInputData();
        
        return new PlayerInputData
        {
            frame = Time.frameCount,
            horizontalInput = InputManager.Instance.GetMovementInput().x,
            verticalInput = InputManager.Instance.GetMovementInput().y,
            jumpInput = InputManager.Instance.IsJumping(),
            lightAttackInput = InputManager.Instance.IsAttacking(),
            mediumAttackInput = false, // TODO: Add medium attack input
            heavyAttackInput = false,  // TODO: Add heavy attack input
            blockInput = InputManager.Instance.IsGuarding(),
            specialInput = InputManager.Instance.IsSpecial(),
            timestamp = Time.time
        };
    }

    private GameState GetCurrentGameState()
    {
        // This would get the current game state from the character
        // For now, return a basic state
        return new GameState
        {
            frame = currentFrame,
            position = transform.position,
            rotation = transform.rotation,
            velocity = GetComponent<Rigidbody2D>()?.linearVelocity ?? Vector2.zero,
            health = GetComponent<PlayerCharacter>()?.currentHealth ?? 100f,
            characterState = GetComponent<PlayerCharacter>()?.GetStateData().currentState ?? CharacterState.Idle,
            timestamp = Time.time
        };
    }

    private void OnGUI()
    {
        if (!showInputDebug) return;
        
        GUILayout.BeginArea(new Rect(10, 220, 300, 200));
        GUILayout.Label($"Current Frame: {currentFrame}");
        GUILayout.Label($"Last Confirmed: {lastConfirmedFrame}");
        GUILayout.Label($"Input Buffer: {inputBuffer.Count}");
        GUILayout.Label($"Confirmed Inputs: {confirmedInputs.Count}");
        GUILayout.Label($"Predicted Inputs: {predictedInputs.Count}");
        GUILayout.Label($"Input Latency: {GetInputLatency():F3}s");
        
        PlayerInputData latestInput = GetLatestInput();
        GUILayout.Label($"Latest Input: H={latestInput.horizontalInput:F2}, V={latestInput.verticalInput:F2}");
        GUILayout.EndArea();
    }
} 