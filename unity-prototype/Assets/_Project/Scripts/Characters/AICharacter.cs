using UnityEngine;

public class AICharacter : BaseCharacter
{
    [Header("AI Settings")]
    public float detectionRange = 5f;
    public float attackRange = 2f;
    public float retreatDistance = 3f;
    public float decisionTime = 0.5f;
    public float aggressionLevel = 0.7f;
    
    [Header("AI Behavior")]
    public bool isAggressive = true;
    public bool canBlock = true;
    public bool canJump = true;
    public bool canUseSpecial = true;
    
    private PlayerCharacter target;
    private float decisionTimer;
    private AIState currentAIState;
    private new float lastAttackTime;
    private float attackCooldown = 1f;

    protected override void Awake()
    {
        base.Awake();
        InitializeAI();
    }

    private void InitializeAI()
    {
        // Find target (assume it's the other player)
        PlayerCharacter[] players = FindObjectsOfType<PlayerCharacter>();
        foreach (PlayerCharacter player in players)
        {
            if (player != this)
            {
                target = player;
                break;
            }
        }
        
        if (target == null)
        {
            Debug.LogWarning("AI Character couldn't find a target!");
        }
        
        currentAIState = AIState.Idle;
        decisionTimer = decisionTime;
    }

    protected override void Update()
    {
        base.Update();
        UpdateAI();
    }

    private void UpdateAI()
    {
        if (target == null || isDead) return;

        decisionTimer -= Time.deltaTime;
        
        if (decisionTimer <= 0)
        {
            MakeDecision();
            decisionTimer = decisionTime;
        }
        
        ExecuteCurrentState();
    }

    private void MakeDecision()
    {
        float distanceToTarget = Vector2.Distance(transform.position, target.transform.position);
        
        // Check if we should block
        if (canBlock && target.IsAttacking() && distanceToTarget < attackRange)
        {
            currentAIState = AIState.Blocking;
            return;
        }
        
        // Check if we should retreat
        if (distanceToTarget < retreatDistance && currentHealth < maxHealth * 0.3f)
        {
            currentAIState = AIState.Retreating;
            return;
        }
        
        // Check if we should attack
        if (distanceToTarget <= attackRange && Time.time - lastAttackTime > attackCooldown)
        {
            currentAIState = AIState.Attacking;
            return;
        }
        
        // Check if we should approach
        if (distanceToTarget > attackRange)
        {
            currentAIState = AIState.Approaching;
            return;
        }
        
        // Default to idle
        currentAIState = AIState.Idle;
    }

    private void ExecuteCurrentState()
    {
        switch (currentAIState)
        {
            case AIState.Idle:
                ExecuteIdle();
                break;
            case AIState.Approaching:
                ExecuteApproaching();
                break;
            case AIState.Attacking:
                ExecuteAttacking();
                break;
            case AIState.Blocking:
                ExecuteBlocking();
                break;
            case AIState.Retreating:
                ExecuteRetreating();
                break;
        }
    }

    private void ExecuteIdle()
    {
        // Do nothing, just wait
        horizontalInput = 0f;
        verticalInput = 0f;
    }

    private void ExecuteApproaching()
    {
        if (target == null) return;
        
        // Move towards target
        Vector2 direction = (target.transform.position - transform.position).normalized;
        horizontalInput = direction.x;
        
        // Jump if there's an obstacle or if target is above
        if (canJump && (target.transform.position.y > transform.position.y + 1f))
        {
            jumpInput = true;
        }
    }

    private void ExecuteAttacking()
    {
        if (target == null) return;
        
        // Stop moving
        horizontalInput = 0f;
        
        // Choose attack based on distance and aggression
        float distance = Vector2.Distance(transform.position, target.transform.position);
        
        if (distance <= attackRange)
        {
            // Random attack selection with bias towards light attacks
            float attackRoll = Random.value;
            
            if (attackRoll < 0.6f)
            {
                PerformAttack(AttackType.Light);
            }
            else if (attackRoll < 0.85f)
            {
                PerformAttack(AttackType.Medium);
            }
            else
            {
                PerformAttack(AttackType.Heavy);
            }
            
            lastAttackTime = Time.time;
        }
    }

    private void ExecuteBlocking()
    {
        // Stop moving and block
        horizontalInput = 0f;
        StartBlocking();
        
        // Stop blocking after a short time
        Invoke(nameof(StopBlocking), 0.5f);
    }

    private void ExecuteRetreating()
    {
        if (target == null) return;
        
        // Move away from target
        Vector2 direction = (transform.position - target.transform.position).normalized;
        horizontalInput = direction.x;
        
        // Jump to avoid attacks
        if (canJump && target.IsAttacking())
        {
            jumpInput = true;
        }
    }

    public override void TakeDamage(float damage, AttackType attackType = AttackType.Light)
    {
        base.TakeDamage(damage, attackType);
        
        // AI becomes more aggressive when damaged
        if (currentHealth < maxHealth * 0.5f)
        {
            aggressionLevel = Mathf.Min(aggressionLevel + 0.1f, 1f);
        }
    }

    public void SetTarget(PlayerCharacter newTarget)
    {
        target = newTarget;
    }

    public PlayerCharacter GetTarget()
    {
        return target;
    }

    public AIState GetCurrentAIState()
    {
        return currentAIState;
    }

    public void SetAggressionLevel(float level)
    {
        aggressionLevel = Mathf.Clamp01(level);
    }

    public void SetCanBlock(bool canBlock)
    {
        this.canBlock = canBlock;
    }

    public void SetCanJump(bool canJump)
    {
        this.canJump = canJump;
    }

    public void SetCanUseSpecial(bool canUseSpecial)
    {
        this.canUseSpecial = canUseSpecial;
    }
}

public enum AIState
{
    Idle,
    Approaching,
    Attacking,
    Blocking,
    Retreating
} 