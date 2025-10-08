using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance;

    [Header("Audio Mixer")]
    public AudioMixer audioMixer;

    [Tooltip("Exposed parameter name for music volume (in AudioMixer)")]
    public string musicParam = "MusicVolume";

    [Tooltip("Exposed parameter name for SFX volume (in AudioMixer)")]
    public string sfxParam = "SFXVolume";

    [Header("UI (wire in Inspector)")]
    public GameObject settingsPanel;    // panel to open/close
    public Button openSettingsButton;   // optional: button that opens the panel
    public Button closeSettingsButton;  // optional: button that closes the panel

    public Slider musicSlider;        
    public Slider sfxSlider;         

    [Header("Master Mute")]
    public Button masterMuteButton;   
    public Image masterMuteIcon;      // image component inside button to swap sprites
    public Sprite masterMutedSprite;  // sprite when muted
    public Sprite masterUnmutedSprite;// sprite when unmuted

    [Header("Defaults")]
    [Range(0f, 1f)] public float defaultMusicVolume = 0.7f;
    [Range(0f, 1f)] public float defaultSFXVolume = 0.7f;
    public bool defaultMasterMuted = false;

    // PlayerPrefs keys
    const string KEY_MUSIC = "settings_music_volume";
    const string KEY_SFX = "settings_sfx_volume";
    const string KEY_MASTER_MUTED = "settings_master_muted";

    // runtime state
    bool isMasterMuted = false;

    void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        if (musicSlider != null)
        {
            musicSlider.minValue = 0f;
            musicSlider.maxValue = 1f;
            musicSlider.onValueChanged.AddListener(SetMusicVolume);
        }

        if (sfxSlider != null)
        {
            sfxSlider.minValue = 0f;
            sfxSlider.maxValue = 1f;
            sfxSlider.onValueChanged.AddListener(SetSFXVolume);
        }

        if (openSettingsButton != null)
            openSettingsButton.onClick.AddListener(OpenSettingsPanel);
        if (closeSettingsButton != null)
            closeSettingsButton.onClick.AddListener(CloseSettingsPanel);

        if (masterMuteButton != null)
            masterMuteButton.onClick.AddListener(ToggleMasterMute);

        // Load saved settings
        float savedMusic = PlayerPrefs.HasKey(KEY_MUSIC) ? PlayerPrefs.GetFloat(KEY_MUSIC) : defaultMusicVolume;
        float savedSfx = PlayerPrefs.HasKey(KEY_SFX) ? PlayerPrefs.GetFloat(KEY_SFX) : defaultSFXVolume;
        bool savedMasterMuted = PlayerPrefs.HasKey(KEY_MASTER_MUTED) ? PlayerPrefs.GetInt(KEY_MASTER_MUTED) == 1 : defaultMasterMuted;

        // Apply loaded values to UI
        if (musicSlider != null) musicSlider.value = savedMusic;
        if (sfxSlider != null) sfxSlider.value = savedSfx;

        isMasterMuted = savedMasterMuted;
        UpdateMasterMuteIcon();

        // Apply to mixer/managers
        ApplyMusicVolume(savedMusic, persist: false);
        ApplySFXVolume(savedSfx, persist: false);
    }

    public void OpenSettingsPanel()
    {
        if (settingsPanel != null) settingsPanel.SetActive(true);

        Time.timeScale = 0f;

        if (EventSystem.current != null && musicSlider != null)
            EventSystem.current.SetSelectedGameObject(musicSlider.gameObject);
    }

    public void CloseSettingsPanel()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);

        Time.timeScale = 1f;

        // Clear selected to avoid accidental reactivation
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
    }

    public void ToggleMasterMute()
    {
        SetMasterMuted(!isMasterMuted);
    }

    public void SetMasterMuted(bool muted)
    {
        isMasterMuted = muted;
        PlayerPrefs.SetInt(KEY_MASTER_MUTED, isMasterMuted ? 1 : 0);
        PlayerPrefs.Save();

        UpdateMasterMuteIcon();

        // Re-apply current slider state to ensure mixer and managers reflect master mute override.
        float currentMusic = musicSlider != null ? musicSlider.value : defaultMusicVolume;
        float currentSfx = sfxSlider != null ? sfxSlider.value : defaultSFXVolume;

        ApplyMusicVolume(currentMusic, persist: false);
        ApplySFXVolume(currentSfx, persist: false);
    }

    void UpdateMasterMuteIcon()
    {
        if (masterMuteIcon == null) return;
        masterMuteIcon.sprite = isMasterMuted ? masterMutedSprite : masterUnmutedSprite;
    }
    public void SetMusicVolume(float linear)
    {
        ApplyMusicVolume(linear, persist: true);
    }

    public void SetSFXVolume(float linear)
    {
        ApplySFXVolume(linear, persist: true);
    }

    void ApplyMusicVolume(float linear, bool persist)
    {
        // store requested volume
        float storedLinear = Mathf.Clamp01(linear);

        // effective linear (0 if master muted)
        float finalLinear = isMasterMuted ? 0f : storedLinear;

        SetMixerVolume(musicParam, finalLinear);

        // tell MusicManager the linear volume for its AudioSource(s)
        if (MusicManager.Instance != null)
            MusicManager.Instance.SetVolume(finalLinear);

        if (persist)
        {
            PlayerPrefs.SetFloat(KEY_MUSIC, storedLinear);
            PlayerPrefs.Save();
        }
    }

    void ApplySFXVolume(float linear, bool persist)
    {
        // store requested volume
        float storedLinear = Mathf.Clamp01(linear);

        // effective linear (0 if master muted)
        float finalLinear = isMasterMuted ? 0f : storedLinear;

        SetMixerVolume(sfxParam, finalLinear);

        // tell SFXManager the linear volume for PlayOneShot calls
        if (SFXManager.Instance != null)
            SFXManager.Instance.SetVolume(finalLinear);

        if (persist)
        {
            PlayerPrefs.SetFloat(KEY_SFX, storedLinear);
            PlayerPrefs.Save();
        }
    }

    void SetMixerVolume(string exposedParam, float linear)
    {
        if (audioMixer == null) return;

        // clamp
        linear = Mathf.Clamp01(linear);

        // convert to dB
        float dB;
        if (linear <= 0.0001f) dB = -80f;
        else dB = 20f * Mathf.Log10(linear);

        audioMixer.SetFloat(exposedParam, dB);
    }

    public void ResetToDefaults()
    {
        if (musicSlider != null) musicSlider.value = defaultMusicVolume;
        if (sfxSlider != null) sfxSlider.value = defaultSFXVolume;

        isMasterMuted = defaultMasterMuted;
        PlayerPrefs.SetInt(KEY_MASTER_MUTED, isMasterMuted ? 1 : 0);
        UpdateMasterMuteIcon();

        ApplyMusicVolume(defaultMusicVolume, persist: true);
        ApplySFXVolume(defaultSFXVolume, persist: true);
    }
}
