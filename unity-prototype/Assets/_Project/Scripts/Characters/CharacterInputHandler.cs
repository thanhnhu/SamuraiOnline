using UnityEngine;
using System.Collections.Generic;

public class CharacterInputHandler : MonoBehaviour
{
    [Header("Input Settings")]
    public bool useGamepad = false;
    public int playerNumber = 1;
    
    [Header("Keyboard Controls")]
    public KeyCode leftKey = KeyCode.A;
    public KeyCode rightKey = KeyCode.D;
    public KeyCode upKey = KeyCode.W;
    public KeyCode downKey = KeyCode.S;
    public KeyCode jumpKey = KeyCode.Space;
    public KeyCode lightAttackKey = KeyCode.J;
    public KeyCode mediumAttackKey = KeyCode.K;
    public KeyCode heavyAttackKey = KeyCode.L;
    public KeyCode blockKey = KeyCode.LeftShift;
    public KeyCode specialKey = KeyCode.U;

    [Header("Gamepad Controls")]
    public string horizontalAxis = "Horizontal";
    public string verticalAxis = "Vertical";
    public KeyCode gamepadJumpButton = KeyCode.Joystick1Button0;
    public KeyCode gamepadLightAttackButton = KeyCode.Joystick1Button1;
    public KeyCode gamepadMediumAttackButton = KeyCode.Joystick1Button2;
    public KeyCode gamepadHeavyAttackButton = KeyCode.Joystick1Button3;
    public KeyCode gamepadBlockButton = KeyCode.Joystick1Button4;
    public KeyCode gamepadSpecialButton = KeyCode.Joystick1Button5;

    [Header("Input Buffering")]
    public float inputBufferTime = 0.1f;
    public int maxBufferedInputs = 3;

    private float horizontalInput;
    private float verticalInput;
    private bool jumpInput;
    private bool lightAttackInput;
    private bool mediumAttackInput;
    private bool heavyAttackInput;
    private bool blockInput;
    private bool specialInput;

    private InputBuffer inputBuffer;

    private void Awake()
    {
        inputBuffer = new InputBuffer(inputBufferTime, maxBufferedInputs);
    }

    private void Update()
    {
        if (useGamepad)
        {
            HandleGamepadInput();
        }
        else
        {
            HandleKeyboardInput();
        }

        inputBuffer.Update();
    }

    private void HandleKeyboardInput()
    {
        // Movement
        horizontalInput = 0f;
        if (Input.GetKey(leftKey)) horizontalInput -= 1f;
        if (Input.GetKey(rightKey)) horizontalInput += 1f;
        horizontalInput = Mathf.Clamp(horizontalInput, -1f, 1f);

        verticalInput = 0f;
        if (Input.GetKey(upKey)) verticalInput += 1f;
        if (Input.GetKey(downKey)) verticalInput -= 1f;
        verticalInput = Mathf.Clamp(verticalInput, -1f, 1f);

        // Actions
        jumpInput = Input.GetKeyDown(jumpKey);
        lightAttackInput = Input.GetKeyDown(lightAttackKey);
        mediumAttackInput = Input.GetKeyDown(mediumAttackKey);
        heavyAttackInput = Input.GetKeyDown(heavyAttackKey);
        blockInput = Input.GetKey(blockKey);
        specialInput = Input.GetKeyDown(specialKey);

        // Buffer inputs
        if (jumpInput) inputBuffer.BufferInput(InputType.Jump);
        if (lightAttackInput) inputBuffer.BufferInput(InputType.LightAttack);
        if (mediumAttackInput) inputBuffer.BufferInput(InputType.MediumAttack);
        if (heavyAttackInput) inputBuffer.BufferInput(InputType.HeavyAttack);
        if (specialInput) inputBuffer.BufferInput(InputType.Special);
    }

