using UnityEngine;
using System.Collections.Generic;

public class Hitbox : MonoBehaviour
{
    [Header("Hitbox Settings")]
    public float damage = 10f;
    public float knockback = 5f;
    public AttackType attackType = AttackType.Light;
    public bool canBlock = true;
    public bool canCounter = false;
    public LayerMask targetLayers = -1;
    
    [Header("Visual Debug")]
    public bool showDebug = true;
    public Color debugColor = Color.red;

    private BaseCharacter owner;
    private AttackData attackData;
    private List<BaseCharacter> hitTargets;
    private bool isActive = true;

    private void Awake()
    {
        hitTargets = new List<BaseCharacter>();
        
        // Add collider if not present
        if (GetComponent<Collider2D>() == null)
        {
            BoxCollider2D collider = gameObject.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            collider.size = new Vector2(1f, 1f);
        }
    }

    public void Initialize(BaseCharacter owner, AttackData attackData, AttackType attackType)
    {
        this.owner = owner;
        this.attackData = attackData;
        this.attackType = attackType;
        
        // Set properties from attack data
        this.damage = attackData.damage;
        this.knockback = attackData.knockback;
        this.canBlock = attackData.canBlock;
        this.canCounter = attackData.canCounter;
        
        // Apply owner's attack power
        this.damage += owner.attackPower;
        
        hitTargets.Clear();
        isActive = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isActive) return;
        
        // Check if we can hit this target
        if (!CanHitTarget(other)) return;
        
        BaseCharacter target = other.GetComponent<BaseCharacter>();
        if (target == null || target == owner) return;
        
        // Check if we've already hit this target
        if (hitTargets.Contains(target)) return;
        
        // Check if target is blocking
        if (target.IsBlocking() && canBlock)
        {
            HandleBlockedHit(target);
        }
        else
        {
            HandleSuccessfulHit(target);
        }
        
        // Add to hit targets to prevent multiple hits
        hitTargets.Add(target);
    }

    private bool CanHitTarget(Collider2D other)
    {
        // Check layer mask
        if (((1 << other.gameObject.layer) & targetLayers) == 0)
            return false;
            
        // Check if target has a character component
        BaseCharacter target = other.GetComponent<BaseCharacter>();
        if (target == null)
            return false;
            
        // Check if target is dead
        if (target.IsDead())
            return false;
            
        return true;
    }

    private void HandleSuccessfulHit(BaseCharacter target)
    {
        // Apply damage
        target.TakeDamage(damage, attackType);
        
        // Apply knockback
        ApplyKnockback(target);
        
        // Trigger hit effects
        TriggerHitEffects(target);
        
        // Notify owner of successful hit
        if (owner is PlayerCharacter playerCharacter)
        {
            // Could add combo tracking, meter gain, etc.
        }
    }

    private void HandleBlockedHit(BaseCharacter target)
    {
        // Apply reduced damage for blocked hits
        float blockedDamage = damage * 0.3f;
        target.TakeDamage(blockedDamage, attackType);
        
        // Apply minimal knockback
        ApplyKnockback(target, 0.5f);
        
        // Trigger block effects
        TriggerBlockEffects(target);
        
        // Notify owner of blocked hit
        if (owner is PlayerCharacter playerCharacter)
        {
            // Could add block stun, meter gain, etc.
        }
    }

    private void ApplyKnockback(BaseCharacter target, float multiplier = 1f)
    {
        Rigidbody2D targetRb = target.GetComponent<Rigidbody2D>();
        if (targetRb == null) return;
        
        // Calculate knockback direction
        Vector2 knockbackDirection = (target.transform.position - owner.transform.position).normalized;
        knockbackDirection.y = 0.5f; // Add some upward force
        knockbackDirection.Normalize();
        
        // Apply knockback force
        Vector2 knockbackForce = knockbackDirection * knockback * multiplier;
        targetRb.AddForce(knockbackForce, ForceMode2D.Impulse);
    }

    private void TriggerHitEffects(BaseCharacter target)
    {
        // Play hit sound
        // AudioManager.Instance?.PlaySound("Hit");
        
        // Spawn hit particles
        SpawnHitParticles(target.transform.position);
        
        // Screen shake
        // CameraShake.Instance?.Shake(0.1f, 0.2f);
        
        // Hit stop (time slow effect)
        // TimeManager.Instance?.HitStop(0.1f);
    }

    private void TriggerBlockEffects(BaseCharacter target)
    {
        // Play block sound
        // AudioManager.Instance?.PlaySound("Block");
        
        // Spawn block particles
        SpawnBlockParticles(target.transform.position);
        
        // Screen shake (less intense)
        // CameraShake.Instance?.Shake(0.05f, 0.1f);
    }

    private void SpawnHitParticles(Vector3 position)
    {
        // This would spawn hit effect particles
        // For now, just log the effect
        Debug.Log($"Hit effect at {position}");
    }

    private void SpawnBlockParticles(Vector3 position)
    {
        // This would spawn block effect particles
        // For now, just log the effect
        Debug.Log($"Block effect at {position}");
    }

    public void Deactivate()
    {
        isActive = false;
    }

    public void Activate()
    {
        isActive = true;
        hitTargets.Clear();
    }

    public bool IsActive()
    {
        return isActive;
    }

    public BaseCharacter GetOwner()
    {
        return owner;
    }

    public AttackData GetAttackData()
    {
        return attackData;
    }

    private void OnDrawGizmos()
    {
        if (!showDebug) return;
        
        Gizmos.color = debugColor;
        Gizmos.DrawWireCube(transform.position, transform.localScale);
    }
} 