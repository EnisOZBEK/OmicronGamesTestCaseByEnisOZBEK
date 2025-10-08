using UnityEngine;
using System.Collections;

[RequireComponent(typeof(SpriteRenderer))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float horizontalSpeed = 12f;
    public float clampX = 2.5f;
    public float smoothing = 10f;

    [Header("Shooting")]
    public Transform firePoint;
    public GameObject projectilePrefab;
    public float fireRate = 0.2f; // inspector interval (seconds between shots)

    [Header("Shoot Animation")]
    public float shootScaleAmount = 0.85f; // Scale down to 85% when shooting
    public float shootScaleDuration = 0.1f; // How long the scale animation takes

    // Upgradeable projectile properties (defaults)
    [Header("Projectile (upgradeable)")]
    public int projectileCount = 1;          // base projectile count
    public float projectileSpread = 0.35f;   // horizontal offset between projectiles
    public int projectileDamage = 1;         // base damage
    public int projectilePenetration = 1;    // base penetration (health)

    // Upgrade counters & limits
    [Header("Upgrade limits")]
    public int maxFireRateUpgrades = 10;     // fire rate upgrades max (adds up to +10 of base 10%)
    public int maxProjectileCountUpgrades = 2; // max +2 => 1 -> 3
    public int maxPenetrationUpgrades = 4;   // max +4 => 1 -> 5

    // internal counters
    int fireRateUpgradeCount = 0;
    int projectileCountUpgradeCount = 0;
    int penetrationUpgradeCount = 0;
    int damageUpgradeCount = 0; // not limited

    float baseShotsPerSecond;   
    float currentShotsPerSecond;

    [Header("Health")]
    public int maxHealth = 3;
    public float invincibleDuration = 3f;

    [HideInInspector] public int currentHealth;
    [HideInInspector] public bool isInvincible = false;

    SpriteRenderer sr;
    float targetX;
    bool isDragging = false;
    float nextFireTime = 0f;
    bool isShooting = false;
    Vector3 originalScale; // Store original scale for shoot animation

    // caching base counts for additive upgrades
    int baseProjectileCount;
    int basePenetration;
    int baseDamage;

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        currentHealth = maxHealth;
        targetX = transform.position.x;
        originalScale = transform.localScale; // Store original scale

        // compute base shots-per-second from the inspector fireRate interval
        baseShotsPerSecond = Mathf.Approximately(fireRate, 0f) ? 1f : (1f / fireRate);
        currentShotsPerSecond = baseShotsPerSecond;

        // cache base counts
        baseProjectileCount = Mathf.Max(1, projectileCount);
        basePenetration = Mathf.Max(1, projectilePenetration);
        baseDamage = Mathf.Max(1, projectileDamage);

        // calculate clamp if desired
        if (Camera.main != null)
        {
            float screenWidth = Camera.main.orthographicSize * Camera.main.aspect;
            clampX = screenWidth * 0.7f;
        }
    }

    void Update()
    {
        if (Time.timeScale == 0f) return;

        HandleInput();
        SmoothMove();

        if (isShooting && Time.time >= nextFireTime)
        {
            Shoot();
            nextFireTime = Time.time + (1f / currentShotsPerSecond);
        }
    }

    public void ResetPlayer()
    {
        currentHealth = maxHealth;
        isInvincible = false;
        if (sr != null) sr.enabled = true;
        targetX = transform.position.x;
        if (GameManager.Instance != null) GameManager.Instance.UpdateHeartsUI(currentHealth);

        // place player at this level's start point (optional)
        if (GameManager.Instance.playerStartPoint!= null && GameManager.Instance.player != null)
        {
            GameManager.Instance.player.transform.position = GameManager.Instance.playerStartPoint.position;
        }
    }

    void HandleInput()
    {
#if UNITY_EDITOR
        if (Input.GetMouseButton(0))
        {
            Vector3 wp = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            isDragging = true;
            targetX = Mathf.Clamp(wp.x, -clampX, clampX);
            isShooting = true;
        }
        else if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
            isShooting = false;
        }