    private void HandleGamepadInput()
    {
        string playerPrefix = playerNumber == 1 ? "" : "P2_";
        
        // Movement
        horizontalInput = Input.GetAxis(playerPrefix + horizontalAxis);
        verticalInput = Input.GetAxis(playerPrefix + verticalAxis);

        // Actions
        jumpInput = Input.GetKeyDown(gamepadJumpButton);
        lightAttackInput = Input.GetKeyDown(gamepadLightAttackButton);
        mediumAttackInput = Input.GetKeyDown(gamepadMediumAttackButton);
        heavyAttackInput = Input.GetKeyDown(gamepadHeavyAttackButton);
        blockInput = Input.GetKey(gamepadBlockButton);
        specialInput = Input.GetKeyDown(gamepadSpecialButton);

        // Buffer inputs
        if (jumpInput) inputBuffer.BufferInput(InputType.Jump);
        if (lightAttackInput) inputBuffer.BufferInput(InputType.LightAttack);
        if (mediumAttackInput) inputBuffer.BufferInput(InputType.MediumAttack);
        if (heavyAttackInput) inputBuffer.BufferInput(InputType.HeavyAttack);
        if (specialInput) inputBuffer.BufferInput(InputType.Special);
    }

    // Public getters for input values
    public float GetHorizontalInput()
    {
        return horizontalInput;
    }

    public float GetVerticalInput()
    {
        return verticalInput;
    }

    public bool GetJumpInput()
    {
        return jumpInput || inputBuffer.HasBufferedInput(InputType.Jump);
    }

    public bool GetLightAttackInput()
    {
        return lightAttackInput || inputBuffer.HasBufferedInput(InputType.LightAttack);
    }

    public bool GetMediumAttackInput()
    {
        return mediumAttackInput || inputBuffer.HasBufferedInput(InputType.MediumAttack);
    }

    public bool GetHeavyAttackInput()
    {
        return heavyAttackInput || inputBuffer.HasBufferedInput(InputType.HeavyAttack);
    }

    public bool GetBlockInput()
    {
        return blockInput;
    }

    public bool GetSpecialInput()
    {
        return specialInput || inputBuffer.HasBufferedInput(InputType.Special);
    }

    public AttackType GetAttackInput()
    {
        if (GetHeavyAttackInput()) return AttackType.Heavy;
        if (GetMediumAttackInput()) return AttackType.Medium;
        if (GetLightAttackInput()) return AttackType.Light;
        return AttackType.Light; // Default
    }

    public void ConsumeBufferedInput(InputType inputType)
    {
        inputBuffer.ConsumeInput(inputType);
    }
}

public enum InputType
{
    Jump,
    LightAttack,
    MediumAttack,
    HeavyAttack,
    Special
}

public class InputBuffer
{
    private float bufferTime;
    private int maxInputs;
    private Dictionary<InputType, List<float>> bufferedInputs;

    public InputBuffer(float bufferTime, int maxInputs)
    {
        this.bufferTime = bufferTime;
        this.maxInputs = maxInputs;
        bufferedInputs = new Dictionary<InputType, List<float>>();
        
        foreach (InputType inputType in System.Enum.GetValues(typeof(InputType)))
        {
            bufferedInputs[inputType] = new List<float>();
        }
    }

    public void Update()
    {
        float currentTime = Time.time;
        
        foreach (var kvp in bufferedInputs)
        {
            kvp.Value.RemoveAll(time => currentTime - time > bufferTime);
        }
    }

    public void BufferInput(InputType inputType)
    {
        if (bufferedInputs[inputType].Count < maxInputs)
        {
            bufferedInputs[inputType].Add(Time.time);
        }
    }

    public bool HasBufferedInput(InputType inputType)
    {
        return bufferedInputs[inputType].Count > 0;
    }

    public void ConsumeInput(InputType inputType)
    {
        if (bufferedInputs[inputType].Count > 0)
        {
            bufferedInputs[inputType].RemoveAt(0);
        }
    }

    public void ClearAllInputs()
    {
        foreach (var kvp in bufferedInputs)
        {
            kvp.Value.Clear();
        }
    }
} 