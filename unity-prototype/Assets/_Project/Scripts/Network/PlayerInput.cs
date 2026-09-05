using System;
using UnityEngine;

[System.Serializable]
public class PlayerInput
{
    // Movement inputs
    public float horizontalInput;
    public float verticalInput;
    public bool jumpInput;
    public bool crouchInput;
    
    // Combat inputs
    public bool attackInput;
    public bool specialInput;
    public bool rageInput;
    public bool blockInput;
    
    // Additional inputs
    public bool dashInput;
    public bool tauntInput;
    public bool pauseInput;
    
    // Input timing
    public float inputTime;
    public int frameNumber;
    
    // Input validation
    public bool isValid;
    public string inputSource; // "keyboard", "gamepad", "network"
    
    public PlayerInput()
    {
        Reset();
    }
    
    public void Reset()
    {
        horizontalInput = 0f;
        verticalInput = 0f;
        jumpInput = false;
        crouchInput = false;
        attackInput = false;
        specialInput = false;
        rageInput = false;
        blockInput = false;
        dashInput = false;
        tauntInput = false;
        pauseInput = false;
        inputTime = 0f;
        frameNumber = 0;
        isValid = true;
        inputSource = "unknown";
    }
    
    public bool HasAnyInput()
    {
        return Mathf.Abs(horizontalInput) > 0.1f ||
               Mathf.Abs(verticalInput) > 0.1f ||
               jumpInput ||
               crouchInput ||
               attackInput ||
               specialInput ||
               rageInput ||
               blockInput ||
               dashInput ||
               tauntInput ||
               pauseInput;
    }
    
    public bool HasMovementInput()
    {
        return Mathf.Abs(horizontalInput) > 0.1f ||
               Mathf.Abs(verticalInput) > 0.1f ||
               jumpInput ||
               crouchInput;
    }
    
    public bool HasCombatInput()
    {
        return attackInput ||
               specialInput ||
               rageInput ||
               blockInput;
    }
    
    public PlayerInput Clone()
    {
        PlayerInput clone = new PlayerInput();
        clone.horizontalInput = this.horizontalInput;
        clone.verticalInput = this.verticalInput;
        clone.jumpInput = this.jumpInput;
        clone.crouchInput = this.crouchInput;
        clone.attackInput = this.attackInput;
        clone.specialInput = this.specialInput;
        clone.rageInput = this.rageInput;
        clone.blockInput = this.blockInput;
        clone.dashInput = this.dashInput;
        clone.tauntInput = this.tauntInput;
        clone.pauseInput = this.pauseInput;
        clone.inputTime = this.inputTime;
        clone.frameNumber = this.frameNumber;
        clone.isValid = this.isValid;
        clone.inputSource = this.inputSource;
        return clone;
    }
    
    public override bool Equals(object obj)
    {
        if (obj is PlayerInput other)
        {
            return Mathf.Approximately(horizontalInput, other.horizontalInput) &&
                   Mathf.Approximately(verticalInput, other.verticalInput) &&
                   jumpInput == other.jumpInput &&
                   crouchInput == other.crouchInput &&
                   attackInput == other.attackInput &&
                   specialInput == other.specialInput &&
                   rageInput == other.rageInput &&
                   blockInput == other.blockInput &&
                   dashInput == other.dashInput &&
                   tauntInput == other.tauntInput &&
                   pauseInput == other.pauseInput;
        }
        return false;
    }
    
    public override int GetHashCode()
    {
        int hash1 = HashCode.Combine(horizontalInput, verticalInput, jumpInput, crouchInput,
                                    attackInput, specialInput, rageInput, blockInput);
        int hash2 = HashCode.Combine(dashInput, tauntInput, pauseInput);
        return HashCode.Combine(hash1, hash2);
    }
    
    public override string ToString()
    {
        return $"PlayerInput[H:{horizontalInput:F2}, V:{verticalInput:F2}, " +
               $"Jump:{jumpInput}, Attack:{attackInput}, Special:{specialInput}, " +
               $"Rage:{rageInput}, Block:{blockInput}, Frame:{frameNumber}]";
    }
} 