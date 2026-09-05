using UnityEngine;
using System.Collections.Generic;

public class PlayerCharacter : BaseCharacter
{
    [Header("Player Specific")]
    public string characterName = "Samurai";
    public CharacterType characterType = CharacterType.Samurai;
    
    [Header("Attack Data")]
    public AttackData lightAttack;
    public AttackData mediumAttack;
    public AttackData heavyAttack;
    public AttackData specialAttack;
    
    [Header("Combo System")]
    public List<ComboData> combos;
    public new float comboWindow = 1.0f;
    public int maxComboLength = 5;
    
    [Header("Movement")]
    public float dashSpeed = 15f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 0.5f;
    
    [Header("Special Abilities")]
    public new float rageMeter = 0f;
    public float maxRageMeter = 100f;
    public bool isRageMode = false;
    
    private float dashTimer;
    private float dashCooldownTimer;
    private bool isDashing;
    private List<AttackType> currentCombo;
    private float lastComboTime;

    protected override void Awake()
    {
        base.Awake();
        InitializePlayerCharacter();
    }

    private void InitializePlayerCharacter()
    {
        currentCombo = new List<AttackType>();
        
        // Initialize attack data if not set
        if (lightAttack == null) lightAttack = new AttackData { damage = 10f, range = 1.5f, duration = 0.3f };
        if (mediumAttack == null) mediumAttack = new AttackData { damage = 20f, range = 2f, duration = 0.5f };
        if (heavyAttack == null) heavyAttack = new AttackData { damage = 35f, range = 2.5f, duration = 0.8f };
        if (specialAttack == null) specialAttack = new AttackData { damage = 50f, range = 3f, duration = 1.2f };
    }

    protected override void Update()
    {
        base.Update();
        UpdatePlayerSpecific();
    }

    private void UpdatePlayerSpecific()
    {
        UpdateDash();
        UpdateCombo();
        UpdateRageMeter();
        HandlePlayerInput();
    }

    private void UpdateDash()
    {
        if (dashTimer > 0)
        {
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0)
            {
                isDashing = false;
            }
        }

