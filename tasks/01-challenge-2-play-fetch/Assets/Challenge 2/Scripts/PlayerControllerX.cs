using UnityEngine;

public class PlayerControllerX : MonoBehaviour
{
    public GameObject dogPrefab;

    // 加分项：同一时间只允许放出一只狗，避免连按空格刷屏
    [SerializeField] private float spawnCooldown = 0.5f;
    private float nextSpawnTime;

    // Update is called once per frame
    void Update()
    {
        // On spacebar press, send dog
        if (Input.GetKeyDown(KeyCode.Space))
        {
            SpawnDog();
        }
    }

    /// <summary>Sends a dog to catch the next ball, respecting the cooldown.</summary>
    public void SpawnDog()
    {
        if (Time.time < nextSpawnTime)
        {
            return;
        }

        Instantiate(dogPrefab, transform.position, dogPrefab.transform.rotation);
        nextSpawnTime = Time.time + spawnCooldown;
    }
}
