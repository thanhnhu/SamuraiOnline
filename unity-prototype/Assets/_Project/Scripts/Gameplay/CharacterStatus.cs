using UnityEngine;

public enum CharacterState
{
    Idle,
    Walking,
    Moving,
    Running,
    Jumping,
    Falling,
    Attacking,
    Guarding,
    Blocking,   // Đỡ đòn
    Hit,
    Hurt,
    Stunned,    // Choáng
    Dead,
    Special
}

public class CharacterStatus : MonoBehaviour
{
    public int maxHealth = 5;
    public int currentHealth;
    public CharacterState state = CharacterState.Idle;
    public float invincibleTime = 1f; // Thời gian bất tử sau khi bị thương
    public float stunTime = 1f;       // Thời gian choáng
    public float blockDamageReduction = 0.5f; // Giảm sát thương khi block (50%)

    private float invincibleTimer = 0f;
    private float stunTimer = 0f;

    void Start()
    {
        currentHealth = maxHealth;
        state = CharacterState.Idle;
    }

    void Update()
    {
        // Đếm ngược bất tử
        if (invincibleTimer > 0)
            invincibleTimer -= Time.deltaTime;

        // Đếm ngược choáng
        if (state == CharacterState.Stunned)
        {
            stunTimer -= Time.deltaTime;
            if (stunTimer <= 0)
            {
                state = CharacterState.Idle;
            }
        }

        // Chuyển trạng thái về Idle nếu hết bị thương
        if (state == CharacterState.Hurt && invincibleTimer <= 0)
        {
            state = CharacterState.Idle;
        }
    }

    public void TakeDamage(int amount, bool isBlock = false, bool canStun = false)
    {
        if (invincibleTimer > 0 || state == CharacterState.Dead)
            return;

        int finalDamage = amount;
        if (isBlock && state == CharacterState.Blocking)
        {
            finalDamage = Mathf.RoundToInt(amount * blockDamageReduction);
            Debug.Log(gameObject.name + " đã block! Sát thương nhận: " + finalDamage);
        }

        currentHealth -= finalDamage;
        Debug.Log(gameObject.name + " bị thương! Máu còn: " + currentHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            if (isBlock && state == CharacterState.Blocking)
            {
                // Nếu block thành công, không bị choáng
                state = CharacterState.Blocking;
                invincibleTimer = invincibleTime * 0.5f;
            }
            else if (canStun)
            {
                // Nếu đòn có thể gây choáng
                state = CharacterState.Stunned;
                stunTimer = stunTime;
            }
            else
            {
                state = CharacterState.Hurt;
                invincibleTimer = invincibleTime;
            }
        }
    }

    public void StartBlock()
    {
        if (state == CharacterState.Idle || state == CharacterState.Moving)
        {
            state = CharacterState.Blocking;
            Debug.Log(gameObject.name + " bắt đầu block!");
        }
    }

    public void StopBlock()
    {
        if (state == CharacterState.Blocking)
        {
            state = CharacterState.Idle;
            Debug.Log(gameObject.name + " dừng block!");
        }
    }

    public void Die()
    {
        state = CharacterState.Dead;
        Debug.Log(gameObject.name + " đã chết!");
        // Thêm hiệu ứng chết, disable điều khiển, v.v.
        Destroy(gameObject, 1f);
    }

    // Hàm chuyển trạng thái thủ công
    public void SetState(CharacterState newState)
    {
        state = newState;
    }
} 