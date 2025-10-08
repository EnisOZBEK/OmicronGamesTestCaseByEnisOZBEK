using UnityEngine;

public class SFXManager : MonoBehaviour
{
    public static SFXManager Instance;

    [Header("Shooting SFX")]
    public AudioClip[] shootingSFX; // Array of shooting sounds

    [Header("Projectile Hit SFX")]
    public AudioClip[] projectileHitSFX; // Array of projectile hitting obstacle sounds

    [Header("Player Hit SFX")]
    public AudioClip playerHitSFX; // When player gets hit

    [Header("Upgrade SFX")]
    public AudioClip upgradeSFX; // When upgraded.

    [Header("Level Complete SFX")]
    public AudioClip levelCompleteSFX; // When level is completed

    [Header("Level Failed SFX")]
    public AudioClip levelFailedSFX; // When player dies

    [Header("SFX Settings")]
    [Range(0f, 1f)]
    public float sfxVolume = 0.7f;

    private AudioSource audioSource;

    void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Setup AudioSource
        audioSource = gameObject.GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.volume = sfxVolume;
    }

    // Shooting SFX - randomly picks from array
    public void PlayShootingSFX()
    {
        if (shootingSFX != null && shootingSFX.Length > 0)
        {
            int randomIndex = Random.Range(0, shootingSFX.Length);
            audioSource.PlayOneShot(shootingSFX[randomIndex], sfxVolume);
        }
    }

    // Projectile Hit SFX - randomly picks from array
    public void PlayProjectileHitSFX()
    {
        if (projectileHitSFX != null && projectileHitSFX.Length > 0)
        {
            int randomIndex = Random.Range(0, projectileHitSFX.Length);
            audioSource.PlayOneShot(projectileHitSFX[randomIndex], sfxVolume);
        }
    }

    // Player Hit SFX
    public void PlayPlayerHitSFX()
    {
        if (playerHitSFX != null)
        {
            audioSource.PlayOneShot(playerHitSFX, sfxVolume);
        }
    }

    // Upgrade SFX
    public void PlayUpgradeSFX()
    {
        if (upgradeSFX != null)
        {
            audioSource.PlayOneShot(upgradeSFX, sfxVolume);
        }
    }

    // Level Complete SFX
    public void PlayLevelCompleteSFX()
    {
        if (levelCompleteSFX != null)
        {
            audioSource.PlayOneShot(levelCompleteSFX, sfxVolume);
        }
    }

    // Level Failed SFX
    public void PlayLevelFailedSFX()
    {
        if (levelFailedSFX != null)
        {
            audioSource.PlayOneShot(levelFailedSFX, sfxVolume);
        }
    }

    // Volume control
    public void SetVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        audioSource.volume = sfxVolume;
    }

    // Optional: Stop all SFX
    public void StopAllSFX()
    {
        audioSource.Stop();
    }
}