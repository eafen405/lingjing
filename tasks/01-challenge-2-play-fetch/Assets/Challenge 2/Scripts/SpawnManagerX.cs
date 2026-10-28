using UnityEngine;

public class SpawnManagerX : MonoBehaviour
{
    public GameObject[] ballPrefabs;

    private float spawnLimitXLeft = -22;
    private float spawnLimitXRight = 7;
    private float spawnPosY = 30;

    private float startDelay = 1.0f;

    // 加分项：生成间隔在 3~5 秒之间随机
    private float minSpawnInterval = 3.0f;
    private float maxSpawnInterval = 5.0f;

    // Start is called before the first frame update
    void Start()
    {
        Invoke("SpawnRandomBall", startDelay);
    }

    // Spawn random ball at random x position at top of play area
    void SpawnRandomBall()
    {
        // 随机挑一种球
        int ballIndex = Random.Range(0, ballPrefabs.Length);
        GameObject ballPrefab = ballPrefabs[ballIndex];

        // Generate random ball index and random spawn position
        Vector3 spawnPos = new Vector3(Random.Range(spawnLimitXLeft, spawnLimitXRight), spawnPosY, 0);

        // instantiate ball at random spawn location
        Instantiate(ballPrefab, spawnPos, ballPrefab.transform.rotation);

        // InvokeRepeating 的间隔是固定的，这里用 Invoke 自己排下一次
        Invoke("SpawnRandomBall", Random.Range(minSpawnInterval, maxSpawnInterval));
    }
}
