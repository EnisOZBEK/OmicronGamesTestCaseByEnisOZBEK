using UnityEngine;

public class LevelEndTrigger : MonoBehaviour
{
    public int levelIndex;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        GameManager.Instance.OnLevelPassed(levelIndex);
    }
}
