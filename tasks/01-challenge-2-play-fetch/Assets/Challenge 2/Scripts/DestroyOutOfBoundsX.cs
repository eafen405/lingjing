using UnityEngine;

public class DestroyOutOfBoundsX : MonoBehaviour
{
    // 狗在世界里沿 -X 方向向左跑，跑出画面左边界后销毁
    private float leftLimit = -25f;
    // 球从画面上方落下，低于画面下边界后销毁
    private float bottomLimit = -5f;

    // Update is called once per frame
    void Update()
    {
        // 狗和球要分别判断：狗横向出界，球纵向出界
        if (CompareTag("Dog"))
        {
            if (transform.position.x < leftLimit)
            {
                Destroy(gameObject);
            }
        }
        else if (transform.position.y < bottomLimit)
        {
            // 球落到地面，结算本局
            Debug.Log("Game Over!");
            Destroy(gameObject);
        }
    }
}
