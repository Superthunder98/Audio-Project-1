using UnityEngine;
using UnityStandardAssets.Characters.FirstPerson;
using System.Collections;

/*
 * GameTesting.cs
 * 
 * Purpose: Development tool for testing game mechanics and systems.
 * Used by: Development team during testing
 * 
 * Provides shortcuts and utilities for testing various game systems,
 * including inventory, objectives, and player stats. Should be disabled
 * in production builds.
 * 
 * Features:
 * - Quick item addition
 * - Objective completion
 * - Stats modification
 * - Time control
 * 
 * Dependencies:
 * - Inventory system
 * - ObjectiveManager
 * - PlayerStats
 * - DayNightCycle
 */

public class GameTesting : MonoBehaviour
{
    [Header("Test Items")]
    [Tooltip("List of items to add to the player's inventory for testing purposes")]
    [SerializeField] private Item[] testItems;

    [Header("Test Objectives - Set 1")]
    [Tooltip("List of objective IDs to mark as completed for testing purposes (Set 1)")]
    [SerializeField] private string[] testObjectiveIds;
    
    [Header("Test Objectives - Set 2")]
    [Tooltip("List of objective IDs to mark as completed for testing purposes (Set 2)")]
    [SerializeField] private string[] secondaryTestObjectiveIds;
    
    [Space(10)]
    [Tooltip("Delay between completing each objective (in seconds)")]
    [SerializeField] private float objectiveCompletionDelay = 0.5f;

    [Header("Item Shortcut Settings")]
    [Tooltip("Modifier key required to be held down for adding items (e.g., Left Shift)")]
    [SerializeField] private KeyCode itemModifierKey = KeyCode.LeftShift;
    
    [Tooltip("Key to press while holding the modifier to add items (e.g., I)")]
    [SerializeField] private KeyCode itemTriggerKey = KeyCode.I;

    [Header("Primary Objective Shortcut Settings")]
    [Tooltip("Modifier key required to be held down for completing objectives Set 1 (e.g., Left Control)")]
    [SerializeField] private KeyCode objectiveModifierKey = KeyCode.LeftControl;
    
    [Tooltip("Key to press while holding the modifier to complete objectives Set 1 (e.g., O)")]
    [SerializeField] private KeyCode objectiveTriggerKey = KeyCode.O;

    [Header("Secondary Objective Shortcut Settings")]
    [Space(5)]
    [Tooltip("Modifier key required to be held down for completing objectives Set 2 (e.g., Right Control)")]
    [SerializeField] private KeyCode secondaryObjectiveModifierKey = KeyCode.RightControl;
    
    [Tooltip("Key to press while holding the modifier to complete objectives Set 2 (e.g., P)")]
    [SerializeField] private KeyCode secondaryObjectiveTriggerKey = KeyCode.P;

    [Header("XP Testing Settings")]
    [Tooltip("Modifier key required to be held down for adding XP (e.g., Left Alt)")]
    [SerializeField] private KeyCode xpModifierKey = KeyCode.LeftAlt;
    
    [Tooltip("Key to press while holding the modifier to add XP (e.g., X)")]
    [SerializeField] private KeyCode xpTriggerKey = KeyCode.X;
    
    [Tooltip("Amount of XP to add each time the shortcut is used")]
    [SerializeField] private float xpTestAmount = 100f;

    [Header("References")]
    [SerializeField] private PlayerStats playerStats;

    private Inventory playerInventory;
    private bool isCompletingObjectives = false;
    private bool isCompletingSecondaryObjectives = false;

    [Header("Time Acceleration")]
    [Tooltip("Whether completing objectives should also accelerate time")]
    [SerializeField] private bool accelerateTimeWithObjectives = true;
    
    [Tooltip("How much faster time should pass when accelerated")]
    [SerializeField] private float timeAccelerationMultiplier = 10f;

    private DayNightCycle dayNightCycle;
    private bool isTimeAccelerated = false;

    private void Start()
    {
        // Find the FirstPersonController and get its Inventory component
        FirstPersonController fpsController = FindFirstObjectByType<FirstPersonController>();
        if (fpsController != null)
        {
            playerInventory = fpsController.GetInventory();
            if (playerInventory == null)
            {
                Debug.LogError("Inventory component not found on FirstPersonController!");
            }

            // Only try to find PlayerStats if it wasn't assigned in the Inspector
            if (playerStats == null)
            {
                playerStats = fpsController.GetComponent<PlayerStats>();
                if (playerStats == null)
                {
                    Debug.LogError("PlayerStats component not found! Please assign it in the Inspector.");
                }
            }
        }
        else
        {
            Debug.LogError("FirstPersonController not found in scene!");
        }

        // Find DayNightCycle
        dayNightCycle = FindFirstObjectByType<DayNightCycle>();
        if (dayNightCycle == null)
        {
            Debug.LogWarning("DayNightCycle not found in scene! Time acceleration will be disabled.");
        }
    }

    private void Update()
    {
        // Check for items shortcut
        if (Input.GetKey(itemModifierKey) && Input.GetKeyDown(itemTriggerKey))
        {
            AddTestItemsToInventory();
        }

        // Check for primary objectives shortcut
        if (Input.GetKey(objectiveModifierKey) && Input.GetKeyDown(objectiveTriggerKey) && !isCompletingObjectives)
        {
            StartCoroutine(CompleteTestObjectivesSequentially(testObjectiveIds, "primary"));
        }

        // Check for secondary objectives shortcut
        if (Input.GetKey(secondaryObjectiveModifierKey) && Input.GetKeyDown(secondaryObjectiveTriggerKey) && !isCompletingSecondaryObjectives)
        {
            StartCoroutine(CompleteTestObjectivesSequentially(secondaryTestObjectiveIds, "secondary"));
        }

        // Check for XP shortcut
        if (Input.GetKey(xpModifierKey) && Input.GetKeyDown(xpTriggerKey))
        {
            AddTestXP();
        }
    }

