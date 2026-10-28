using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerControllerX : MonoBehaviour
{
    public bool gameOver;

    // Bonus: the balloon must not float above the ceiling. isLowEnough is
    // false as soon as the balloon has reached it.
    public bool isLowEnough;
    public float maxHeight = 14f;

    public float floatForce;
    private float gravityModifier = 1.5f;
    private Rigidbody playerRb;

    public ParticleSystem explosionParticle;
    public ParticleSystem fireworksParticle;

    private AudioSource playerAudio;
    public AudioClip moneySound;
    public AudioClip explodeSound;
    public AudioClip bounceSound;

    // Bonus: the ground bounce.
    private float bounceForce = 8f;

    // The balloon is slowed down near the ceiling so that it cannot shoot
    // past it in a single physics step.
    private float maxRiseSpeed = 4f;
    private float ceilingZone = 3f;

    // Start is called before the first frame update
    void Start()
    {
        Physics.gravity *= gravityModifier;
        playerRb = GetComponent<Rigidbody>();
        playerAudio = GetComponent<AudioSource>();

        // Apply a small upward force at the start of the game
        playerRb.AddForce(Vector3.up * 5, ForceMode.Impulse);

    }

    // FixedUpdate runs at a fixed rate, so the force that makes the balloon
    // float is applied once per physics step. That keeps the balloon speed
    // the same no matter how many frames the game draws.
    void FixedUpdate()
    {
        UpdateCeiling();

        // While space is pressed and player is low enough, float up
        if (Input.GetKey(KeyCode.Space) && !gameOver)
        {
            Float();
        }
    }

    /// <summary>
    /// Adds the upward force that makes the balloon float.
    ///
    /// This is public so the play mode probe can hold down the space bar in
    /// a head-less batch mode session, where the legacy Input class cannot
    /// fake a key press. The space bar binding itself is checked by
    /// tools/check-wiring.ps1 and by the build smoke test.
    /// </summary>
    public void Float()
    {
        UpdateCeiling();

        if (gameOver || !isLowEnough)
        {
            return;
        }

        playerRb.AddForce(Vector3.up * floatForce);
    }

    /// <summary>
    /// Keeps the balloon below the ceiling. While the balloon is low enough
    /// it may be pushed upwards, but not faster than maxRiseSpeed. Once it
    /// has reached the ceiling the upward movement is stopped and the balloon
    /// is held in place, so it cannot drift off the top of the screen.
    /// </summary>
    private void UpdateCeiling()
    {
        isLowEnough = transform.position.y < maxHeight;

        if (isLowEnough)
        {
            // Slow the balloon down when it approaches the ceiling, so that it
            // cannot shoot past it in a single physics step.
            if (transform.position.y > maxHeight - ceilingZone && playerRb.velocity.y > maxRiseSpeed)
            {
                playerRb.velocity = new Vector3(playerRb.velocity.x, maxRiseSpeed, playerRb.velocity.z);
            }
            return;
        }

        // At the ceiling: kill the upward movement and keep the balloon there.
        if (playerRb.velocity.y > 0f)
        {
            playerRb.velocity = new Vector3(playerRb.velocity.x, 0f, playerRb.velocity.z);
        }

        Vector3 position = transform.position;
        if (position.y > maxHeight)
        {
            position.y = maxHeight;
            transform.position = position;
            playerRb.position = position;
        }
    }

    private void OnCollisionEnter(Collision other)
    {
        // if player collides with bomb, explode and set gameOver to true
        if (other.gameObject.CompareTag("Bomb"))
        {
            explosionParticle.Play();
            playerAudio.PlayOneShot(explodeSound, 1.0f);
            gameOver = true;
            Debug.Log("Game Over!");
            Destroy(other.gameObject);
        }

        // if player collides with money, fireworks
        else if (other.gameObject.CompareTag("Money"))
        {
            // The fireworks are a child object of the player, so they have to
            // be placed on the balloon before they are played, otherwise they
            // show up next to the balloon.
            fireworksParticle.transform.position = transform.position;
            fireworksParticle.Play();
            playerAudio.PlayOneShot(moneySound, 1.0f);
            Destroy(other.gameObject);

        }

        // bonus: the balloon bounces off the ground and makes a sound
        else if (other.gameObject.CompareTag("Ground"))
        {
            // Make sure the balloon actually goes up, whatever the impact
            // speed was, and then push it off the ground.
            if (playerRb.velocity.y < 0f)
            {
                playerRb.velocity = new Vector3(playerRb.velocity.x, 0f, playerRb.velocity.z);
            }
            playerRb.AddForce(Vector3.up * bounceForce, ForceMode.Impulse);
            playerAudio.PlayOneShot(bounceSound, 1.0f);
        }

    }

}
