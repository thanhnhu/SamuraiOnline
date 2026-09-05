using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    public float attackRange = 1f;
    public int attackDamage = 1;
    public float attackCooldown = 0.5f;
    public LayerMask enemyLayers;

    private float lastAttackTime = 0f;

    void Update()
    {
        // Tấn công bằng phím J
        if (Input.GetKeyDown(KeyCode.J) && Time.time - lastAttackTime > attackCooldown)
        {
            Attack();
            lastAttackTime = Time.time;
        }
    }

    void Attack()
    {
        // Vẽ vùng tấn công phía trước nhân vật
        Vector2 attackPos = transform.position + transform.right * (transform.localScale.x > 0 ? 1 : -1) * attackRange * 0.5f;
        Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(attackPos, attackRange, enemyLayers);

        foreach (Collider2D enemy in hitEnemies)
        {
            // Gây sát thương cho đối thủ (giả sử có script EnemyHealth)
            enemy.GetComponent<EnemyHealth>()?.TakeDamage(attackDamage);
        }

        // Debug vùng tấn công
        Debug.Log("Tấn công! Số đối thủ trúng: " + hitEnemies.Length);
    }

    void OnDrawGizmosSelected()
    {
        // Vẽ vùng tấn công trong Editor
        Vector2 attackPos = transform.position + transform.right * (transform.localScale.x > 0 ? 1 : -1) * attackRange * 0.5f;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPos, attackRange);
    }
} 