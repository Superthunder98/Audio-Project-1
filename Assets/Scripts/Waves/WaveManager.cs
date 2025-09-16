using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using System.Linq;

/*
 * WaveManager.cs
 * 
 * Purpose: Controls zombie wave spawning and management during night cycles
 * Used by: Game progression system, zombie spawning, night cycle events
 * 
 * Key Features:
 * - Week/Night/Wave hierarchy system
 * - Time-based wave spawning
 * - Dynamic zombie difficulty scaling
 * - Objective system integration
 * - UI status updates
 * - Night cycle synchronization
 * 
 * Wave System Structure:
 * - Weeks contain multiple nights
 * - Nights contain multiple waves
 * - Waves control:
 *   - Zombie count and types
 *   - Spawn timing and locations
 *   - Difficulty multipliers
 * 
 * Performance Considerations:
 * - Coroutine-based wave management
 * - Efficient zombie spawning
 * - Smart state tracking
 * - Event-driven updates
 * 
 * Dependencies:
 * - DayNightCycle system
 * - ObjectiveManager
 * - UIAudioManager
 * - AnimationEventHandler
 * - Requires configured wave data
 * - TextMeshPro for UI elements
 * 
 * Debug Features:
 * - Detailed logging
 * - State monitoring
 * - Wave progression tracking
 * - Zombie count verification
 */

#pragma warning disable 0414 // delayTime is for future use
public class WaveManager : MonoBehaviour
{
    /// <summary>
    /// Represents a single wave of zombies with specific properties and behaviors
    /// </summary>
    [System.Serializable]
    public class Wave
    {
        [Tooltip("Name of this wave for organization")]
        public string waveName = "Wave";
        
        [Tooltip("Total number of zombies that will spawn in this wave")]
        public int numberOfZombies = 5;
        
        [Tooltip("Time between individual zombie spawns within the wave")]
        public float spawnDelay = 2f;
        
        [Tooltip("Delay from night start until this wave begins (in seconds)")]
        public float timeToStart = 0f;
        
        [Tooltip("Array of different zombie types that can spawn in this wave")]
        public GameObject[] zombiePrefabs;
        
        [Tooltip("Modifies zombie movement speed (1 = normal, 2 = double speed)")]
        public float zombieSpeedMultiplier = 1f;
        
        [Tooltip("Modifies zombie health (1 = normal, 2 = double health)")]
        public float healthMultiplier = 1f;
        
        [Tooltip("Possible locations where zombies can spawn")]
        public Transform[] waveSpawnPoints;
    }

    /// <summary>
    /// Represents a single night of waves with progressive difficulty
    /// </summary>
    [System.Serializable]
    public class Night
    {
        [Tooltip("Identifier for this night")]
        public string nightName = "Night";
        
        [Tooltip("Sequential waves that occur during this night")]
        public Wave[] waves;
        
        [Tooltip("Editor-only: Controls expansion in inspector")]
        public bool isExpanded;
    }

    /// <summary>
    /// Represents a week of nights with escalating challenges
    /// </summary>
    [System.Serializable]
    public class Week
    {
        [Tooltip("Identifier for this week")]
        public string weekName = "Week";
        
        [Tooltip("Five nights of progressive difficulty")]
        public Night[] nights = new Night[5];
        
        [Tooltip("Editor-only: Controls expansion in inspector")]
        public bool isExpanded;
    }

    [Header("Wave Settings")]
    [Tooltip("Weeks of waves configuration")]
    [SerializeField] private Week[] weeks;
    [SerializeField, HideInInspector] private bool autoStartWaves = true; // Always true, hidden in inspector

    [Header("UI Elements")]
    [Tooltip("Text displaying current week number")]
    [SerializeField] private TextMeshProUGUI weekText;
    [Tooltip("Text displaying current night number")]
    [SerializeField] private TextMeshProUGUI nightText;
    [Tooltip("Text displaying current wave number")]
    [SerializeField] private TextMeshProUGUI waveText;
    [Tooltip("Text displaying number of zombies remaining")]
    [SerializeField] private TextMeshProUGUI zombieCountText;

    [Header("Day/Night Cycle")]
    [Tooltip("Reference to the DayNightCycle component")]
    [SerializeField] private DayNightCycle dayNightCycle;

