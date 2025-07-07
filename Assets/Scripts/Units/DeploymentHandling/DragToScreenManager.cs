// DragToScreenManager.cs (Phase 1 Logic included, LayoutElement fix REMOVED)

using System.Collections.Generic;
using TowerDefense.Utils;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DragToScreenManager : MonoBehaviour
{
    // --- Singleton ---
    public static DragToScreenManager Instance { get; private set; }

    // --- Inspector References ---
    [Header("Required References")]
    [Tooltip("Assign the GameObject that DIRECTLY holds all the UnitIconData icons as children")]
    [SerializeField] private Transform unitIconContainer;
    [SerializeField] private Canvas parentCanvas;
    public RectTransform dragGhostIconParent;
    [SerializeField] private GameObject dragGhostIconPrefab;
    // Keep PlacementUIManager reference for the input blocking check
    [SerializeField] private PlacementUIManager placementUIManager;


    // --- State ---
    private List<UnitIconData> unitIcons = new List<UnitIconData>();
    private LayoutElement originalIconLayoutElement = null;
    private GameObject currentDraggedPrefab = null;
    private Unit currentUnitDataOnPrefab = null;
    private UnitIconData currentlyDraggedIconData = null;
    private bool isDragging = false;
    private bool isDraggingLastOne = false;
    private bool canPlace;
    private Vector3Int closestTile;

    // Drag Target State
    private GameObject ghostIconInstance = null;
    private RectTransform ghostIconRectTransform = null;
    private RectTransform draggedOriginalIconRect = null;
    private Transform originalParent = null;
    private int originalSiblingIndex;
    private Vector2 originalAnchoredPosition;
    private Vector2 dragOffset;
    // REMOVED: private LayoutElement draggedOriginalLayoutElement;

    // --- Dependencies ---
    private DeploymentManager deploymentManager;
    private TileManager tileManager;

    private void Awake()
    {
        // Singleton Setup
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Get Dependencies
        deploymentManager = DeploymentManager.Instance;
        tileManager = TileManager.Instance;
        if (placementUIManager == null) placementUIManager = PlacementUIManager.Instance; // Get if not assigned

        // Validate References
        bool referencesValid = true;
        if (unitIconContainer == null) { /* LogError */ referencesValid = false; }
        if (tileManager == null) { /* LogError */ referencesValid = false; }
        if (parentCanvas == null) parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null) { /* LogError */ referencesValid = false; }
        if (deploymentManager == null) { /* LogError */ referencesValid = false; }
        if (dragGhostIconPrefab == null) { /* LogError */ referencesValid = false; }
        if (dragGhostIconParent == null) dragGhostIconParent = parentCanvas.transform as RectTransform;
        if (placementUIManager == null) { /* LogError */ referencesValid = false; } // Still need this check
        if (!referencesValid) { this.enabled = false; /* LogError */ return; }

        // Populate Icon List
        PopulateUnitIconList();
    }

    private void PopulateUnitIconList()
    {
        unitIcons.Clear();
        if (unitIconContainer != null)
        {
            unitIconContainer.GetComponentsInChildren<UnitIconData>(true, unitIcons);
        }
        else { Debug.LogError("DragToScreenManager: Unit Icon Container not assigned!"); }
    }

    // --- Event Handlers ---

    public void HandleBeginDrag(BaseEventData baseData)
    {
        // Block if direction setting UI is active
        if (placementUIManager != null && placementUIManager.IsDirectionUIShown) { PointerEventData data = baseData as PointerEventData; if (data != null) data.pointerDrag = null; return; }
        if (!this.enabled) return;
        PointerEventData eventData = baseData as PointerEventData;
        if (eventData == null || eventData.pointerDrag == null) return;

        currentlyDraggedIconData = eventData.pointerDrag.GetComponent<UnitIconData>();
        if (currentlyDraggedIconData == null || currentlyDraggedIconData.unitPrefab == null) { CancelDrag(); return; }

        Unit unitComp = currentlyDraggedIconData.unitPrefab.GetComponent<Unit>();
        if (unitComp == null || unitComp.unitDataSO == null) { CancelDrag(); return; }

        // Check Deployment Limit
        int maxCount = unitComp.unitDataSO.maxNumberOfDeployments;
        if (!deploymentManager.CanDeploy(currentlyDraggedIconData.unitPrefab, maxCount)) { CancelDrag(); return; }

        // Decide Drag Type
        int currentCount = deploymentManager.GetCurrentDeploymentCount(currentlyDraggedIconData.unitPrefab);
        isDraggingLastOne = (maxCount - currentCount <= 1);

        // Set common state
        isDragging = true;
        currentDraggedPrefab = currentlyDraggedIconData.unitPrefab;
        currentUnitDataOnPrefab = unitComp;
        InputManager.SignalUIDragActive();

        if (isDraggingLastOne)
        {
            // --- Prepare Original Icon ---
            ghostIconInstance = null; ghostIconRectTransform = null;
            draggedOriginalIconRect = currentlyDraggedIconData.GetComponent<RectTransform>();
            if (draggedOriginalIconRect == null) { CancelDrag(); return; }

            dragOffset = (Vector2)draggedOriginalIconRect.position - ((PointerEventData)baseData).position;

            originalParent = draggedOriginalIconRect.parent;
            originalSiblingIndex = draggedOriginalIconRect.GetSiblingIndex();
            originalAnchoredPosition = draggedOriginalIconRect.anchoredPosition;

            draggedOriginalIconRect.SetParent(dragGhostIconParent, true); // Reparent
            draggedOriginalIconRect.SetAsLastSibling();
            ToggleRaycasts(draggedOriginalIconRect, false); // Disable raycasts

            UpdateOriginalIconPosition((PointerEventData)baseData);
        }
        else
        {
            // --- Prepare Ghost Icon ---
            draggedOriginalIconRect = null; originalParent = null;
            CreateGhostIcon(currentlyDraggedIconData, eventData);

            originalIconLayoutElement = currentlyDraggedIconData.GetComponent<LayoutElement>();
            if (originalIconLayoutElement != null)
            {
                originalIconLayoutElement.ignoreLayout = true;
            }
        }

        // Highlight tiles
        if (tileManager != null) tileManager.HighlightPlaceableTiles(currentDraggedPrefab);
        canPlace = false;
    }


    public void HandleDrag(BaseEventData baseData)
    {
        if (!isDragging) return;
        PointerEventData eventData = baseData as PointerEventData;
        if (eventData == null || tileManager == null || currentDraggedPrefab == null) return;

        Vector3 worldPos = InputManager.MouseWorldPosition;
        closestTile = tileManager.GetClosestPlaceableTile(worldPos, currentDraggedPrefab, out canPlace);

        // Update position conditionally (ghost or original)
        if (ghostIconInstance != null)
        {
            UpdateGhostIconPosition(eventData);
            if (canPlace) SnapGhostIconToTile(closestTile);
        }
        else if (draggedOriginalIconRect != null)
        {
            UpdateOriginalIconPosition(eventData);
            if (canPlace) SnapOriginalIconToTile(closestTile);
        }
    }


    public void HandleEndDrag(BaseEventData baseData)
    {
        if (!isDragging) return;

        InputManager.SignalUIDragInactive();

        if (tileManager?.tileHighlighter != null)
            tileManager.tileHighlighter.ClearHighlights();

        // --- PHASE 1 PLACEMENT LOGIC ---
        if (canPlace)
        {
            // only place when enough hdp 
            int unitCost = currentUnitDataOnPrefab.unitDataSO.DP;
            if (DPManager.Instance.CanSpendDP(unitCost))
            {

                Vector3 snapPosition = tileManager.tilemap.GetCellCenterWorld(closestTile);
                Unit awaitDeploymentUnit = null;

                // Call provisional placement (no DP check/spend here)
                bool placedSuccessfully = tileManager.TryPlaceCharacterProvisionally(snapPosition, currentDraggedPrefab, out awaitDeploymentUnit);

                if (placedSuccessfully && awaitDeploymentUnit != null)
                {
                    if (isDraggingLastOne && currentlyDraggedIconData != null)
                    {
                        currentlyDraggedIconData.gameObject.SetActive(false);
                    }
                }
                else
                {
                    Debug.LogWarning($"Phase 1: Provisional placement failed for {currentDraggedPrefab?.name ?? "Unknown"} at {closestTile}.");
                }
            }

        }
   


        // --- Cleanup ---
        if (ghostIconInstance != null)
        {
            DestroyGhostIcon();
            // Optional: Re-enable original icon
        }
        else if (draggedOriginalIconRect != null)
        {
            // Reset original icon *only if it wasn't just disabled*
            if (currentlyDraggedIconData != null && currentlyDraggedIconData.gameObject.activeSelf)
            {
                ResetOriginalIcon(); // Resets parent, index, position, raycasts (NO layout element)
            }
            else
            {
                // Icon was disabled, just clear refs
                draggedOriginalIconRect = null;
                originalParent = null;
            }
        }
            
        ResetDragState(); // Reset manager's internal state
    }


    // --- Helper Methods ---

    // ResetOriginalIcon (NO LayoutElement)
    private void ResetOriginalIcon()
    {
        if (draggedOriginalIconRect == null) return;

        if (originalParent != null)
        {
            draggedOriginalIconRect.SetParent(originalParent, true);
            try { draggedOriginalIconRect.SetSiblingIndex(originalSiblingIndex); } catch { }
        }
        draggedOriginalIconRect.anchoredPosition = originalAnchoredPosition;
        ToggleRaycasts(draggedOriginalIconRect, true);

        // References cleared in ResetDragState
    }

    // ResetDragState (NO LayoutElement)
    private void ResetDragState()
    {
        if (originalIconLayoutElement != null)
        {
            originalIconLayoutElement.ignoreLayout = false;
            originalIconLayoutElement = null; // Clear the reference
        }
        isDragging = false;
        isDraggingLastOne = false;
        dragOffset = Vector2.zero;
        currentDraggedPrefab = null;
        currentUnitDataOnPrefab = null;
        canPlace = false;
        ghostIconInstance = null;
        ghostIconRectTransform = null;
        draggedOriginalIconRect = null;
        originalParent = null;
        currentlyDraggedIconData = null;
        // NO draggedOriginalLayoutElement reset
    }

    // HandleUnitRetreat (Relies on Layout Group)
    public void HandleUnitRetreat(GameObject unitPrefab)
    {
        if (unitPrefab == null) { /* Log warning */ return; }

        // If the prefab is on cooldown, subscribe to the event and return
        if (RedeploymentManager.Instance != null && RedeploymentManager.Instance.IsOnCooldown(unitPrefab))
        {
            RedeploymentManager.Instance.OnRedeployReady -= OnRedeployReadyHandler; // Prevent double subscription
            RedeploymentManager.Instance.OnRedeployReady += OnRedeployReadyHandler;
            Debug.Log("THIS IS CALLED");
            return;
        }

        // If not on cooldown, immediately re-enable the prefab/icon
        ReactivatePrefab(unitPrefab);
    }

    // Handler for cooldown completion
    private void OnRedeployReadyHandler(GameObject prefab)
    {
        // Unsubscribe to avoid memory leaks
        if (RedeploymentManager.Instance != null)
        {
            RedeploymentManager.Instance.OnRedeployReady -= OnRedeployReadyHandler;
        }

        ReactivatePrefab(prefab);
    }

    // Actual logic to re-enable the prefab/icon in UI
    private void ReactivatePrefab(GameObject prefab)
    {
 
        foreach (UnitIconData icon in unitIcons)
        {
            icon.gameObject.SetActive(true);
          
            if (icon != null && icon.unitPrefab == prefab)
            {
                // Found the matching icon!
                RectTransform iconRect = icon.GetComponent<RectTransform>();
                if (iconRect == null) continue; // Need RectTransform

                if (icon.originalParent != null)
                {
                    // Reparent first
                    iconRect.SetParent(icon.originalParent, false);
                    try { iconRect.SetSiblingIndex(icon.originalSiblingIndex); } catch { } // Try/Catch just in case
                }
                else { Debug.LogWarning($"Original parent for {icon.name} not stored, cannot reparent accurately."); }

                // Restore position AFTER potential reparenting
                iconRect.anchoredPosition = icon.originalAnchoredPosition;
                // Re-enable raycasting if it was disabled (ToggleRaycasts handles null checks)
                ToggleRaycasts(iconRect, true);

                // Re-activate the GameObject
                //if (!icon.gameObject.activeSelf)
                //{
                //    icon.gameObject.SetActive(true);
                //    Debug.Log("THIS IS CALLED RETREAT ACTIVE");
                //}

                // Optional: Force layout group update on the original parent
                if (icon.originalParent != null)
                    LayoutRebuilder.MarkLayoutForRebuild(icon.originalParent as RectTransform); // Requires 'using UnityEngine.UI;'

                return; // Exit after handling
            }
        }
        // ... (Log warning if not found) ...
        Debug.LogWarning($"Could not find matching UnitIconData in list to re-enable for prefab {prefab.name}");
    }

    public void RefreshIconList()
    {
        PopulateUnitIconList();
    }
    // Keep other helpers: CreateGhostIcon, DestroyGhostIcon, Update/Snap methods, CancelDrag, ToggleRaycasts...
    // ... (Include the rest of the helper methods from the previous full script) ...
    private void CreateGhostIcon(UnitIconData iconData, PointerEventData eventData) { if (dragGhostIconPrefab == null) return; ghostIconInstance = Instantiate(dragGhostIconPrefab, dragGhostIconParent); ghostIconInstance.name = $"GhostIcon_{iconData.unitPrefab.name}"; Image ghostImage = ghostIconInstance.GetComponent<Image>(); Image originalImage = iconData.GetComponent<Image>(); if (ghostImage != null && originalImage != null && originalImage.sprite != null) { ghostImage.sprite = originalImage.sprite; ghostImage.color = originalImage.color; ghostImage.raycastTarget = false; } else if (ghostImage != null) ghostImage.raycastTarget = false; ghostIconRectTransform = ghostIconInstance.GetComponent<RectTransform>(); if (ghostIconRectTransform != null) UpdateGhostIconPosition(eventData); else Debug.LogError("Ghost Icon Prefab missing RectTransform!", ghostIconInstance); ghostIconInstance.SetActive(true); }
    private void DestroyGhostIcon() { if (ghostIconInstance != null) { Destroy(ghostIconInstance); ghostIconInstance = null; ghostIconRectTransform = null; } }
    private void UpdateGhostIconPosition(PointerEventData eventData) { if (ghostIconRectTransform == null || parentCanvas == null) return; Vector2 localPoint; RectTransformUtility.ScreenPointToLocalPointInRectangle(parentCanvas.transform as RectTransform, eventData.position, parentCanvas.worldCamera, out localPoint); ghostIconRectTransform.anchoredPosition = localPoint; }
    private void UpdateOriginalIconPosition(PointerEventData eventData) { if (draggedOriginalIconRect == null) return; draggedOriginalIconRect.position = eventData.position + dragOffset; }
    private void SnapGhostIconToTile(Vector3Int tilePosition) { if (tileManager == null || parentCanvas == null || ghostIconRectTransform == null || Camera.main == null) return; Vector3 worldPos = tileManager.tilemap.GetCellCenterWorld(tilePosition); Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos); Vector2 anchoredPos; if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentCanvas.transform as RectTransform, screenPos, parentCanvas.worldCamera, out anchoredPos)) { ghostIconRectTransform.anchoredPosition = anchoredPos; } }
    private void SnapOriginalIconToTile(Vector3Int tilePosition) { if (tileManager == null || draggedOriginalIconRect == null || Camera.main == null) return; Vector3 worldPos = tileManager.tilemap.GetCellCenterWorld(tilePosition); Vector2 screenPos = Camera.main.WorldToScreenPoint(worldPos); draggedOriginalIconRect.position = screenPos + dragOffset; }
    private void CancelDrag() {DestroyGhostIcon(); if (draggedOriginalIconRect != null) ResetOriginalIcon(); ResetDragState(); }
    private void ToggleRaycasts(Transform target, bool enable) { Graphic g = target.GetComponent<Graphic>(); if (g != null) g.raycastTarget = enable; foreach (Transform child in target) { ToggleRaycasts(child, enable); } }

} // End of Class