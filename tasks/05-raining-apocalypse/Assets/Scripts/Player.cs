using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Player : MonoBehaviour
{

    public GameObject losePanel;

    public Text healthDisplay;

    public float speed;
    private float input;

    Rigidbody2D rb;
    Animator anim;
    AudioSource source;

    public int health;

    // --- the dash -----------------------------------------------------------
    // The course describes a dash as a short burst of extra speed. The four
    // fields below are the ones the course names, so they keep their names.

    /// <summary>How long a single dash may last, in seconds.</summary>
    public float startDashTime = 0.2f;

    /// <summary>How long the dash that is running right now has been running.</summary>
    public float dashTime;

    /// <summary>The speed that is added to the movement speed while dashing.</summary>
    public float extraSpeed = 20f;

    /// <summary>True while a dash is running.</summary>
    public bool dashing;

    float dashCooldown;          // seconds that still have to pass before the next dash
    float dashDirection;         // -1 or 1: the direction the dash runs in
    float facingDirection = 1f;  // the direction the player is looking at
    float? inputOverride;        // lets a head-less probe drive the player

    // Start is called before the first frame update
    void Start()
    {
        source = GetComponent<AudioSource>();
        anim = GetComponent<Animator>();
        rb = GetComponent<Rigidbody2D>();
        healthDisplay.text = health.ToString();
    }

    private void Update()
    {
        UpdateDash();

        // Remember the direction the player is looking at, so that a dash
        // started without an arrow key still goes the right way.
        if (input != 0)
        {
            facingDirection = Mathf.Sign(input);

            // A dash follows the arrow key that is held down.
            if (dashing)
            {
                dashDirection = facingDirection;
            }
        }

        // The direction the player is actually moving in.
        float facing = MoveDirection;

        anim.SetBool("isRunning", facing != 0);

        if (facing > 0)
        {
            transform.eulerAngles = new Vector3(0, 0, 0);
        }
        else if (facing < 0) {
            transform.eulerAngles = new Vector3(0, 180, 0);
        }
    }

    /// <summary>
    /// The direction the player moves in: the direction that is held down, or
    /// the direction of the dash while the dash carries the player.
    /// </summary>
    private float MoveDirection
    {
        get
        {
            if (input != 0)
            {
                return input;
            }

            return dashing ? dashDirection : 0f;
        }
    }

    /// <summary>
    /// Advances the dash clock and reads the dash key. A dash runs for
    /// startDashTime seconds, and a new one may only start once startDashTime
    /// has passed since the last dash started.
    /// </summary>
    private void UpdateDash()
    {
        if (dashing)
        {
            dashTime += Time.deltaTime;
            if (dashTime >= startDashTime)
            {
                dashing = false;
                dashTime = 0f;
            }
        }

        dashCooldown -= Time.deltaTime;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            StartDash();
        }
    }

    /// <summary>
    /// Starts a dash. The space bar calls this; the head-less probe calls it
    /// directly, because the old Input Manager cannot be faked in a batch mode
    /// session.
    /// </summary>
    public void StartDash()
    {
        if (!CanDash())
        {
            return;
        }

        dashing = true;
        dashTime = 0f;
        dashCooldown = startDashTime;

        // The dash goes the way the player is looking: the direction that is
        // held down right now, or the direction of the last movement.
        dashDirection = input != 0 ? Mathf.Sign(input) : facingDirection;
    }

    /// <summary>
    /// A dash may only start when no dash is running and the cooldown of the
    /// previous dash has passed.
    /// </summary>
    public bool CanDash()
    {
        return !dashing && dashCooldown <= 0f;
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        // Storing player's input
        input = ReadHorizontalInput();

        // While dashing the player keeps the dash direction, even when no
        // arrow key is held down any more.
        float direction = MoveDirection;

        // Moving player. While dashing, extraSpeed is added to the movement
        // speed.
        float currentSpeed = dashing ? speed + extraSpeed : speed;
        rb.velocity = new Vector2(direction * currentSpeed, rb.velocity.y);
    }

    /// <summary>
    /// The horizontal axis the player moves on. The keyboard is the normal
    /// source; a head-less probe can push its own value with SetInputAxis.
    /// </summary>
    public float ReadHorizontalInput()
    {
        return inputOverride.HasValue ? inputOverride.Value : Input.GetAxisRaw("Horizontal");
    }

    /// <summary>
    /// Overrides the keyboard axis. Pass null to hand the control back to the
    /// keyboard.
    /// </summary>
    public void SetInputAxis(float? axis)
    {
        inputOverride = axis;
    }

    public void TakeDamage(int damageAmount) {
        source.Play();
        health -= damageAmount;
        healthDisplay.text = health.ToString();

        if (health <= 0) {
            losePanel.SetActive(true);
            Destroy(gameObject);
        }
    }

}
