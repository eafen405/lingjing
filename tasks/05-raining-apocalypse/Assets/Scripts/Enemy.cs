using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    public float minSpeed;
    public float maxSpeed;

    float speed;

    Player playerScript;

    public int damage;

    public GameObject explosion;

    // Start is called before the first frame update
    void Start()
    {
        speed = Random.Range(minSpeed, maxSpeed);

        // The player can already be gone when a hazard spawns, so the lookup
        // has to be guarded.
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            playerScript = playerObject.GetComponent<Player>();
        }
    }

    // Update is called once per frame
    void Update()
    {
        transform.Translate(Vector2.down * speed * Time.deltaTime);
    }

    void OnTriggerEnter2D(Collider2D hitObject)
    {

        if(hitObject.tag == "Player") {
            if (playerScript != null) {
                playerScript.TakeDamage(damage);
            }
            Instantiate(explosion, transform.position, Quaternion.identity);
            Destroy(gameObject);
        }

        if (hitObject.tag == "Ground") {
            Instantiate(explosion, transform.position, Quaternion.identity);
            Destroy(gameObject);
        }

    }


}