    [Header("Wave Completed Animation")]
    [Tooltip("Animator for wave completion effects")]
    [SerializeField] private Animator waveCompletedAnimator;
    [Tooltip("Animator parameter for wave completion")]
    [SerializeField] private string waveCompletedBoolName = "WaveCompleted";
    [Tooltip("Duration to display wave completion animation")]
    [SerializeField] private float waveCompletedDisplayTime = 3f;
    [Tooltip("UI element to animate on wave completion")]
    [SerializeField] private GameObject animatedUIElement;
    [Tooltip("Delay before starting wave completion animation")]
    [SerializeField] private float delayTime = 2f;

    [Header("Night End Settings")]
    [Tooltip("Time of day to set when zombies are cleared (0.1 = early dawn)")]
    [SerializeField, Range(0f, 1f)] private float zombieDefenseTimeTillDawn = 0.1f;

    [Header("Wave Completion UI")]
    [SerializeField] private GameObject waveCompletionUI;

    private int currentWeekIndex = 0;
    private int currentNightIndex = 0;
    private int currentWaveIndex = -1;
    private int zombiesAlive = 0;
    private int zombiesSpawned = 0;
    private bool isWaveActive = false;
    private Wave currentWave;
    private bool isNightStarted = false;
    private float nightTimer = 0f;
    private List<int> pendingWaves = new List<int>();
    private Coroutine waveManagementCoroutine;
    private Coroutine currentWaveSpawningCoroutine;
    private Coroutine monitorCoroutine;
    private bool hasZombieDefenseObjective = false;
    private int totalZombiesForNight = 0; // Track total zombies across all waves
    private const float ZOMBIE_SPAWN_START_TIME = 0.868f;

    /// <summary>
    /// Initializes wave system and subscribes to necessary events
    /// </summary>
    private void Start()
    {
        if (dayNightCycle == null)
        {
            dayNightCycle = FindFirstObjectByType<DayNightCycle>();
            if (dayNightCycle == null)
            {
                //Debug.LogError("No DayNightCycle found in the scene!");
                return;
            }
        }

        // Subscribe to day changes
        dayNightCycle.OnDayChanged += HandleDayChanged;

        // Initialize week/night based on current day
        UpdateWeekAndNightFromDay(dayNightCycle.GetCurrentDay());

        dayNightCycle.OnNightStateChanged += HandleNightStateChanged;

        if (weeks == null || weeks.Length == 0)
        {
           // Debug.LogError("No weeks configured in WaveManager!");
            return;
        }

        StartMonitoring();
    }

    private void UpdateWeekAndNightFromDay(int day)
    {
        // Convert from 1-based day to 0-based index
        int dayIndex = day - 1;
        
        // Calculate week and night indices
        currentWeekIndex = dayIndex / 5;  // Integer division for week number
        currentNightIndex = dayIndex % 5;  // Remainder for night within week
        
       // Debug.Log($"[WaveManager] Day {day} converted to: Week {currentWeekIndex + 1}, Night {currentNightIndex + 1}");
      //  Debug.Log($"[WaveManager] Raw values - dayIndex: {dayIndex}, weekIndex: {currentWeekIndex}, nightIndex: {currentNightIndex}");
        
        // Validate indices
        if (currentWeekIndex >= weeks.Length)
        {
            Debug.LogError($"[WaveManager] Week index {currentWeekIndex} is out of range! Max weeks: {weeks.Length}");
        }
        else if (currentNightIndex >= weeks[currentWeekIndex].nights.Length)
        {
            Debug.LogError($"[WaveManager] Night index {currentNightIndex} is out of range! Max nights in week {currentWeekIndex + 1}: {weeks[currentWeekIndex].nights.Length}");
        }
        
        UpdateUI();
    }

    private void HandleDayChanged(int newDay)
    {
       // Debug.Log($"[WaveManager] Day changed to: {newDay}");
        UpdateWeekAndNightFromDay(newDay);
    }

    private void HandleNightStateChanged(bool isNight)
    {
        if (isNight)
        {
            // Remove OnNightStart();
        }
        else
        {
            // Remove OnNightEnd();
        }
    }

