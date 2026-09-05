using UnityEngine;
using System.Collections.Generic;

public abstract class BaseCharacter : MonoBehaviour
{
    [Header("Character Stats")]
    public float maxHealth = 100f;
    public float currentHealth;
    public float attackPower = 10f;
    public float defensePower = 5f;
    public float moveSpeed = 5f;
    public float jumpForce = 10f;
    public float airControl = 0.5f;
    public float gravity = 20f;
    public float maxFallSpeed = 15f;

    [Header("Combat")]
    public float specialMeter = 0f;
    public float maxSpecialMeter = 100f;
    public float specialMeterGainRate = 10f;
    public float specialMeterDecayRate = 5f;
    public float blockStun = 0.2f;
    public float hitStun = 0.5f;
    public float attackRecovery = 0.3f;

    [Header("Components")]
    protected Animator animator;
    protected Rigidbody2D rb;
    protected SpriteRenderer spriteRenderer;
    protected BoxCollider2D hitbox;
    protected BoxCollider2D hurtbox;
    protected CharacterInputHandler inputHandler;

    [Header("State Management")]
    protected ICharacterState currentState;
    protected CharacterStateData stateData;
    protected Dictionary<CharacterState, ICharacterState> stateMachine;
    
    [Header("Character Properties")]
    protected bool isFacingRight = true;
    protected bool isGrounded;
    protected bool isBlocking;
    protected bool isAttacking;
    protected bool isHit;
    protected bool isDead;
    protected float stunTimer;
    protected float attackTimer;
    protected float blockTimer;

    [Header("Movement")]
    protected float horizontalInput;
    protected float verticalInput;
    protected bool jumpInput;
    protected AttackType attackInput;
    protected bool blockInput;
    protected bool specialInput;

    [Header("Combat")]
    protected List<Hitbox> activeHitboxes;
    protected List<Hurtbox> activeHurtboxes;
    protected float lastAttackTime;
    protected int comboCount;
    protected float comboWindow = 1.0f;

    protected virtual void Awake()
    {
        InitializeComponents();
        InitializeStateMachine();
        InitializeStateData();
    }

    protected virtual void InitializeComponents()
    {
        animator = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        hitbox = GetComponent<BoxCollider2D>();
        hurtbox = GetComponentInChildren<BoxCollider2D>();
        inputHandler = GetComponent<CharacterInputHandler>();

        if (rb != null)
        {
            rb.gravityScale = gravity / 9.81f;
        }

        currentHealth = maxHealth;
        activeHitboxes = new List<Hitbox>();
        activeHurtboxes = new List<Hurtbox>();
    }

    protected virtual void InitializeStateMachine()
    {
        stateMachine = new Dictionary<CharacterState, ICharacterState>
        {
            { CharacterState.Idle, new IdleState(this) },
            { CharacterState.Walking, new WalkingState(this) },
            { CharacterState.Running, new RunningState(this) },
            { CharacterState.Jumping, new JumpingState(this) },
            { CharacterState.Falling, new FallingState(this) },
            { CharacterState.Attacking, new AttackingState(this) },
            { CharacterState.Guarding, new GuardingState(this) },
            { CharacterState.Hit, new HitState(this) },
            { CharacterState.Dead, new DeadState(this) },
            { CharacterState.Special, new SpecialState(this) }
        };
    }

    protected virtual void InitializeStateData()
    {
        stateData = new CharacterStateData();
        SetState(CharacterState.Idle);
    }

    protected virtual void Update()
    {
        if (isDead) return;

        HandleInput();
        UpdateTimers();
        UpdateSpecialMeter();
        UpdateState();
        UpdateAnimations();
    }

    protected virtual void HandleInput()
    {
        if (inputHandler != null)
        {
            horizontalInput = inputHandler.GetHorizontalInput();
            verticalInput = inputHandler.GetVerticalInput();
            jumpInput = inputHandler.GetJumpInput();
            attackInput = inputHandler.GetAttackInput();
            blockInput = inputHandler.GetBlockInput();
            specialInput = inputHandler.GetSpecialInput();
        }
    }

    protected virtual void UpdateTimers()
    {
        if (stunTimer > 0)
        {
            stunTimer -= Time.deltaTime;
            if (stunTimer <= 0)
            {
                isHit = false;
            }
        }

        if (attackTimer > 0)
        {
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0)
            {
                isAttacking = false;
            }
        }

