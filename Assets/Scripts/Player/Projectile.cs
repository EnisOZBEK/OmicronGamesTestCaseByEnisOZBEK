using UnityEngine;
using System.Collections;

public class Projectile : MonoBehaviour
{
    [Header("Damage / Health")]
    public int damage = 1;
    public int projectileMaxHealth = 1;
    int projectileCurrentHealth;

    [Header("Motion / Lifetime")]
    public float speed = 10f;
    public float lifeTime = 3f;

    [Header("Visual Effects")]
    public TrailRenderer trailRenderer;

    Coroutine lifeCoroutine;
    bool isDeactivating = false;

    [System.NonSerialized] public float lastDeactivateTime = -100f;

    public void Initialize(int damageValue, int maxHealth)
    {
        damage = damageValue;
        projectileMaxHealth = Mathf.Max(1, maxHealth);
        projectileCurrentHealth = projectileMaxHealth;
    }

    void OnEnable()
    {
        projectileCurrentHealth = Mathf.Max(1, projectileMaxHealth);
        isDeactivating = false;

        // Reset trail immediately when enabled
        if (trailRenderer != null)
        {
            trailRenderer.Clear();
        }

        if (lifeCoroutine != null) StopCoroutine(lifeCoroutine);
        lifeCoroutine = StartCoroutine(DeactivateAfterTime(lifeTime));
    }

    IEnumerator DeactivateAfterTime(float time)
    {
        yield return new WaitForSeconds(time);

        if (!isDeactivating)
        {
            StartCoroutine(DeactivateWithTrailCleanup());
        }
    }

    void OnDisable()
    {
        if (lifeCoroutine != null)
        {
            StopCoroutine(lifeCoroutine);
            lifeCoroutine = null;
        }
        isDeactivating = false;
    }

    void Update()
    {
        transform.position += -transform.up * speed * Time.deltaTime;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!gameObject.activeInHierarchy || isDeactivating) return;

        if (other.CompareTag("Obstacle"))
        {
            var obs = other.GetComponent<Obstacle>();
            if (obs != null)
            {
                obs.TakeDamage(damage);
            }

            projectileCurrentHealth -= 1;

            if (projectileCurrentHealth <= 0 && !isDeactivating)
            {
                StartCoroutine(DeactivateWithTrailCleanup());
            }
        }
    }

    IEnumerator DeactivateWithTrailCleanup()
    {
        if (isDeactivating) yield break;

        isDeactivating = true;

        // Wait for trail to fully disappear
        float trailFadeTime = 0f;
        if (trailRenderer != null)
        {
            trailFadeTime = trailRenderer.time;
            yield return new WaitForSeconds(trailFadeTime);
        }

        // Record deactivation time
        lastDeactivateTime = Time.time;

        // Now safely deactivate
        gameObject.SetActive(false);
        transform.position = Vector3.zero;
    }
}