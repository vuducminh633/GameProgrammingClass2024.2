using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DirectionSelectionUI : MonoBehaviour
{
    [Tooltip("Assign the retreat button from the UI prefab")]
    [SerializeField] private Button retreatButton;
    [SerializeField] private DirectionControlUI directionControl; // Assign DirectionHandle GO in Inspector


    private Unit targetUnit;

    private void Awake()
    {
        if (retreatButton == null)
        {
            Debug.LogError("Failed to reference retreatButton");
        }
        else
        {
            retreatButton.onClick.AddListener(OnRetreatClicked);
        }
        if (directionControl == null) directionControl = GetComponentInChildren<DirectionControlUI>();
        if (directionControl == null) Debug.LogError("DirectionDragger component not found in children!", this);
    }

    public void Initialize(Unit unit) 
    {
        targetUnit = unit; 
        if (targetUnit == null) PlacementUIManager.Instance?.NotifyDirectionUIHidden();
        /* Maybe Destroy(gameObject) here too? */
        if (directionControl != null)
        {
            directionControl.Initialize(targetUnit);
            Debug.Log("DirectionSelectionUI initialized with unit: " + unit?.name);
        }
    }

    private void OnRetreatClicked()
    {
        if (targetUnit != null)
        {
            // Tell the unit to handle its own retreat process.
            // It will destroy this UI instance (as a child), notify managers, etc.
            targetUnit.InitiateRetreat();
        }
        else
        {
            Debug.LogWarning("Retreat clicked, but targetUnit is already null.");
            Destroy(gameObject); // Destroy self if target lost
        }
    }

    // --- Phase 3 Placeholder ---
    // Add methods here later for handling the "second drag" (direction setting)
    // These methods will calculate rotation and call targetUnit.ConfirmPlacement(rotation)
    // and PlacementUIManager.Instance.HideDirectionUI()

    private void OnDestroy()
    {
        if(retreatButton != null)
        {
            retreatButton.onClick.RemoveListener(OnRetreatClicked);
        }
    }
}