    private void OnDestroy()
    {
        if (dayNightCycle != null)
        {
            dayNightCycle.OnNightStateChanged -= HandleNightStateChanged;
            dayNightCycle.OnDayChanged -= HandleDayChanged;
        }
    }
    private void OnDisable()
    {
        if (dayNightCycle != null)
        {
            dayNightCycle.OnNightStateChanged -= HandleNightStateChanged;
        }

        if (monitorCoroutine != null)
        {
            StopCoroutine(monitorCoroutine);
            monitorCoroutine = null;
        }
    }

    private void StartMonitoring()
    {
        if (autoStartWaves && monitorCoroutine == null)
        {
            monitorCoroutine = StartCoroutine(MonitorDayNightCycle());
        }
    }

    /// <summary>
    /// Monitors day/night cycle to trigger wave events at appropriate times
    /// Manages zombie defense objective states
    /// </summary>
    private IEnumerator MonitorDayNightCycle()
    {
        yield return new WaitForSeconds(1f);
        
        while (true)
        {
            if (dayNightCycle == null)
            {
                Debug.LogError("DayNightCycle reference lost!");
                yield break;
            }

            float currentTime = dayNightCycle.GetTimeOfDay();
            
            if (currentTime >= ZOMBIE_SPAWN_START_TIME && !isNightStarted)
            {
                isNightStarted = true;
                StartNight();  // Initialize waves first

                // Only add the objective if waves were successfully initialized
                if (zombiesAlive > 0 && pendingWaves.Count > 0)
                {
                    if (ObjectiveManager.Instance != null)
                    {
                        ObjectiveManager.Instance.AddObjective(ObjectiveConstants.ZOMBIE_DEFENCE);
                        hasZombieDefenseObjective = true;
//                        Debug.Log($"[ZombieDefense] Added objective for night. Zombies: {zombiesAlive}");
                    }
                }
            }
            else if (currentTime < ZOMBIE_SPAWN_START_TIME && isNightStarted)
            {
                isNightStarted = false;
                EndNight();
            }
            
            yield return new WaitForSeconds(0.5f);
        }
    }

    /// <summary>
    /// Begins a night of waves, calculating total zombies and initializing wave tracking
    /// </summary>
    private void StartNight()
    {
        if (currentWeekIndex >= weeks.Length)
        {
            //Debug.LogError($"Week index {currentWeekIndex} is out of range");
            return;
        }

        Week currentWeek = weeks[currentWeekIndex];
        Night currentNight = currentWeek.nights[currentNightIndex];

        // Add debug logging for night initialization
        //Debug.Log($"Starting Week {currentWeekIndex + 1}, Night {currentNightIndex + 1}");
        //Debug.Log($"Number of waves for this night: {currentNight.waves.Length}");
        foreach (var wave in currentNight.waves)
        {
           // Debug.Log($"Wave '{wave.waveName}' spawn points: {string.Join(", ", wave.waveSpawnPoints.Select(sp => sp.name))}");
        }

        totalZombiesForNight = 0;
        foreach (Wave wave in currentNight.waves)
        {
            totalZombiesForNight += wave.numberOfZombies;
        }
        zombiesAlive = totalZombiesForNight;
//        Debug.Log($"[ZombieDefense] Night starting with {totalZombiesForNight} zombies to defeat");

        nightTimer = 0f;
        pendingWaves.Clear();

        for (int i = 0; i < currentNight.waves.Length; i++)
        {
            pendingWaves.Add(i);
        }

        UpdateUI();
        waveManagementCoroutine = StartCoroutine(ManageNightWaves());
    }

    private void StartWave(Wave wave, int waveIndex)
    {
        currentWaveIndex = waveIndex;
        currentWave = wave;
        zombiesSpawned = 0;
        UpdateUI();
        currentWaveSpawningCoroutine = StartCoroutine(SpawnWaveZombies());
    }

