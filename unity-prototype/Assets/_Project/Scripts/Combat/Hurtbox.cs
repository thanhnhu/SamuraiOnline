using UnityEngine;

public class Hurtbox : MonoBehaviour
{
    [Header("Hurtbox Settings")]
    public HurtboxType hurtboxType = HurtboxType.Body;
    public float damageMultiplier = 1f;
    public bool canBeHit = true;
    public bool canBlock = true;
    public bool canCounter = false;
    
    [Header("Visual Debug")]
    public bool showDebug = true;
    public Color debugColor = Color.blue;

    private BaseCharacter owner;
    private BoxCollider2D hurtboxCollider;

    private void Awake()
    {
        owner = GetComponentInParent<BaseCharacter>();
        hurtboxCollider = GetComponent<BoxCollider2D>();
        
        if (hurtboxCollider == null)
        {
            hurtboxCollider = gameObject.AddComponent<BoxCollider2D>();
        }
        
        // Set up collider
        hurtboxCollider.isTrigger = true;
        hurtboxCollider.size = new Vector2(1f, 1f);
    }

    public BaseCharacter GetOwner()
    {
        return owner;
    }

    public HurtboxType GetHurtboxType()
    {
        return hurtboxType;
    }

    public float GetDamageMultiplier()
    {
        return damageMultiplier;
    }

    public bool CanBeHit()
    {
        return canBeHit && !owner.IsDead();
    }

    public bool CanBlock()
    {
        return canBlock && owner.IsBlocking();
    }

    public bool CanCounter()
    {
        return canCounter && owner.IsAttacking();
    }

    public void SetDamageMultiplier(float multiplier)
    {
        damageMultiplier = multiplier;
    }

    public void SetCanBeHit(bool canHit)
    {
        canBeHit = canHit;
    }

    public void SetCanBlock(bool canBlock)
    {
        this.canBlock = canBlock;
    }

    public void SetCanCounter(bool canCounter)
    {
        this.canCounter = canCounter;
    }

    public void EnableHurtbox()
    {
        if (hurtboxCollider != null)
        {
            hurtboxCollider.enabled = true;
        }
        canBeHit = true;
    }

    public void DisableHurtbox()
    {
        if (hurtboxCollider != null)
        {
            hurtboxCollider.enabled = false;
        }
        canBeHit = false;
    }

    public void ResizeHurtbox(Vector2 size)
    {
        if (hurtboxCollider != null)
        {
            hurtboxCollider.size = size;
        }
    }

    public void MoveHurtbox(Vector3 localPosition)
    {
        transform.localPosition = localPosition;
    }

    private void OnDrawGizmos()
    {
        if (!showDebug) return;
        
        Gizmos.color = debugColor;
        Gizmos.DrawWireCube(transform.position, transform.localScale);
    }
}

public enum HurtboxType
{
    Head,
    Body,
    Arms,
    Legs,
    Weapon
} 