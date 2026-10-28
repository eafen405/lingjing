using UnityEngine;

public class DetectCollisionsX : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // 只有真正碰到狗的时候才销毁球
        if (other.CompareTag("Dog"))
        {
            Destroy(gameObject);
        }
    }
}