    private void AddTestItemsToInventory()
    {
        if (playerInventory == null)
        {
            Debug.LogError("Cannot add items: Player inventory not found!");
            return;
        }

//        Debug.Log("Adding test items to inventory...");
        foreach (var item in testItems)
        {
            if (item != null)
            {
                bool added = playerInventory.AddItem(item);
                if (added)
                {
//                    Debug.Log($"Successfully added {item.GetItemName()} to inventory.");
                }
                else
                {
                    Debug.LogWarning($"Failed to add {item.GetItemName()} to inventory. Inventory might be full.");
                }
            }
            else
            {
                Debug.LogWarning("Null item found in testItems array!");
            }
        }
    }

    private IEnumerator CompleteTestObjectivesSequentially(string[] objectiveIds, string setName)
    {
        if (ObjectiveManager.Instance == null)
        {
            Debug.LogError("Cannot complete objectives: ObjectiveManager not found!");
            yield break;
        }

        if (setName == "primary")
            isCompletingObjectives = true;
        else
            isCompletingSecondaryObjectives = true;

 //       Debug.Log($"Starting sequential objective completion for {setName} set...");

        // Accelerate time if enabled
        if (accelerateTimeWithObjectives && dayNightCycle != null && !isTimeAccelerated)
        {
            dayNightCycle.SetTimeSpeedMultiplier(timeAccelerationMultiplier);
            isTimeAccelerated = true;
 //           Debug.Log($"[TimeAcceleration] Time speed increased to {timeAccelerationMultiplier}x");
        }

        foreach (var objectiveId in objectiveIds)
        {
            if (!string.IsNullOrEmpty(objectiveId))
            {
                try
                {
                    ObjectiveManager.Instance.CompleteObjective(objectiveId);
                    
                    // Remove world space marker for the completed objective
                    if (WorldSpaceObjectiveManager.Instance != null)
                    {
                        WorldSpaceObjectiveManager.Instance.RemoveObjectiveMarker(objectiveId);
 //                       Debug.Log($"Removed world space marker for objective: {objectiveId}");
                    }
                    
             //       Debug.Log($"Successfully completed {setName} objective: {objectiveId}");
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"Failed to complete {setName} objective {objectiveId}: {e.Message}");
                }

                yield return new WaitForSeconds(objectiveCompletionDelay);
            }
            else
            {
                Debug.LogWarning($"Empty objective ID found in {setName} testObjectiveIds array!");
            }
        }

       // Debug.Log($"Finished completing all {setName} test objectives.");
        
        // Reset time acceleration
        if (accelerateTimeWithObjectives && dayNightCycle != null && isTimeAccelerated)
        {
            dayNightCycle.SetTimeSpeedMultiplier(1f);
            isTimeAccelerated = false;
//            Debug.Log("[TimeAcceleration] Time speed reset to normal");
        }

        if (setName == "primary")
            isCompletingObjectives = false;
        else
            isCompletingSecondaryObjectives = false;
    }

    private void AddTestXP()
    {
        if (playerStats == null)
        {
            Debug.LogError("Cannot add XP: PlayerStats not found!");
            return;
        }

        playerStats.AddXP(xpTestAmount);
        Debug.Log($"Added {xpTestAmount} XP to player");
    }

    // Optional: Methods to change shortcuts at runtime
    public void SetItemShortcut(KeyCode newModifier, KeyCode newTrigger)
    {
        itemModifierKey = newModifier;
        itemTriggerKey = newTrigger;
        Debug.Log($"Item testing shortcut changed to: {itemModifierKey} + {itemTriggerKey}");
    }

    public void SetObjectiveShortcut(KeyCode newModifier, KeyCode newTrigger)
    {
        objectiveModifierKey = newModifier;
        objectiveTriggerKey = newTrigger;
        Debug.Log($"Objective testing shortcut changed to: {objectiveModifierKey} + {objectiveTriggerKey}");
    }

    // Optional: Method to change XP shortcut at runtime
    public void SetXPShortcut(KeyCode newModifier, KeyCode newTrigger)
    {
        xpModifierKey = newModifier;
        xpTriggerKey = newTrigger;
        Debug.Log($"XP testing shortcut changed to: {xpModifierKey} + {xpTriggerKey}");
    }

    // Add method to change secondary objective shortcuts at runtime
    public void SetSecondaryObjectiveShortcut(KeyCode newModifier, KeyCode newTrigger)
    {
        secondaryObjectiveModifierKey = newModifier;
        secondaryObjectiveTriggerKey = newTrigger;
        Debug.Log($"Secondary objective testing shortcut changed to: {secondaryObjectiveModifierKey} + {secondaryObjectiveTriggerKey}");
    }

    // Add method to toggle time acceleration
    public void SetTimeAcceleration(bool enabled)
    {
        accelerateTimeWithObjectives = enabled;
        Debug.Log($"Time acceleration with objectives {(enabled ? "enabled" : "disabled")}");
    }

    // Add method to change time acceleration multiplier
    public void SetTimeAccelerationMultiplier(float multiplier)
    {
        timeAccelerationMultiplier = Mathf.Max(1f, multiplier);
        Debug.Log($"Time acceleration multiplier set to {timeAccelerationMultiplier}x");
    }
} 