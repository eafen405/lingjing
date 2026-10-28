using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerControllerX : MonoBehaviour
{
    private Rigidbody playerRb;
    private float speed = 500;
    private GameObject focalPoint;

    public bool hasPowerup;
    public GameObject powerupIndicator;
    public int powerUpDuration = 5;

    private float normalStrength = 10; // how hard to hit enemy without powerup
    private float powerupStrength = 25; // how hard to hit enemy with powerup

    // Bonus: the turbo boost. The smoke effect hangs under the focal point.
    public GameObject dashParticleEffect;
    public float dashStrength = 200;

    void Start()
    {
        playerRb = GetComponent<Rigidbody>();
        focalPoint = GameObject.Find("Focal Point");

        if (dashParticleEffect == null && focalPoint != null)
        {
            ParticleSystem particles = focalPoint.GetComponentInChildren<ParticleSystem>(true);
            if (particles != null)
            {
                dashParticleEffect = particles.gameObject;
            }
        }
    }

    void Update()
    {
        // Add force to player in direction of the focal point (and camera)
        float verticalInput = Input.GetAxis("Vertical");
        playerRb.AddForce(focalPoint.transform.forward * verticalInput * speed * Time.deltaTime);

        // Bonus: turbo boost on the space bar
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Dash();
        }

        // Set powerup indicator position to beneath player
        powerupIndicator.transform.position = transform.position + new Vector3(0, -0.6f, 0);

    }

    // Bonus: the turbo boost shoves the player towards the focal point and
    // kicks off the smoke effect underneath it.
    public void Dash()
    {
        // A sleeping player would ignore the impulse, so wake it up first.
        playerRb.WakeUp();
        playerRb.AddForce(focalPoint.transform.forward * dashStrength, ForceMode.Impulse);

        if (dashParticleEffect != null)
        {
            ParticleSystem particles = dashParticleEffect.GetComponent<ParticleSystem>();
            if (particles != null)
            {
                particles.Play();
            }
        }
    }

    // If Player collides with powerup, activate powerup
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Powerup"))
        {
            Destroy(other.gameObject);
            hasPowerup = true;
            powerupIndicator.SetActive(true);
            StartCoroutine(PowerupCooldown());
        }
    }

    // Coroutine to count down powerup duration
    IEnumerator PowerupCooldown()
    {
        yield return new WaitForSeconds(powerUpDuration);
        hasPowerup = false;
        powerupIndicator.SetActive(false);
    }

    // If Player collides with enemy
    private void OnCollisionEnter(Collision other)
    {
        if (other.gameObject.CompareTag("Enemy"))
        {
            Rigidbody enemyRigidbody = other.gameObject.GetComponent<Rigidbody>();
            Vector3 awayFromPlayer = other.gameObject.transform.position - transform.position;
           
            if (hasPowerup) // if have powerup hit enemy with powerup force
            {
                enemyRigidbody.AddForce(awayFromPlayer * powerupStrength, ForceMode.Impulse);
            }
            else // if no powerup, hit enemy with normal strength 
            {
                enemyRigidbody.AddForce(awayFromPlayer * normalStrength, ForceMode.Impulse);
            }


        }
    }



}
