using UnityEngine;
using System.Collections;
using TMPro;

public class Obstacle : MonoBehaviour
{
    [Header("Obstacle Properties")]
    public int health = 3;
    public int xpReward = 10;

    [Header("Hit Effects")]
    public Color hitColor = Color.red;
    public float hitFlashDuration = 0.15f;
    public float shakeIntensity = 0.1f;
    public float shakeDuration = 0.15f;

    [Header("Health Label (optional)")]
    public TextMeshPro healthText;
    public Color healthTextColor = Color.white;

    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private Vector3 originalLocalPosition;
    private bool isShaking = false;

    // Reference to movement components
    private ObstacleHorizontalMover horizontalMover;
    private ObstacleVerticalMover verticalMover;

    int initialHealth;

    Coroutine flashCoroutine;
    Coroutine shakeCoroutine;
    Coroutine dieCoroutine;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }

        originalLocalPosition = transform.localPosition;

        // Get movement components
        horizontalMover = GetComponent<ObstacleHorizontalMover>();
        verticalMover = GetComponent<ObstacleVerticalMover>();

        initialHealth = health;
        EnsureHealthText();
        UpdateHealthText();
    }

    void EnsureHealthText()
    {
        if (healthText != null) return;

        healthText = GetComponentInChildren<TextMeshPro>();
        if (healthText != null)
        {
            healthText.alignment = TextAlignmentOptions.Center;
            healthText.color = healthTextColor;
            return;
        }

        GameObject go = new GameObject("HealthText_TMP");
        go.transform.SetParent(transform, false);
        healthText = go.AddComponent<TextMeshPro>();
        healthText.alignment = TextAlignmentOptions.Center;
        healthText.color = healthTextColor;
        healthText.raycastTarget = false;
        healthText.sortingOrder = 1000;
    }

    public void TakeDamage(int damage)
    {
        if (damage <= 0) return;

        if (SFXManager.Instance != null) SFXManager.Instance.PlayProjectileHitSFX();

        health -= damage;
        health = Mathf.Max(0, health);

        UpdateHealthText();

        if (spriteRenderer != null)
        {
            if (flashCoroutine != null) StopCoroutine(flashCoroutine);
            flashCoroutine = StartCoroutine(HitColorFlash());
        }

        if (shakeCoroutine != null) StopCoroutine(shakeCoroutine);
        shakeCoroutine = StartCoroutine(HitShake());

        if (health <= 0)
        {
            if (dieCoroutine != null) StopCoroutine(dieCoroutine);
            dieCoroutine = StartCoroutine(DieSequence());
        }
    }

    void UpdateHealthText()
    {
        if (healthText == null) return;

        if (health > 0)
        {
            healthText.text = health.ToString();
        }
        else
        {
            healthText.text = "";
        }
    }

    IEnumerator HitColorFlash()
    {
        if (spriteRenderer == null) yield break;

        spriteRenderer.color = hitColor;
        yield return new WaitForSeconds(hitFlashDuration);
        spriteRenderer.color = originalColor;
        flashCoroutine = null;
    }

    IEnumerator HitShake()
    {
        if (isShaking) yield break;
        isShaking = true;

        // Get the current base position (respecting movement)
        Vector3 basePosition = GetCurrentBasePosition();

        float elapsed = 0f;
        while (elapsed < shakeDuration)
        {
            // Always shake relative to the current base position
            basePosition = GetCurrentBasePosition();
            float offsetX = Random.Range(-shakeIntensity, shakeIntensity);
            float offsetY = Random.Range(-shakeIntensity, shakeIntensity);
            transform.position = basePosition + new Vector3(offsetX, offsetY, 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Return to current base position, not the original static position
        transform.position = GetCurrentBasePosition();
        isShaking = false;
        shakeCoroutine = null;
    }

    IEnumerator DieSequence()
    {
        UpdateHealthText();
        yield return 0;
        Die();
        dieCoroutine = null;
    }

    void Die()
    {
        // Play VFX at CURRENT position (respects movement)
        Vector3 currentWorldPosition = transform.position;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddXP(xpReward);
            GameManager.Instance.PlayVFX(currentWorldPosition);
        }

        gameObject.SetActive(false);
    }

    // Helper method to get the current base position (respecting movement)
    private Vector3 GetCurrentBasePosition()
    {
        if (horizontalMover != null || verticalMover != null)
        {
            return transform.position; // This will be maintained by movement components
        }

        // For static obstacles, return the original local position in world space
        return transform.parent != null ?
            transform.parent.TransformPoint(originalLocalPosition) :
            originalLocalPosition;
    }

    public void ResetObstacle()
    {
        if (flashCoroutine != null) { StopCoroutine(flashCoroutine); flashCoroutine = null; }
        if (shakeCoroutine != null) { StopCoroutine(shakeCoroutine); shakeCoroutine = null; }
        if (dieCoroutine != null) { StopCoroutine(dieCoroutine); dieCoroutine = null; }

        health = initialHealth;

        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalColor;
        }

        // Reset to original local position - movement components will handle the rest
        transform.localPosition = originalLocalPosition;
        isShaking = false;

        EnsureHealthText();
        UpdateHealthText();
    }

    void OnValidate()
    {
        if (healthText != null)
        {
            healthText.color = healthTextColor;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!gameObject.activeInHierarchy) return;

        if (other.CompareTag("Player"))
        {
            var player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                player.TakeDamage(1);
            }
            gameObject.SetActive(false);
        }
    }
}