    /// <summary>
    /// Manages the progression of waves throughout the night
    /// Controls wave timing and sequencing
    /// </summary>
    private IEnumerator ManageNightWaves()
    {
        Week currentWeek = weeks[currentWeekIndex];
        Night currentNight = currentWeek.nights[currentNightIndex];
        
        while (isNightStarted && pendingWaves.Count > 0)
        {
            nightTimer += Time.deltaTime;

            // Only check for new waves if we're not currently spawning
            if (!isWaveActive)
            {
                for (int i = pendingWaves.Count - 1; i >= 0; i--)
                {
                    int waveIndex = pendingWaves[i];
                    Wave wave = currentNight.waves[waveIndex];

                    if (nightTimer >= wave.timeToStart)
                    {
                        StartWave(wave, waveIndex);
                        pendingWaves.RemoveAt(i);
                        break; // Only start one wave at a time
                    }
                }
            }

            yield return null;
        }
    }

    private void EndNight()
    {
        if (zombiesAlive > 0)
        {
            return;
        }

        // Only reset hasZombieDefenseObjective if the objective was completed
        if (ObjectiveManager.Instance != null && 
            ObjectiveManager.Instance.IsObjectiveCompleted(ObjectiveConstants.ZOMBIE_DEFENCE))
        {
            hasZombieDefenseObjective = false;
        }
        
        if (currentWaveSpawningCoroutine != null)
        {
            StopCoroutine(currentWaveSpawningCoroutine);
            currentWaveSpawningCoroutine = null;
        }

        if (waveManagementCoroutine != null)
        {
            StopCoroutine(waveManagementCoroutine);
            waveManagementCoroutine = null;
        }

        // Clear wave state
        currentWaveIndex = -1;
        isWaveActive = false;
        zombiesAlive = 0;
        zombiesSpawned = 0;
        pendingWaves.Clear();
        nightTimer = 0f;
    }

    /// <summary>
    /// Handles individual zombie spawning with configured modifiers
    /// Applies wave-specific settings to each zombie
    /// </summary>
    private void SpawnZombie()
    {
        if (currentWave == null)
        {
           // Debug.LogError("No current wave set!");
            return;
        }

        if (currentWave.waveSpawnPoints == null || currentWave.waveSpawnPoints.Length == 0)
        {
           // Debug.LogError($"Wave '{currentWave.waveName}' has no spawn points assigned!");
            return;
        }

        if (currentWave.zombiePrefabs == null || currentWave.zombiePrefabs.Length == 0)
        {
           // Debug.LogError($"Wave '{currentWave.waveName}' has no zombie prefabs assigned!");
            return;
        }

        // Add debug logging for spawn point selection
        Transform spawnPoint = currentWave.waveSpawnPoints[Random.Range(0, currentWave.waveSpawnPoints.Length)];
       // Debug.Log($"[Wave {currentWaveIndex}] Spawning zombie at {spawnPoint.name} (position: {spawnPoint.position})");
       // Debug.Log($"Current Week: {currentWeekIndex + 1}, Night: {currentNightIndex + 1}");
       // Debug.Log($"Available spawn points for this wave: {string.Join(", ", currentWave.waveSpawnPoints.Select(sp => sp.name))}");

        GameObject zombiePrefab = currentWave.zombiePrefabs[Random.Range(0, currentWave.zombiePrefabs.Length)];
        GameObject zombie = Instantiate(zombiePrefab, spawnPoint.position, spawnPoint.rotation);
        zombie.tag = "Enemy";
        
        // Just ensure AudioSource exists if not already on prefab
        AudioSource audioSource = zombie.GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = zombie.AddComponent<AudioSource>();
        }
        
        ZombieAI zombieAI = zombie.GetComponent<ZombieAI>();
        if (zombieAI == null)
        {
          //  Debug.LogError($"Zombie prefab '{zombiePrefab.name}' is missing ZombieAI component!");
            return;
        }
        
