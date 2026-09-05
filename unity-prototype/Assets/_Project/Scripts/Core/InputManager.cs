using UnityEngine;
using System.Collections.Generic;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    [Header("Input Settings")]
    public float inputBufferTime = 0.1f;
    public float doubleTapWindow = 0.3f;

    // Input buffer
    private float lastInputTime;
    private Vector2 lastMoveInput;
    private bool lastAttackInput;
    private bool lastJumpInput;
    private bool lastGuardInput;
    private bool lastSpecialInput;

    // Event for network input
    public delegate void InputReceivedHandler(NetworkInput input);
    public event InputReceivedHandler OnInputReceived;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        UpdateInputBuffer();
        SendNetworkInput();
    }

    private void UpdateInputBuffer()
    {
        if (Time.time - lastInputTime > inputBufferTime)
        {
            lastMoveInput = Vector2.zero;
            lastAttackInput = false;
            lastJumpInput = false;
            lastGuardInput = false;
            lastSpecialInput = false;
        }
    }

    private void SendNetworkInput()
    {
        if (OnInputReceived != null)
        {
            PlayerInputData input = NetworkInput.FromInputManager(0); // TODO: Get actual player ID
            // Convert PlayerInputData to NetworkInput if needed
            // For now, we'll need to create a NetworkInput from PlayerInputData
            OnInputReceived(null); // TODO: Fix this conversion
        }
    }

    public Vector2 GetMovementInput()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        Vector2 input = new Vector2(horizontal, vertical);
        
        if (input != Vector2.zero)
        {
            lastMoveInput = input;
            lastInputTime = Time.time;
        }
        return input;
    }

    public bool IsAttacking()
    {
        bool input = Input.GetButton("Fire1") || Input.GetKey(KeyCode.J);
        if (input)
        {
            lastAttackInput = true;
            lastInputTime = Time.time;
        }
        return input;
    }

    public bool IsJumping()
    {
        bool input = Input.GetButton("Jump") || Input.GetKey(KeyCode.Space);
        if (input)
        {
            lastJumpInput = true;
            lastInputTime = Time.time;
        }
        return input;
    }

    public bool IsGuarding()
    {
        bool input = Input.GetButton("Fire2") || Input.GetKey(KeyCode.K);
        if (input)
        {
            lastGuardInput = true;
            lastInputTime = Time.time;
        }
        return input;
    }

    public bool IsSpecial()
    {
        bool input = Input.GetButton("Fire3") || Input.GetKey(KeyCode.L);
        if (input)
        {
            lastSpecialInput = true;
            lastInputTime = Time.time;
        }
        return input;
    }

    public bool IsDoubleTap(Vector2 direction)
    {
        // Implement double tap detection
        return false;
    }

    public bool IsComboInput(string combo)
    {
        // Implement combo detection
        return false;
    }
} 