        if (blockTimer > 0)
        {
            blockTimer -= Time.deltaTime;
            if (blockTimer <= 0)
            {
                isBlocking = false;
            }
        }
    }

    protected virtual void UpdateSpecialMeter()
    {
        if (specialMeter < maxSpecialMeter)
        {
            specialMeter += specialMeterGainRate * Time.deltaTime;
            specialMeter = Mathf.Min(specialMeter, maxSpecialMeter);
        }
    }

    protected virtual void UpdateState()
    {
        if (currentState != null)
        {
            currentState.UpdateState();
        }
    }

    protected virtual void UpdateAnimations()
    {
        if (animator != null)
        {
            animator.SetBool("IsGrounded", isGrounded);
            animator.SetBool("IsAttacking", isAttacking);
            animator.SetBool("IsBlocking", isBlocking);
            animator.SetBool("IsHit", isHit);
            animator.SetFloat("HorizontalSpeed", Mathf.Abs(horizontalInput));
            animator.SetFloat("VerticalSpeed", rb.linearVelocity.y);
            animator.SetFloat("Health", currentHealth / maxHealth);
            animator.SetFloat("SpecialMeter", specialMeter / maxSpecialMeter);
        }
    }

    protected virtual void FixedUpdate()
    {
        if (isDead) return;

        CheckGrounded();
        HandleMovement();
    }

    protected virtual void CheckGrounded()
    {
        // Simple ground check using raycast
        Vector2 raycastOrigin = transform.position;
        float raycastDistance = 0.1f;
        LayerMask groundLayer = LayerMask.GetMask("Ground");

        isGrounded = Physics2D.Raycast(raycastOrigin, Vector2.down, raycastDistance, groundLayer);
    }

    protected virtual void HandleMovement()
    {
        if (isHit || isAttacking) return;

        float currentMoveSpeed = moveSpeed;
        if (!isGrounded)
        {
            currentMoveSpeed *= airControl;
        }

        Vector2 velocity = rb.linearVelocity;
        velocity.x = horizontalInput * currentMoveSpeed;

        // Apply gravity when not grounded
        if (!isGrounded)
        {
            velocity.y -= gravity * Time.fixedDeltaTime;
            velocity.y = Mathf.Max(velocity.y, -maxFallSpeed);
        }

        rb.linearVelocity = velocity;

        // Handle facing direction
        if (horizontalInput > 0 && !isFacingRight)
        {
            Flip();
        }
        else if (horizontalInput < 0 && isFacingRight)
        {
            Flip();
        }
    }

    public virtual void TakeDamage(float damage, AttackType attackType = AttackType.Light)
    {
        if (isDead) return;

        float finalDamage = damage;
        
        if (isBlocking)
        {
            finalDamage *= 0.3f; // Block reduces damage by 70%
            blockTimer = blockStun;
        }
        else
        {
            finalDamage = Mathf.Max(0, damage - defensePower);
            stunTimer = hitStun;
            isHit = true;
        }

        currentHealth -= finalDamage;
        
        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            // Gain meter when taking damage
            specialMeter += finalDamage * 0.5f;
            specialMeter = Mathf.Min(specialMeter, maxSpecialMeter);
        }
    }

    public virtual void Heal(float amount)
    {
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
    }

    protected virtual void Die()
    {
        isDead = true;
        SetState(CharacterState.Dead);
        
        if (animator != null)
        {
            animator.SetTrigger("Die");
        }
    }

    public virtual void UseSpecialAttack()
    {
        if (specialMeter >= maxSpecialMeter && !isAttacking && !isHit)
        {
            SetState(CharacterState.Special);
            specialMeter = 0f;
        }
    }

    public virtual void PerformAttack(AttackType attackType)
    {
        if (isAttacking || isHit || isDead) return;

        // Check combo window
        if (Time.time - lastAttackTime > comboWindow)
        {
            comboCount = 0;
        }

        comboCount++;
        lastAttackTime = Time.time;
        
        SetState(CharacterState.Attacking);
        attackTimer = attackRecovery;
        isAttacking = true;

        // Create hitbox based on attack type
        CreateHitbox(attackType);
    }

    protected virtual void CreateHitbox(AttackType attackType)
    {
        // This will be implemented by specific character classes
        // to create appropriate hitboxes for their attacks
    }

    public virtual void StartBlocking()
    {
        if (isAttacking || isHit || isDead) return;
        
        isBlocking = true;
        SetState(CharacterState.Guarding);
    }

    public virtual void StopBlocking()
    {
        isBlocking = false;
        if (stateData.currentState == CharacterState.Guarding)
        {
            SetState(CharacterState.Idle);
        }
    }

    public virtual void Jump()
    {
        if (!isGrounded || isAttacking || isHit || isDead) return;

        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        SetState(CharacterState.Jumping);
    }

    public virtual void Flip()
    {
        isFacingRight = !isFacingRight;
        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
    }

    public virtual void SetState(CharacterState newState)
    {
        if (currentState != null)
        {
            currentState.ExitState();
        }

        currentState = stateMachine[newState];
        stateData.currentState = newState;
        currentState.EnterState();
    }

    public virtual CharacterStateData GetStateData()
    {
        return stateData;
    }

    public virtual bool IsFacingRight()
    {
        return isFacingRight;
    }

    public virtual bool IsGrounded()
    {
        return isGrounded;
    }

    public virtual bool IsAttacking()
    {
        return isAttacking;
    }

    public virtual bool IsBlocking()
    {
        return isBlocking;
    }

    public virtual bool IsHit()
    {
        return isHit;
    }

    public virtual bool IsDead()
    {
        return isDead;
    }

    public virtual float GetHealthPercentage()
    {
        return currentHealth / maxHealth;
    }

    public virtual float GetSpecialMeterPercentage()
    {
        return specialMeter / maxSpecialMeter;
    }

    // Replay system methods
    public virtual CharacterState GetCurrentState()
    {
        return stateData.currentState;
    }
    
    public virtual void RestoreState(CharacterState state)
    {
        if (state != stateData.currentState)
        {
            SetState(state);
        }
    }
    
    // Public properties for network synchronization
    public float HorizontalInput
    {
        get { return horizontalInput; }
        set { horizontalInput = value; }
    }
    
    public float VerticalInput
    {
        get { return verticalInput; }
        set { verticalInput = value; }
    }
    
    // Additional properties for replay system
    public float rageMeter
    {
        get { return specialMeter; }
        set { specialMeter = Mathf.Clamp(value, 0f, maxSpecialMeter); }
    }
} 