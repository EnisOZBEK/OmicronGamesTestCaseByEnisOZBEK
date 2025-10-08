using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Editor Testing (use in Editor only)")]
    public bool useEditorStartLevel = false;
    public int editorStartLevel = 0;          // 0-based index

    [Header("Levels")]
    public List<Level> levels = new List<Level>(); // populate in inspector in order (0..N-1)

    [Header("Gameplay")]
    public Transform levelStartingPoint;         

    public float levelScrollSpeed = 2f;

    [Header("Player start (optional)")]
    public Transform playerStartPoint;

    [Header("Object Pooling")]
    public int projectilePoolSize = 50;
    private List<GameObject> projectilePool;
    private Transform poolParent;
    private int currentPoolIndex = 0; // Track which projectile to use next

    [Header("VFX Pooling")]
    public GameObject deathVFXPrefab;           
    public int vfxPoolSize = 20;
    private List<GameObject> vfxPool;
    private Transform vfxPoolParent;

    [Header("UI")]
    public TMP_Text levelTextStartScreen;
    public Slider levelProgressBar;

    [Header("Level Complete UI")]
    public GameObject levelCompletePanel;        
    public Image[] levelCompleteStars;            
    public Sprite starFilled;                       
    public Sprite starBroken;                      
    public Button levelCompleteContinueButton;    

    [Header("XP / Upgrades")]
    public int xp = 0;
    public int xpThreshold = 100;                
    public Slider upgradeProgressBar; 
    public GameObject upgradePanel;

    [Header("Upgrade Buttons (assign 4 buttons in inspector)")]
    public Button[] upgradeButtons; // 0: fire rate, 1: count, 2: damage, 3: penetration
    public TMP_Text[] upgradeButtonLabels_TMP; 

    [Header("Upgrade UI Text")]
    public string maxLevelText = "Reached\nMax\nLevel!";
    public string[] upgradeButtonLabels = new string[4]
    {
        "Projectile\nFire Rate\n+%10",
        "Projectile\nNumber\n+1",
        "Projectile\nDamage\n+1",
        "Projectile\nPenetration\n+1"
    };

    [Header("Player")]
    public PlayerController player;

    // Internal state
    int currentLevelIndex = -1;
    int lastReachedIndex = 0;
    bool hasRunInitialStart = false; 

    
    int pendingNextLevel = -1;

    // Progress tracking for current level
    private float currentLevelInitialDistance = 1f;
    private bool levelProgressActive = false;


    [Header("Health UI")]
    public GameObject[] heartImages; 

    // PlayerPrefs upgrade keys
    const string KEY_UP_FIRE = "up_fire";
    const string KEY_UP_COUNT = "up_count";
    const string KEY_UP_PEN = "up_pen";
    const string KEY_UP_DMG = "up_dmg";

    // XP persistence
    const string KEY_XP = "xp";
    const string KEY_XP_THRESHOLD = "xpThreshold";

    [Header("Failed / All Complete UI")]
    public GameObject failedPanel;                 // Panel to show when player dies
    public Button failedRetryButton;               // Retry button inside failedPanel

    public Button allCompletedRestartButton;       // Restart-game button inside allCompletedPanel
    [Header("Celebration VFX")]
    public GameObject confettiPrefab; // keep existing
    public Transform confettiSpawnPoint; 





    public void UpdateHeartsUI(int currentHealth)
    {
        if (heartImages == null || heartImages.Length == 0) return;

        for (int i = 0; i < heartImages.Length; i++)
        {
            heartImages[i].SetActive(i < currentHealth);
        }
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        // Start paused until the player touches the screen
        Time.timeScale = 0f;
        hasRunInitialStart = false;

        InitializeProjectilePool();
        InitializeVFXPool();

        // ensure upgradeProgressBar configured
        if (upgradeProgressBar != null)
        {
            upgradeProgressBar.minValue = 0;
            upgradeProgressBar.maxValue = xpThreshold;
            upgradeProgressBar.value = xp;
        }

        // wire Continue button if assigned
        if (levelCompleteContinueButton != null)
        {
            levelCompleteContinueButton.onClick.RemoveAllListeners();
            levelCompleteContinueButton.onClick.AddListener(OnLevelCompleteContinue);
        }

        for (int i = 0; i < levels.Count; i++)
        {
            bool passed = PlayerPrefs.GetInt("LevelPassed_" + i, 0) == 1;
            levels[i].isPassed = passed;
            levels[i].gameObject.SetActive(false);
        }

        // load last reached level (0-based)
        lastReachedIndex = PlayerPrefs.GetInt("LastLevel", 0);
        lastReachedIndex = Mathf.Clamp(lastReachedIndex, 0, Mathf.Max(0, levels.Count - 1));

#if UNITY_EDITOR
        if (useEditorStartLevel)
        {
            editorStartLevel = Mathf.Clamp(editorStartLevel, 0, Mathf.Max(0, levels.Count - 1));
            lastReachedIndex = editorStartLevel;
        }
#endif

        // Ensure level complete panel is hidden at start
        if (levelCompletePanel != null) levelCompletePanel.SetActive(false);

        // optionally update start screen label to show current level (you can change as desired)
        if (levelTextStartScreen != null)
            levelTextStartScreen.text = "Tap To Start!";

        // --- Load persistent upgrade state & XP ---
        int savedFire = PlayerPrefs.GetInt(KEY_UP_FIRE, 0);
        int savedCount = PlayerPrefs.GetInt(KEY_UP_COUNT, 0);
        int savedPen = PlayerPrefs.GetInt(KEY_UP_PEN, 0);
        int savedDmg = PlayerPrefs.GetInt(KEY_UP_DMG, 0);
        xp = PlayerPrefs.GetInt(KEY_XP, xp);
        xpThreshold = PlayerPrefs.GetInt(KEY_XP_THRESHOLD, xpThreshold);

        // Apply loaded values to the player (player exposes a method - see next change)
        if (player != null)
        {
            player.LoadUpgradeState(savedFire, savedCount, savedPen, savedDmg);
        }
        if (upgradeProgressBar != null)
        {
            upgradeProgressBar.maxValue = xpThreshold;
            upgradeProgressBar.value = Mathf.Clamp(xp, 0, xpThreshold);
        }

        // wire Failed & AllCompleted panel buttons if assigned
        if (failedRetryButton != null)
        {
            failedRetryButton.onClick.RemoveAllListeners();
            failedRetryButton.onClick.AddListener(OnFailedRetry);
        }

        if (allCompletedRestartButton != null)
        {
            allCompletedRestartButton.onClick.RemoveAllListeners();
            allCompletedRestartButton.onClick.AddListener(OnAllCompletedRestart);
        }

        // Ensure failed panel is hidden initially
        if (failedPanel != null) failedPanel.SetActive(false);

#if UNITY_EDITOR
        // If Editor-start was requested, apply override *after* all initialization so nothing overwrites it.
        if (useEditorStartLevel)
        {
            ApplyEditorStartOverride();
        }
#endif
    }

    void InitializeProjectilePool()
    {
        projectilePool = new List<GameObject>();
        GameObject poolHolder = new GameObject("ProjectilePool");
        poolParent = poolHolder.transform;

        if (player == null || player.projectilePrefab == null)
        {
            Debug.LogWarning("Player or Projectile Prefab not assigned in GameManager. Projectile pool won't be created.");
            return;
        }

        for (int i = 0; i < projectilePoolSize; i++)
        {
            GameObject proj = Instantiate(player.projectilePrefab, poolParent);
            proj.SetActive(false);
            projectilePool.Add(proj);
        }

        currentPoolIndex = 0;
    }

    public GameObject GetPooledProjectile()
    {
        if (projectilePool == null || projectilePool.Count == 0) return null;

        // Use sequential cycling - always move to next projectile in the pool
        int attempts = 0;
        while (attempts < projectilePool.Count)
        {
            GameObject proj = projectilePool[currentPoolIndex];
            currentPoolIndex = (currentPoolIndex + 1) % projectilePool.Count;

            if (!proj.activeInHierarchy)
            {
                return proj;
            }

            attempts++;
        }

        // If exhausted, expand (warn)
        if (player != null && player.projectilePrefab != null)
        {
            Debug.LogWarning("Projectile pool exhausted! Creating additional projectile. Consider increasing pool size.");
            GameObject newProj = Instantiate(player.projectilePrefab, poolParent);
            newProj.SetActive(false);
            projectilePool.Add(newProj);
            return newProj;
        }

        return null;
    }

    // Resets upgrades/xp both in memory and in PlayerPrefs (used for Editor start and final restart)
    void ResetUpgradesAndXP(bool persistToPlayerPrefs = true)
    {
        // runtime
        xp = 0;
        xpThreshold = 100;

        if (upgradeProgressBar != null)
        {
            upgradeProgressBar.maxValue = xpThreshold;
            upgradeProgressBar.value = 0;
        }

        // reset player upgrade state in memory
        if (player != null)
        {
            player.LoadUpgradeState(0, 0, 0, 0);
            player.ResetPlayer();
            if (playerStartPoint != null) player.transform.position = playerStartPoint.position;
        }

        // deactivate pool projectiles
        DeactivateAllActiveProjectiles();

        if (persistToPlayerPrefs)
        {
            PlayerPrefs.SetInt(KEY_UP_FIRE, 0);
            PlayerPrefs.SetInt(KEY_UP_COUNT, 0);
            PlayerPrefs.SetInt(KEY_UP_PEN, 0);
            PlayerPrefs.SetInt(KEY_UP_DMG, 0);
            PlayerPrefs.SetInt(KEY_XP, xp);
            PlayerPrefs.SetInt(KEY_XP_THRESHOLD, xpThreshold);
            PlayerPrefs.Save();
        }
    }

    // Called from editor-mode start override to clear saved flags + upgrades and immediately start editor level
    void ApplyEditorStartOverride()
    {
        // Clear level-passed flags and last-level (keeps it clean)
        ClearSavedLevelPasses_EditorOnly();

        // Clear upgrades & xp now (do not require explicit UI action in editor)
        ResetUpgradesAndXP(true);

        // Ensure lastReachedIndex is editorStartLevel and persist
        editorStartLevel = Mathf.Clamp(editorStartLevel, 0, Mathf.Max(0, levels.Count - 1));
        lastReachedIndex = editorStartLevel;
        PlayerPrefs.SetInt("LastLevel", lastReachedIndex);
        PlayerPrefs.Save();

        // Deactivate all levels then start the editor level (StartLevel will ResetLevel / ResetPlayer)
        for (int i = 0; i < levels.Count; i++)
        {
            if (levels[i] != null) levels[i].gameObject.SetActive(false);
        }

        // Start that level and unpause immediately so editor test begins
        StartLevel(editorStartLevel);
        Time.timeScale = 1f;
        hasRunInitialStart = true;
    }

    void InitializeVFXPool()
    {
        vfxPool = new List<GameObject>();
        GameObject poolHolder = new GameObject("VFXPool");
        vfxPoolParent = poolHolder.transform;

        if (deathVFXPrefab == null)
        {
            Debug.LogWarning("deathVFXPrefab not assigned to GameManager. VFX pool won't be created.");
            return;
        }

        for (int i = 0; i < vfxPoolSize; i++)
        {
            GameObject fx = Instantiate(deathVFXPrefab, vfxPoolParent);
            fx.SetActive(false);
            vfxPool.Add(fx);
        }
    }

    GameObject GetPooledVFX()
    {
        if (vfxPool == null || vfxPool.Count == 0) return null;

        for (int i = 0; i < vfxPool.Count; i++)
        {
            if (!vfxPool[i].activeInHierarchy)
            {
                return vfxPool[i];
            }
        }

        // Expand if none available
        if (deathVFXPrefab != null)
        {
            Debug.LogWarning("VFX pool exhausted! Instantiating extra VFX. Consider increasing vfxPoolSize.");
            GameObject fx = Instantiate(deathVFXPrefab, vfxPoolParent);
            fx.SetActive(false);
            vfxPool.Add(fx);
            return fx;
        }

        return null;
    }

    public void PlayVFX(Vector3 pos)
    {
        if (deathVFXPrefab == null)
        {
            Debug.LogWarning("PlayVFX called but deathVFXPrefab is not assigned.");
            return;
        }

        GameObject fx = GetPooledVFX();
        if (fx == null)
        {
            Debug.LogWarning("No pooled VFX available.");
            return;
        }

        fx.transform.position = pos;
        fx.transform.rotation = Quaternion.identity;
        fx.SetActive(true);

        // If it has a ParticleSystem, play and schedule deactivation after its lifetime
        ParticleSystem ps = fx.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            ps.Clear();
            ps.Play();

            // estimate total lifetime: duration + startLifetime (handles const or range)
            float totalLifetime = ps.main.duration;
            var sl = ps.main.startLifetime;
            if (sl.mode == ParticleSystemCurveMode.Constant)
                totalLifetime += sl.constant;
            else
                totalLifetime += sl.constantMax;

            StartCoroutine(DisableVFXAfter(fx, totalLifetime + 0.05f));
            return;
        }

        // If it has child ParticleSystems, find the longest and schedule disable accordingly
        ParticleSystem[] childPS = fx.GetComponentsInChildren<ParticleSystem>();
        if (childPS != null && childPS.Length > 0)
        {
            float longest = 0f;
            foreach (var cps in childPS)
            {
                cps.Clear();
                cps.Play();
                float t = cps.main.duration;
                var sl = cps.main.startLifetime;
                if (sl.mode == ParticleSystemCurveMode.Constant) t += sl.constant;
                else t += sl.constantMax;
                if (t > longest) longest = t;
            }
            StartCoroutine(DisableVFXAfter(fx, longest + 0.05f));
            return;
        }

        // Fallback: deactivate after 1 second
        StartCoroutine(DisableVFXAfter(fx, 1f));
    }

    IEnumerator DisableVFXAfter(GameObject fx, float time)
    {
        yield return new WaitForSeconds(time);
        if (fx != null) fx.SetActive(false);
    }

    void Update()
    {
        if (Time.timeScale == 0f && !hasRunInitialStart)
        {
            if (IsFirstInputDetected())
            {
                hasRunInitialStart = true; // Prevent this from running again
                BeginGameFromInput();
            }
        }

        // If the game is running, scroll the active level and update progress
        if (Time.timeScale > 0)
        {
            ScrollLevel();
            UpdateLevelProgress();
        }
    }

    void UpdateLevelProgress()
    {
        if (!levelProgressActive || levelProgressBar == null) return;
        if (currentLevelIndex < 0 || currentLevelIndex >= levels.Count) return;

        Level lvl = levels[currentLevelIndex];
        if (lvl == null || lvl.levelEndTrigger == null || playerStartPoint == null)
        {
            // disable if missing
            levelProgressBar.gameObject.SetActive(false);
            levelProgressActive = false;
            return;
        }

        // vertical-only distance (world positions). If the level moves downward, endTrigger.y approaches playerStartPoint.y.
        float currentDist = Mathf.Abs(lvl.levelEndTrigger.position.y - playerStartPoint.position.y);

        // compute progress: 0 at start, 1 at finish (or if distance reduced to zero)
        float progress = 1f - (currentDist / currentLevelInitialDistance);
        progress = Mathf.Clamp01(progress);

        levelProgressBar.value = progress;
    }


    void ScrollLevel()
    {
        if (currentLevelIndex >= 0 && currentLevelIndex < levels.Count)
        {
            Level activeLevel = levels[currentLevelIndex];
            if (activeLevel != null && activeLevel.gameObject.activeInHierarchy)
            {
                activeLevel.transform.Translate(Vector3.down * levelScrollSpeed * Time.deltaTime, Space.World);
            }
        }
    }

    bool IsFirstInputDetected()
    {
        // Touch (mobile)
        if (Input.touchCount > 0)
        {
            Touch t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began)
            {
                return true;
            }
        }

        // Mouse (Editor / desktop)
        if (Input.GetMouseButtonDown(0))
        {
            return true;
        }

        return false;
    }

    void BeginGameFromInput()
    {
        // Unpause
        Time.timeScale = 1f;

        // Determine start index (lastReachedIndex or next unpassed)
        int start = FindFirstUnpassedLevel(lastReachedIndex);
        if (start != -1)
        {
            StartLevel(start);
        }
        else
        {
            Debug.Log("All levels passed! Nothing to start.");
            Time.timeScale = 0f;
            hasRunInitialStart = false;
        }
    }

    int FindFirstUnpassedLevel(int startIndex)
    {
        if (levels.Count == 0) return -1;
        startIndex = Mathf.Clamp(startIndex, 0, levels.Count - 1);

        // If the saved level is not passed, start there.
        if (!levels[startIndex].isPassed) return startIndex;

        // Otherwise, find the next unpassed level after the startIndex
        for (int i = startIndex + 1; i < levels.Count; i++)
            if (!levels[i].isPassed) return i;

        // If none found, check from the beginning
        for (int i = 0; i < startIndex; i++)
            if (!levels[i].isPassed) return i;

        // If all levels are passed
        return -1;
    }

    public void StartLevel(int levelIndex)
    {
        if (levelIndex < 0 || levelIndex >= levels.Count)
        {
            Debug.LogWarning("StartLevel: invalid index " + levelIndex);
            return;
        }

        if (levelStartingPoint == null)
        {
            Debug.LogError("GameManager: levelStartingPoint not assigned!");
            return;
        }

        // Deactivate all other levels so only one is active
        for (int i = 0; i < levels.Count; i++)
        {
            levels[i].gameObject.SetActive(false);
        }

        // Activate and position the chosen level root at LevelStartingPoint
        Level chosen = levels[levelIndex];
        chosen.gameObject.SetActive(true);
        chosen.transform.position = levelStartingPoint.position;
        chosen.transform.rotation = levelStartingPoint.rotation;

        // Reset only children/local states
        chosen.ResetLevel();

        // place player at configured start point for levels (if assigned)
        if (player != null && playerStartPoint != null)
        {
            player.transform.position = playerStartPoint.position;
        }

        // Reset player state (health etc.) but do NOT move player transform
        if (player != null)
        {
            player.ResetPlayer();
        }

        currentLevelIndex = levelIndex;
        lastReachedIndex = levelIndex;
        PlayerPrefs.SetInt("LastLevel", lastReachedIndex);
        PlayerPrefs.Save();

        if (levelTextStartScreen != null)
            levelTextStartScreen.text = "Level " + (lastReachedIndex + 1);

        Debug.Log("Started at Level " + (levelIndex + 1));

        levelProgressActive = false;
        if (levelProgressBar != null && playerStartPoint != null)
        {
            // use the chosen level's end trigger if assigned
            if (chosen.levelEndTrigger != null)
            {
                // vertical-only distances
                float initDist = Mathf.Abs(chosen.levelEndTrigger.position.y - playerStartPoint.position.y);
                if (initDist <= Mathf.Epsilon) initDist = 1f; // avoid div-by-zero
                currentLevelInitialDistance = initDist;

                levelProgressBar.minValue = 0f;
                levelProgressBar.maxValue = 1f;
                levelProgressBar.value = 0f;
                levelProgressBar.gameObject.SetActive(true);
                levelProgressActive = true;
            }
            else
            {
                // no trigger assigned -> hide/disable the progress bar
                levelProgressBar.gameObject.SetActive(false);
                levelProgressActive = false;
            }
        }
        else if (levelProgressBar != null)
        {
            // no player start assigned -> hide slider
            levelProgressBar.gameObject.SetActive(false);
            levelProgressActive = false;
        }
    }

    public void OnLevelPassed(int levelIndex)
    {
        if (levelIndex < 0 || levelIndex >= levels.Count) return;
        if (levels[levelIndex].isPassed) return;

        // Mark passed and save
        levels[levelIndex].isPassed = true;
        PlayerPrefs.SetInt("LevelPassed_" + levelIndex, 1);
        PlayerPrefs.Save();

        // Deactivate this level now that it's passed
        levels[levelIndex].gameObject.SetActive(false);
        Debug.Log("Level " + (levelIndex + 1) + " passed.");

        // Determine next level index (first unpassed after current)
        int next = FindFirstUnpassedLevel(levelIndex + 1);

        if (next != -1)
        {
            // There's a next unpassed level -> normal level complete flow
            pendingNextLevel = next;
            ShowLevelCompletePanel(levelIndex, false);
        }
        else
        {
            // This was the last level -> show level-complete panel but in "final" mode
            pendingNextLevel = -1;
            ShowLevelCompletePanel(levelIndex, true);
        }

        // Move player to hold point (if assigned) so the panel shows with player in that position
        if (playerStartPoint != null && player != null)
        {
            player.transform.position = playerStartPoint.position;
        }
        else if (player != null)
        {
            // if no hold point assigned, keep player where they are or optionally move to origin
        }

        // Deactivate any active pooled projectiles so no stray shots remain
        if (projectilePool != null)
        {
            foreach (var proj in projectilePool)
            {
                if (proj != null && proj.activeInHierarchy)
                {
                    proj.SetActive(false);
                    // if projectile has lastDeactivateTime field update it (so pooling cooldown ok)
                    var p = proj.GetComponent<Projectile>();
                    if (p != null) p.lastDeactivateTime = Time.time;
                }
            }
        }


    }

    // Called by the AllCompletedPanel Restart button - wipes progress/upgrades/xp and restarts fresh
    // Called by the AllCompleted (final-level restart) Restart button - wipes progress/upgrades/xp and restarts fresh
    public void OnAllCompletedRestart()
    {
        // Stop gameplay
        Time.timeScale = 0f;
        hasRunInitialStart = false;

        // Clear all level passed flags
        for (int i = 0; i < levels.Count; i++)
        {
            PlayerPrefs.DeleteKey("LevelPassed_" + i);
            if (levels[i] != null)
            {
                levels[i].isPassed = false;
                // Reset root position and children to original authoring states
                levels[i].ResetRootPosition();
                levels[i].ResetLevel();
                levels[i].gameObject.SetActive(false);
            }
        }

        // Clear last level and upgrades / xp
        PlayerPrefs.DeleteKey("LastLevel");
        PlayerPrefs.DeleteKey(KEY_UP_FIRE);
        PlayerPrefs.DeleteKey(KEY_UP_COUNT);
        PlayerPrefs.DeleteKey(KEY_UP_PEN);
        PlayerPrefs.DeleteKey(KEY_UP_DMG);
        PlayerPrefs.DeleteKey(KEY_XP);
        PlayerPrefs.DeleteKey(KEY_XP_THRESHOLD);
        PlayerPrefs.Save();

        // Reset runtime values
        xp = 0;
        xpThreshold = 100;
        if (upgradeProgressBar != null)
        {
            upgradeProgressBar.maxValue = xpThreshold;
            upgradeProgressBar.value = 0;
        }

        // Reset player's upgrade state in memory
        if (player != null)
        {
            player.LoadUpgradeState(0, 0, 0, 0);
            player.ResetPlayer(); // restore health etc
                                  // place player at configured playerStartPoint if assigned
            if (playerStartPoint != null) player.transform.position = playerStartPoint.position;
        }

        // Deactivate pool projectiles
        DeactivateAllActiveProjectiles();

        // Reset runtime pointer to first level
        lastReachedIndex = 0;
        PlayerPrefs.Save();

        // Update start screen prompt
        if (levelTextStartScreen != null)
            levelTextStartScreen.text = "Tap To Start!";
    }

    void ShowLevelCompletePanel(int completedLevelIndex, bool isFinalLevel = false)
    {
        // Pause gameplay
        Time.timeScale = 0f;

        // Update star images based on player's health (if player exists)
        int starsToShow = 0;
        if (player != null)
        {
            int maxStars = (levelCompleteStars != null) ? levelCompleteStars.Length : 3;
            starsToShow = Mathf.Clamp(player.currentHealth, 0, maxStars);
        }

        if (levelCompleteStars != null && levelCompleteStars.Length > 0)
        {
            for (int i = 0; i < levelCompleteStars.Length; i++)
            {
                if (levelCompleteStars[i] == null) continue;
                levelCompleteStars[i].sprite = (i < starsToShow) ? starFilled : starBroken;
            }
        }

        // Play level complete SFX if available
        if (SFXManager.Instance != null) SFXManager.Instance.PlayLevelCompleteSFX();

        // Configure Continue button: when final -> act as Restart; otherwise -> Continue
        if (levelCompleteContinueButton != null)
        {
            // set label if there's a TMP child
            var tmp = levelCompleteContinueButton.GetComponentInChildren<TMP_Text>();
            if (tmp != null) tmp.text = isFinalLevel ? "Restart" : "Continue";

            levelCompleteContinueButton.onClick.RemoveAllListeners();
            if (isFinalLevel)
            {
                levelCompleteContinueButton.onClick.AddListener(OnLevelCompleteRestart);
            }
            else
            {
                levelCompleteContinueButton.onClick.AddListener(OnLevelCompleteContinue);
            }
        }

        // Play confetti if this was the last level
        if (isFinalLevel && confettiPrefab != null)
        {
            Vector3 pos;
            if (confettiSpawnPoint != null)
                pos = confettiSpawnPoint.position;
            else
                pos = (player != null) ? player.transform.position :
                      (levelStartingPoint != null ? levelStartingPoint.position : Vector3.zero);
            PlayConfettiAt(pos);
        }


        // Show panel
        if (levelCompletePanel != null) levelCompletePanel.SetActive(true);

        // Update optional start label
        if (levelTextStartScreen != null)
            levelTextStartScreen.text = "Tap To Start!";
    }

    void PlayConfettiAt(Vector3 position)
    {
        if (confettiPrefab == null) return;

        GameObject go = Instantiate(confettiPrefab, position, Quaternion.identity);

        // Ensure every ParticleSystem in the prefab uses unscaled time, so it will play while Time.timeScale == 0
        var systems = go.GetComponentsInChildren<ParticleSystem>(true);
        float maxLifetime = 0f;
        foreach (var ps in systems)
        {
            var main = ps.main;
            // set useUnscaledTime so particle simulation ignores Time.timeScale
            main.useUnscaledTime = true;

            // start playing
            ps.Play();

            float dur = main.duration;
            var sl = main.startLifetime;
            float lifeAdd = (sl.mode == ParticleSystemCurveMode.Constant) ? sl.constant : sl.constantMax;
            float total = dur + lifeAdd;
            if (total > maxLifetime) maxLifetime = total;
        }

        // If the prefab had no ParticleSystem, just play and destroy after a fallback
        if (systems == null || systems.Length == 0)
        {
            Destroy(go, 5f);
        }
        else
        {
            Destroy(go, maxLifetime + 0.2f);
        }
    }

    // Called when the Level Complete panel is shown for the final level and player presses Restart
    public void OnLevelCompleteRestart()
    {
        // Hide level complete panel
        if (levelCompletePanel != null) levelCompletePanel.SetActive(false);

        // Reuse your existing full-reset logic (same as AllCompleted Restart)
        OnAllCompletedRestart();
    }

    // Called by the Continue button on the Level Complete panel
    public void OnLevelCompleteContinue()
    {
        // Hide level complete panel
        if (levelCompletePanel != null) levelCompletePanel.SetActive(false);

        // Store pending next level as LastLevel so BeginGameFromInput will start from it
        if (pendingNextLevel >= 0)
        {
            lastReachedIndex = pendingNextLevel;
            PlayerPrefs.SetInt("LastLevel", lastReachedIndex);
            PlayerPrefs.Save();
        }

        // Reset pending
        pendingNextLevel = -1;

        // Prepare to wait for tap to start next level
        hasRunInitialStart = false;
        Time.timeScale = 0f;

        // Update UI prompt (optional)
        if (levelTextStartScreen != null)
            levelTextStartScreen.text = "Tap To Start!";
    }

    public void OnPlayerDie()
    {
        // Play fail SFX
        if (SFXManager.Instance != null) SFXManager.Instance.PlayLevelFailedSFX();

        // Save the last level so the player resumes there next session
        PlayerPrefs.SetInt("LastLevel", currentLevelIndex);
        PlayerPrefs.Save();
        Debug.Log($"Player died in Level {currentLevelIndex + 1}. They will resume from this level next time.");

        // Pause game and show failed UI. Do NOT clear upgrades/xp here (user wanted to keep them).
        Time.timeScale = 0f;
        hasRunInitialStart = false;

        UpdateHeartsUI(0);

        if (failedPanel != null)
        {
            failedPanel.SetActive(true);
        }
    }

    // Deactivate any active pooled projectiles safely and update their lastDeactivateTime
    void DeactivateAllActiveProjectiles()
    {
        if (projectilePool == null) return;

        foreach (var proj in projectilePool)
        {
            if (proj == null) continue;
            if (proj.activeInHierarchy)
            {
                // update lastDeactivateTime if projectile has field
                var p = proj.GetComponent<Projectile>();
                if (p != null) p.lastDeactivateTime = Time.time;

                proj.SetActive(false);
                // Optionally reset transform
                proj.transform.position = Vector3.zero;
            }
        }
    }

    // Disable (or deactivate) obstacle GameObjects in the currently active level
    void DeactivateAllObstaclesInCurrentLevel()
    {
        if (currentLevelIndex < 0 || currentLevelIndex >= levels.Count) return;
        Level lvl = levels[currentLevelIndex];
        if (lvl == null) return;

        // find Obstacle components among children
        var obstacles = lvl.GetComponentsInChildren<Obstacle>(true);
        foreach (var obs in obstacles)
        {
            if (obs == null) continue;
            // deactivate obstacle GameObject so it won't trigger until level restart
            obs.gameObject.SetActive(false);
        }
    }

    // Called by FailedPanel Retry button
    public void OnFailedRetry()
    {
        // Hide failed UI
        if (failedPanel != null) failedPanel.SetActive(false);

        // Clear active projectiles so no stray bullets from previous attempt
        DeactivateAllActiveProjectiles();

        // Reset the current level to its initial authored state and restart
        if (currentLevelIndex >= 0 && currentLevelIndex < levels.Count)
        {
            // safe: StartLevel deactivates other levels, resets chosen's children and resets player
            StartLevel(currentLevelIndex);

            // Start playing immediately
            Time.timeScale = 1f;
            hasRunInitialStart = true;
        }
        else
        {
            // fallback: start first available unpassed or 0
            int start = FindFirstUnpassedLevel(lastReachedIndex);
            if (start == -1) start = 0;
            StartLevel(start);
            Time.timeScale = 1f;
            hasRunInitialStart = true;
        }
    }


    // ---------------- XP & Upgrade logic ----------------

    public void AddXP(int amount)
    {
        xp += amount;
        if (upgradeProgressBar != null) upgradeProgressBar.value = Mathf.Clamp(xp, 0, xpThreshold);

        // Persist xp and threshold so progress survives sessions
        PlayerPrefs.SetInt(KEY_XP, xp);
        PlayerPrefs.SetInt(KEY_XP_THRESHOLD, xpThreshold);
        PlayerPrefs.Save();

        if (xp >= xpThreshold)
        {
            PauseForUpgrade();
        }
    }


    void PauseForUpgrade()
    {
        // Update buttons before showing panel
        UpdateUpgradeButtonsUI();

        // fade music to 50% of user's current music level (use MusicManager.fadeDuration if available)
        if (MusicManager.Instance != null)
        {
            float dur = MusicManager.Instance != null ? MusicManager.Instance.fadeDuration : 0.5f;
            MusicManager.Instance.FadeToMultiplier(0.5f, dur);
        }

        // pause gameplay and show UI
        Time.timeScale = 0f;
        if (upgradePanel != null) upgradePanel.SetActive(true);
    }

    // Called by upgrade UI buttons (argument: 0..3)
    public void OnUpgradePicked(int optionIndex)
    {
        if (player == null)
        {
            Debug.LogWarning("OnUpgradePicked called but player missing");
            return;
        }

        bool applied = false;

        switch (optionIndex)
        {
            case 0: // Fire Rate +10% of base (additive), max 10
                applied = player.TryApplyFireRateUpgrade();
                if (!applied) Debug.Log("Fire rate upgrade maxed.");
                break;

            case 1: // Projectile Count +1 (max 2 upgrades)
                applied = player.TryApplyProjectileCountUpgrade();
                if (!applied) Debug.Log("Projectile count upgrade maxed.");
                break;

            case 2: // Projectile Damage +1 (no limit)
                applied = player.TryApplyProjectileDamageUpgrade();
                break;

            case 3: // Projectile Penetration +1 (max 4 upgrades)
                applied = player.TryApplyPenetrationUpgrade();
                if (!applied) Debug.Log("Penetration upgrade maxed.");
                break;

            default:
                Debug.LogWarning("Unknown upgrade index: " + optionIndex);
                break;
        }

        if (applied)
        {
            SFXManager.Instance.PlayUpgradeSFX();

            // Successful upgrade: reset xp and double xpThreshold
            xp = 0;
            xpThreshold = Mathf.Max(1, Mathf.RoundToInt(xpThreshold * 1.5f));
            if (upgradeProgressBar != null)
            {
                upgradeProgressBar.maxValue = xpThreshold;
                upgradeProgressBar.value = 0;
            }

            // Hide panel and restore music
            if (upgradePanel != null) upgradePanel.SetActive(false);

            // restore music to user's level with a fade
            if (MusicManager.Instance != null)
            {
                float dur = MusicManager.Instance != null ? MusicManager.Instance.fadeDuration : 0.5f;
                // restore BEFORE or AFTER unpausing is fine — fades use unscaled time.
                MusicManager.Instance.RestoreVolume(dur);
            }

            // Persist upgrades & xp state immediately
            PlayerPrefs.SetInt(KEY_UP_FIRE, player.GetFireRateUpgradeCount());
            PlayerPrefs.SetInt(KEY_UP_COUNT, player.GetProjectileCountUpgradeCount());
            PlayerPrefs.SetInt(KEY_UP_PEN, player.GetPenetrationUpgradeCount());
            PlayerPrefs.SetInt(KEY_UP_DMG, player.GetDamageUpgradeCount());

            PlayerPrefs.SetInt(KEY_XP, xp);
            PlayerPrefs.SetInt(KEY_XP_THRESHOLD, xpThreshold);

            PlayerPrefs.Save();

            // resume game
            Time.timeScale = 1f;

            Debug.Log("Upgrade applied. New xpThreshold: " + xpThreshold);
        }
        else
        {
            // If not applied (limit reached), set that button to max visually so player can't click it again
            ForceButtonToMax(optionIndex);
            // Keep panel open so player can pick another option
            UpdateUpgradeButtonsUI();
        }
    }

    // Update upgrade buttons' interactable state and label text according to player's current upgrade counts
    void UpdateUpgradeButtonsUI()
    {
        if (upgradeButtons == null || upgradeButtons.Length < 4 || player == null || upgradeButtonLabels_TMP == null || upgradeButtonLabels_TMP.Length < 4) return;

        for (int i = 0; i < 4; i++)
        {
            Button b = upgradeButtons[i];
            TMP_Text tmp = upgradeButtonLabels_TMP[i];
            if (b == null || tmp == null) continue;

            bool isMax = false;
            switch (i)
            {
                case 0: isMax = player.GetFireRateUpgradeCount() >= player.maxFireRateUpgrades; break;
                case 1: isMax = player.GetProjectileCountUpgradeCount() >= player.maxProjectileCountUpgrades; break;
                case 2: isMax = false; break; // damage has no max
                case 3: isMax = player.GetPenetrationUpgradeCount() >= player.maxPenetrationUpgrades; break;
            }

            // Set the text and interactable state
            tmp.text = isMax ? maxLevelText : upgradeButtonLabels[i];
            b.interactable = !isMax;
        }
    }

    // Force a specific button into maxed visual state (useful when a click fails due to limit)
    void ForceButtonToMax(int index)
    {
        if (upgradeButtons == null || index < 0 || index >= upgradeButtons.Length || upgradeButtonLabels_TMP == null || index >= upgradeButtonLabels_TMP.Length) return;

        Button b = upgradeButtons[index];
        TMP_Text tmp = upgradeButtonLabels_TMP[index];

        if (b != null && tmp != null)
        {
            tmp.text = maxLevelText;
            b.interactable = false;
        }
    }



    [ContextMenu("ResetAllProgress")]
    public void ResetAllProgress()
    {
        for (int i = 0; i < levels.Count; i++)
        {
            PlayerPrefs.DeleteKey("LevelPassed_" + i);
            levels[i].isPassed = false;
            if (levels[i].gameObject != null) levels[i].gameObject.SetActive(true);
            levels[i].ResetRootPosition();
            levels[i].ResetLevel();
        }

        PlayerPrefs.DeleteKey(KEY_UP_FIRE);
        PlayerPrefs.DeleteKey(KEY_UP_COUNT);
        PlayerPrefs.DeleteKey(KEY_UP_PEN);
        PlayerPrefs.DeleteKey(KEY_UP_DMG);
        PlayerPrefs.DeleteKey(KEY_XP);
        PlayerPrefs.DeleteKey(KEY_XP_THRESHOLD);
        PlayerPrefs.Save();

        // Also reset runtime values
        xp = 0;
        xpThreshold = 100;

        PlayerPrefs.DeleteKey("LastLevel");
        PlayerPrefs.DeleteKey("xp");
        PlayerPrefs.Save();
        xp = 0;
        xpThreshold = 100;
        if (upgradeProgressBar != null)
        {
            upgradeProgressBar.maxValue = xpThreshold;
            upgradeProgressBar.value = 0;
        }
        lastReachedIndex = 0;
        if (levelTextStartScreen != null) levelTextStartScreen.text = "Level 1";

        // Also reset upgrade panel buttons back to defaults / interactable
        if (upgradeButtons != null)
        {
            for (int i = 0; i < upgradeButtons.Length; i++)
            {
                if (i >= upgradeButtonLabels_TMP.Length) break; // Safety check

                Button b = upgradeButtons[i];
                TMP_Text tmp = upgradeButtonLabels_TMP[i];

                if (b != null && tmp != null)
                {
                    if (i < upgradeButtonLabels.Length)
                    {
                        tmp.text = upgradeButtonLabels[i];
                    }
                    b.interactable = true;
                }
            }
        }

        Debug.Log("Progress reset.");
    }

#if UNITY_EDITOR
    void ClearSavedLevelPasses_EditorOnly()
    {
        for (int i = 0; i < levels.Count; i++)
        {
            PlayerPrefs.DeleteKey("LevelPassed_" + i);
        }
        PlayerPrefs.DeleteKey("LastLevel");
        PlayerPrefs.Save();
        Debug.Log("Editor start override: cleared saved level passes and LastLevel.");
    }
#endif
}