        zombieAI.SetSpeedMultiplier(currentWave.zombieSpeedMultiplier);
        zombieAI.SetHealthMultiplier(currentWave.healthMultiplier);
        zombieAI.OnZombieDeath += HandleZombieDeath;
    }

    /// <summary>
    /// Manages the spawning sequence for a specific wave
    /// Controls timing and progression of zombie spawns
    /// </summary>
    private IEnumerator SpawnWaveZombies()
    {
        isWaveActive = true;
        int zombiesToSpawn = currentWave.numberOfZombies;
        
        for (int i = 0; i < zombiesToSpawn; i++)
        {
            SpawnZombie();
            zombiesSpawned++;
            UpdateUI();
            
            if (i < zombiesToSpawn - 1)  // Don't wait after spawning the last zombie
            {
                yield return new WaitForSeconds(currentWave.spawnDelay);
            }
        }

        isWaveActive = false;
    }

    /// <summary>
    /// Processes zombie death events and checks for wave/night completion
    /// Updates UI and manages objective completion
    /// </summary>
    private void HandleZombieDeath()
    {
        zombiesAlive--;
        UpdateUI();
       // Debug.Log($"[ZombieDefense] Zombie killed. {zombiesAlive} remaining of {totalZombiesForNight}");

        if (hasZombieDefenseObjective)
        {
            // Add debug logging for each condition
            //Debug.Log($"[ZombieDefense] Checking completion conditions:");
            //Debug.Log($"[ZombieDefense] - zombiesAlive <= 0: {zombiesAlive <= 0}");
            //Debug.Log($"[ZombieDefense] - pendingWaves.Count == 0: {pendingWaves.Count == 0}");
            //Debug.Log($"[ZombieDefense] - !isWaveActive: {!isWaveActive}");
            //Debug.Log($"[ZombieDefense] - hasZombieDefenseObjective: {hasZombieDefenseObjective}");
            //Debug.Log($"[ZombieDefense] - IsObjectiveCompleted: {ObjectiveManager.Instance?.IsObjectiveCompleted(ObjectiveConstants.ZOMBIE_DEFENCE)}");

            if (zombiesAlive <= 0 && pendingWaves.Count == 0 && !isWaveActive)
            {
                if (ObjectiveManager.Instance != null && 
                    !ObjectiveManager.Instance.IsObjectiveCompleted(ObjectiveConstants.ZOMBIE_DEFENCE))
                {
                    float currentTime = dayNightCycle.GetTimeOfDay();
                    Debug.Log($"[ZombieDefense] All zombies cleared at time {currentTime:F3}! Showing completion UI");
                    AnimationEventHandler.Instance?.ShowEnemiesClearedAnnouncement();
                    ObjectiveManager.Instance.CompleteObjective(ObjectiveConstants.ZOMBIE_DEFENCE);
                    hasZombieDefenseObjective = false;
                    
                    // Resume time when enemies are cleared
                    dayNightCycle.ResumeTime();
                    dayNightCycle.SetTimeSpeedMultiplier(1f);
                    dayNightCycle.SetTimeOfDay(zombieDefenseTimeTillDawn);
                }
            }
        }
    }

    private void UpdateUI()
    {
        if (weekText != null)
            weekText.text = $"Week {currentWeekIndex + 1}";
        
        if (nightText != null)
            nightText.text = $"Night {currentNightIndex + 1}";
        
        if (waveText != null)
            waveText.text = $"Wave {currentWaveIndex + 1}";
        
        if (zombieCountText != null)
            zombieCountText.text = $"Zombies Remaining: {zombiesAlive} of {totalZombiesForNight}";
    }

    private void OnEnable()
    {
        StartMonitoring();
    }

    public void SetZombieDefenseActive()
    {
        hasZombieDefenseObjective = true;
       // Debug.Log("Zombie Defense objective is now active");
    }

    // Add these public methods to expose debug information
    public int GetTotalZombiesForNight() => totalZombiesForNight;
    public int GetZombiesAlive() => zombiesAlive;
    public int GetPendingWavesCount() => pendingWaves.Count;
    public bool IsWaveActive() => isWaveActive;
    public int GetCurrentWaveIndex() => currentWaveIndex;
    public bool IsNightStarted() => isNightStarted;
    public bool HasZombieDefenseObjective() => hasZombieDefenseObjective;

    // Add this method to be called by the animation event
    public void PlayClearedSound()
    {
        if (UIAudioManager.Instance != null)
        {
            UIAudioManager.Instance.PlayAllEnemiesCleared();
        }
    }

    public void ResetZombieDefenseState()
    {
        hasZombieDefenseObjective = false;
        isNightStarted = false;
        zombiesAlive = 0;
        pendingWaves.Clear();
        isWaveActive = false;
       //Debug.Log("[ZombieDefense] State reset");
    }
}