using UnityEngine;
using System.Collections;

public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance;

    [Header("Background Music")]
    public AudioClip backgroundMusic;

    [Header("Audio Settings")]
    [Range(0f, 1f)]
    public float musicVolume = 0.7f;

    [Header("Fade")]
    public float fadeDuration = 0.5f;

    private AudioSource audioSource;
    Coroutine fadeCoroutine = null;
    float targetVolume = 0.7f;

    float savedMusicVolume = -1f;   
    bool isTempFaded = false;  

    void Awake()
    {
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

        audioSource.loop = true;
        audioSource.playOnAwake = false;

        // initialize volumes
        musicVolume = Mathf.Clamp01(musicVolume);
        targetVolume = musicVolume;
        audioSource.volume = musicVolume;
    }

    void Start()
    {
        PlayMusic();
    }

    public void PlayMusic()
    {
        if (backgroundMusic == null)
        {
            Debug.LogWarning("MusicManager: backgroundMusic not assigned.");
            return;
        }

        // If audio is already playing, ensure targetVolume is correct and return
        if (audioSource.isPlaying)
        {
            targetVolume = musicVolume;
            // If currently paused (isPlaying false) but clip assigned, above check may skip — handled below
            return;
        }

        audioSource.clip = backgroundMusic;
        // Start from zero for a nice fade-in
        audioSource.volume = 0f;
        targetVolume = musicVolume;
        audioSource.Play();

        // Fade up using unscaled time so fades work even when Time.timeScale == 0
        StartFade(targetVolume, fadeDuration);
    }

    public void StopMusic()
    {
        if (!audioSource.isPlaying)
        {
            // nothing to stop
            return;
        }

        // Fade down then stop
        StartFade(0f, fadeDuration, onComplete: () =>
        {
            audioSource.Stop();
        });
    }

    public void PauseMusic()
    {
        if (!audioSource.isPlaying)
        {
            return;
        }

        StartFade(0f, fadeDuration, onComplete: () =>
        {
            audioSource.Pause();
        });
    }

    public void ResumeMusic()
    {
        // If no clip -> just start playing with fade-in
        if (audioSource.clip == null)
        {
            PlayMusic();
            return;
        }

        // If audio is already playing, ensure targetVolume is set
        if (audioSource.isPlaying)
        {
            targetVolume = musicVolume;
            StartFade(targetVolume, fadeDuration);
            return;
        }

        // If paused (not playing but has time), unpause and fade up
        audioSource.UnPause();
        // set starting volume to 0 if currently at 0 (or keep current) then fade to musicVolume
        if (audioSource.volume <= 0.0001f)
            audioSource.volume = 0f;

        targetVolume = musicVolume;
        StartFade(targetVolume, fadeDuration);
    }

    public void FadeToMultiplier(float multiplier, float duration)
    {
        if (audioSource == null) return;

        // Save original only the first time (so nested calls won't clobber original)
        if (!isTempFaded)
        {
            savedMusicVolume = musicVolume;
            isTempFaded = true;
        }

        // Calculate target as multiplier * the saved (user) volume. Clamp safe.
        float target = Mathf.Clamp01((savedMusicVolume < 0f ? musicVolume : savedMusicVolume) * multiplier);
        StartFade(target, Mathf.Max(0f, duration));
    }

    public void RestoreVolume(float duration)
    {
        if (audioSource == null) return;
        if (!isTempFaded)
        {
            // nothing to restore
            return;
        }

        float restoreTo = Mathf.Clamp01(savedMusicVolume < 0f ? musicVolume : savedMusicVolume);

        // Use StartFade with an onComplete callback to clear the temp state.
        StartFade(restoreTo, Mathf.Max(0f, duration), onComplete: () =>
        {
            isTempFaded = false;
            savedMusicVolume = -1f;
        });
    }

    public void SetVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        targetVolume = musicVolume;

        // If a fade is running, cancel it so user-set value applies immediately
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        if (audioSource != null)
            audioSource.volume = musicVolume;
    }

    void StartFade(float toVolume, float duration, System.Action onComplete = null)
    {
        if (audioSource == null)
        {
            onComplete?.Invoke();
            return;
        }

        // cancel existing fade
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        fadeCoroutine = StartCoroutine(FadeCoroutine(toVolume, duration, onComplete));
    }

    IEnumerator FadeCoroutine(float toVolume, float duration, System.Action onComplete)
    {
        float start = audioSource.volume;
        float elapsed = 0f;

        // avoid division by zero
        if (duration <= 0f)
        {
            audioSource.volume = toVolume;
            fadeCoroutine = null;
            onComplete?.Invoke();
            yield break;
        }

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            audioSource.volume = Mathf.Lerp(start, toVolume, t);
            yield return null;
        }

        audioSource.volume = toVolume;
        fadeCoroutine = null;
        onComplete?.Invoke();
    }
}