#endif

        if (Input.touchCount > 0)
        {
            Touch t = Input.GetTouch(0);
            Vector3 wp = Camera.main.ScreenToWorldPoint(t.position);
            if (t.phase == TouchPhase.Began || t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary)
            {
                isDragging = true;
                targetX = Mathf.Clamp(wp.x, -clampX, clampX);
                isShooting = true;
            }
            else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
            {
                isDragging = false;
                isShooting = false;
            }
        }

        if (Input.touchCount == 0 && !Input.GetMouseButton(0))
        {
            isDragging = false;
            isShooting = false;
        }
    }

    void SmoothMove()
    {
        Vector3 pos = transform.position;
        float newX = Mathf.Lerp(pos.x, targetX, Time.deltaTime * smoothing);
        transform.position = new Vector3(newX, pos.y, pos.z);
    }

    void Shoot()
    {
        if (firePoint == null || GameManager.Instance == null) return;

        SFXManager.Instance.PlayShootingSFX();
        // Trigger shoot scale animation
        StartCoroutine(ShootScaleAnimation());

        // compute current projectile count and penetration from base + upgrades
        int currentProjectileCount = baseProjectileCount + projectileCountUpgradeCount;
        int currentPenetration = basePenetration + penetrationUpgradeCount;
        int currentDamage = baseDamage + damageUpgradeCount;

        float half = (currentProjectileCount - 1) * 0.5f;
        for (int i = 0; i < currentProjectileCount; i++)
        {
            float offset = (i - half) * projectileSpread;
            Vector3 spawnPos = firePoint.position + (firePoint.right * offset);

            // Get a projectile from the pool
            GameObject go = GameManager.Instance.GetPooledProjectile();
            if (go == null) continue;

            //Set position and rotation BEFORE activating**
            go.transform.position = spawnPos;
            go.transform.rotation = projectilePrefab.transform.rotation;

            // this will trigger OnEnable and reset the trail
            go.SetActive(true);

            Projectile p = go.GetComponent<Projectile>();
            if (p != null)
            {
                p.Initialize(currentDamage, currentPenetration);
            }
        }
    }

    IEnumerator ShootScaleAnimation()
    {
        // Scale down quickly
        float elapsed = 0f;
        Vector3 startScale = transform.localScale;
        Vector3 targetScale = originalScale * shootScaleAmount;

        while (elapsed < shootScaleDuration / 2f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (shootScaleDuration / 2f);
            transform.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null;
        }

        // Scale back to original
        elapsed = 0f;
        startScale = transform.localScale;
        while (elapsed < shootScaleDuration / 2f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (shootScaleDuration / 2f);
            transform.localScale = Vector3.Lerp(startScale, originalScale, t);
            yield return null;
        }

        transform.localScale = originalScale; // Ensure we end at exact original scale
    }

    public bool TryApplyFireRateUpgrade()
    {
        if (fireRateUpgradeCount >= maxFireRateUpgrades) return false;
        fireRateUpgradeCount++;

        // apply additive +10% of base shots-per-second per upgrade
        currentShotsPerSecond = baseShotsPerSecond * (1f + 0.1f * fireRateUpgradeCount);
        return true;
    }

    // Projectile Count upgrade: +1 projectile per upgrade, max maxProjectileCountUpgrades
    public bool TryApplyProjectileCountUpgrade()
    {
        if (projectileCountUpgradeCount >= maxProjectileCountUpgrades) return false;
        projectileCountUpgradeCount++;
        return true;
    }

    // Projectile Damage upgrade: unlimited
    public bool TryApplyProjectileDamageUpgrade()
    {
        damageUpgradeCount++;
        return true;
    }

    // Penetration upgrade: +1 max health per upgrade, max maxPenetrationUpgrades
    public bool TryApplyPenetrationUpgrade()
    {
        if (penetrationUpgradeCount >= maxPenetrationUpgrades) return false;
        penetrationUpgradeCount++;
        return true;
    }

    // Damage-taking API called by obstacles, etc.
    public void TakeDamage(int dmg = 1)
    {
        if (isInvincible) return;

        SFXManager.Instance.PlayPlayerHitSFX();

        currentHealth -= dmg;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.UpdateHeartsUI(currentHealth);
        }

        if (currentHealth <= 0)
        {
            if (GameManager.Instance != null) GameManager.Instance.OnPlayerDie();
        }
        else
        {
            StartCoroutine(InvincibilityFlash());
        }
    }

    IEnumerator InvincibilityFlash()
    {
        isInvincible = true;
        float elapsed = 0f;
        while (elapsed < invincibleDuration)
        {
            sr.enabled = !sr.enabled;
            yield return new WaitForSeconds(0.15f);
            elapsed += 0.15f;
        }
        sr.enabled = true;
        isInvincible = false;
    }

    // Call this to restore upgrade counts loaded from PlayerPrefs
    public void LoadUpgradeState(int fireRateCount, int projectileCountUp, int penetrationCount, int damageCount)
    {
        // internal fields names in your script: fireRateUpgradeCount, projectileCountUpgradeCount, penetrationUpgradeCount, damageUpgradeCount
        fireRateUpgradeCount = Mathf.Clamp(fireRateCount, 0, maxFireRateUpgrades);
        projectileCountUpgradeCount = Mathf.Clamp(projectileCountUp, 0, maxProjectileCountUpgrades);
        penetrationUpgradeCount = Mathf.Clamp(penetrationCount, 0, maxPenetrationUpgrades);
        damageUpgradeCount = Mathf.Max(0, damageCount);

        // Recompute derived values
        baseShotsPerSecond = Mathf.Approximately(fireRate, 0f) ? 1f : (1f / fireRate);
        currentShotsPerSecond = baseShotsPerSecond * (1f + 0.1f * fireRateUpgradeCount);

        // enforce minimums on cache values (in case inspector changed)
        baseProjectileCount = Mathf.Max(1, projectileCount);
        basePenetration = Mathf.Max(1, projectilePenetration);
        baseDamage = Mathf.Max(1, projectileDamage);
    }


    // Optional getters to display current upgrade counts / values in UI
    public int GetFireRateUpgradeCount() => fireRateUpgradeCount;
    public int GetProjectileCountUpgradeCount() => projectileCountUpgradeCount;
    public int GetPenetrationUpgradeCount() => penetrationUpgradeCount;
    public int GetDamageUpgradeCount() => damageUpgradeCount;
}