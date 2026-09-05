using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float jumpForce = 10f;
    private Rigidbody2D rb;
    private bool isGrounded = false;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        // Di chuyển trái/phải bằng phím mũi tên
        float move = 0f;
        if (Input.GetKey(KeyCode.LeftArrow))  move = -1f;
        if (Input.GetKey(KeyCode.RightArrow)) move = 1f;
        rb.linearVelocity = new Vector2(move * moveSpeed, rb.linearVelocity.y);

        // Nhảy bằng phím W
        if (Input.GetKeyDown(KeyCode.W) && isGrounded)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        }

        // Ngồi bằng phím S
        if (Input.GetKey(KeyCode.S))
        {
            Debug.Log("Đang ngồi!");
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.contacts[0].normal.y > 0.5f)
            isGrounded = true;
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        isGrounded = false;
    }
} 