        if (dashCooldownTimer > 0)
        {
            dashCooldownTimer -= Time.deltaTime;
        }
    }

    private void UpdateCombo()
    {
        if (Time.time - lastComboTime > comboWindow)
        {
            currentCombo.Clear();
        }
    }

    private void UpdateRageMeter()
    {
        if (isRageMode)
        {
            rageMeter -= Time.deltaTime * 10f; // Rage mode drains over time
            if (rageMeter <= 0)
            {
                ExitRageMode();
            }
        }
    }

    private void HandlePlayerInput()
    {
        if (inputHandler == null) return;

        // Handle attack inputs
        if (inputHandler.GetLightAttackInput())
        {
            PerformAttack(AttackType.Light);
            inputHandler.ConsumeBufferedInput(InputType.LightAttack);
        }
        else if (inputHandler.GetMediumAttackInput())
        {
            PerformAttack(AttackType.Medium);
            inputHandler.ConsumeBufferedInput(InputType.MediumAttack);
        }
        else if (inputHandler.GetHeavyAttackInput())
        {
            PerformAttack(AttackType.Heavy);
            inputHandler.ConsumeBufferedInput(InputType.HeavyAttack);
        }

        // Handle special input
        if (inputHandler.GetSpecialInput())
        {
            UseSpecialAttack();
            inputHandler.ConsumeBufferedInput(InputType.Special);
        }

        // Handle block input
        if (inputHandler.GetBlockInput())
        {
            StartBlocking();
        }
        else
        {
            StopBlocking();
        }

        // Handle jump input
        if (inputHandler.GetJumpInput())
        {
            Jump();
            inputHandler.ConsumeBufferedInput(InputType.Jump);
        }

        // Handle dash input (double tap movement)
        HandleDashInput();
    }

    private void HandleDashInput()
    {
        if (dashCooldownTimer <= 0 && Mathf.Abs(horizontalInput) > 0.8f)
        {
            // Simple dash implementation - can be enhanced with double-tap detection
            if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
            {
                Dash();
            }
        }
    }

    public void Dash()
    {
        if (isDashing || dashCooldownTimer > 0 || !isGrounded) return;

        isDashing = true;
        dashTimer = dashDuration;
        dashCooldownTimer = dashCooldown;

        // Apply dash velocity
        Vector2 dashVelocity = new Vector2(isFacingRight ? dashSpeed : -dashSpeed, 0);
        rb.linearVelocity = dashVelocity;
    }

    protected override void HandleMovement()
    {
        if (isDashing) return; // Don't handle normal movement during dash
        
        base.HandleMovement();
    }

    protected override void CreateHitbox(AttackType attackType)
    {
        AttackData attackData = GetAttackData(attackType);
        if (attackData == null) return;

        // Create hitbox GameObject
        GameObject hitboxObj = new GameObject($"Hitbox_{attackType}");
        hitboxObj.transform.SetParent(transform);
        hitboxObj.transform.localPosition = new Vector3(isFacingRight ? attackData.range : -attackData.range, 0, 0);

        // Add hitbox component
        Hitbox hitbox = hitboxObj.AddComponent<Hitbox>();
        hitbox.Initialize(this, attackData, attackType);

        // Add to active hitboxes
        activeHitboxes.Add(hitbox);

        // Destroy hitbox after duration
        Destroy(hitboxObj, attackData.duration);
    }

    private AttackData GetAttackData(AttackType attackType)
    {
        switch (attackType)
        {
            case AttackType.Light:
                return lightAttack;
            case AttackType.Medium:
                return mediumAttack;
            case AttackType.Heavy:
                return heavyAttack;
            case AttackType.Special:
                return specialAttack;
            default:
                return lightAttack;
        }
    }

    public override void PerformAttack(AttackType attackType)
    {
        if (isAttacking || isHit || isDead) return;

        // Add to combo
        currentCombo.Add(attackType);
        lastComboTime = Time.time;

        // Check for valid combos
        CheckCombo(attackType);

        base.PerformAttack(attackType);
    }

    private void CheckCombo(AttackType currentAttack)
    {
        foreach (ComboData combo in combos)
        {
            if (combo.IsValid(currentCombo))
            {
                // Execute combo
                ExecuteCombo(combo);
                break;
            }
        }
    }

    private void ExecuteCombo(ComboData combo)
    {
        // Apply combo bonuses
        AttackData comboAttack = GetAttackData(combo.finalAttack);
        comboAttack.damage *= combo.damageMultiplier;
        
        // Clear combo and execute final attack
        currentCombo.Clear();
        PerformAttack(combo.finalAttack);
    }

    public override void TakeDamage(float damage, AttackType attackType = AttackType.Light)
    {
        base.TakeDamage(damage, attackType);
        
        // Gain rage meter when taking damage
        if (!isRageMode)
        {
            rageMeter += damage * 0.3f;
            rageMeter = Mathf.Min(rageMeter, maxRageMeter);
            
            // Check if rage mode should activate
            if (rageMeter >= maxRageMeter)
            {
                EnterRageMode();
            }
        }
    }

    private void EnterRageMode()
    {
        isRageMode = true;
        // Apply rage mode bonuses
        attackPower *= 1.5f;
        moveSpeed *= 1.2f;
        
        // Visual effects
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.red;
        }
    }

    private void ExitRageMode()
    {
        isRageMode = false;
        rageMeter = 0f;
        
        // Remove rage mode bonuses
        attackPower = attackPower / 1.5f;
        moveSpeed = moveSpeed / 1.2f;
        
        // Reset visual effects
        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.white;
        }
    }

    public float GetRageMeterPercentage()
    {
        return rageMeter / maxRageMeter;
    }

    public bool IsRageMode()
    {
        return isRageMode;
    }

    public List<AttackType> GetCurrentCombo()
    {
        return new List<AttackType>(currentCombo);
    }

    public void ResetCombo()
    {
        currentCombo.Clear();
    }
}

[System.Serializable]
public class AttackData
{
    public float damage = 10f;
    public float range = 1.5f;
    public float duration = 0.3f;
    public float knockback = 5f;
    public bool canBlock = true;
    public bool canCounter = false;
    public string animationTrigger = "Attack";
}

[System.Serializable]
public class ComboData
{
    public List<AttackType> sequence;
    public AttackType finalAttack;
    public float damageMultiplier = 1.5f;
    public string comboName = "Combo";

    public bool IsValid(List<AttackType> currentCombo)
    {
        if (currentCombo.Count != sequence.Count) return false;
        
        for (int i = 0; i < sequence.Count; i++)
        {
            if (currentCombo[i] != sequence[i]) return false;
        }
        
        return true;
    }
}

public enum CharacterType
{
    Samurai,
    Ninja,
    Monk,
    Warrior,
    Archer
} 