// DirectionControlUI.cs (Script on Panel Root, Controls Child Handle)
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections;


[RequireComponent(typeof(RectTransform))] // Panel should have one
[RequireComponent(typeof(Canvas))]       // Panel should have the World Space Canvas
[RequireComponent(typeof(GraphicRaycaster))]
public class DirectionControlUI : MonoBehaviour
{
    [Header("Child UI References")]
    [Tooltip("Assign the child 'Retreat' Button GameObject")]
    [SerializeField] private Button retreatButton;


    //Skill button
    [SerializeField] private Button buffskillButton;
    [SerializeField] private Button dashskillButton;
    [SerializeField] private Button arrowskillButton;
    [SerializeField] private Button chargeskillButton;


    [Tooltip("Assign the child 'DirectionHandle' Image GameObject")]
    [SerializeField] private Image directionHandleImage; // The draggable visual handle

    [Header("Drag Settings")]
    [SerializeField] private float dragRadius = 50f;
    [SerializeField] private float deadZoneRadius = 10f;
    [SerializeField] private Vector2 defaultDirection = Vector2.up;
    [SerializeField] private float defaultAngleDegrees = 90f; // Default to Up
    private UnitRange targetUnitRange;


    // --- State & References ---
    private Unit targetUnit;
    private RectTransform panelRectTransform; // This panel's RectTransform (the parent of the handle)
    private RectTransform handleRectTransform; // The handle's RectTransform (the child we move)
    private Camera eventCamera;
    private bool isDraggingHandle = false;
    private Vector2 handleInitialLocalPos; // Initial position of handle within panel (likely 0,0)
    private bool retreatModeOnly = false;


    [Header("Time Slow Settings")]
    [SerializeField] private float dragTimeScale = 0.3f; // How slow time should be
    private float originalTimeScale = 1f;


    //CoolDownHandle slider
    Slider buffCooldownSlider;
    Slider dashCooldownSlider;
    Slider arrowCooldownSlider;
    Slider chargeCooldownSlider;

    // Handle the flip 



    void Awake()
    {
       

        panelRectTransform = GetComponent<RectTransform>(); // Get self

        // Get handle's RectTransform from the assigned Image
        if (directionHandleImage != null)
        {
            handleRectTransform = directionHandleImage.GetComponent<RectTransform>();
            if (handleRectTransform != null)
            {
                handleInitialLocalPos = handleRectTransform.anchoredPosition; // Store handle's start pos (e.g., 0,0)
            }
            else { Debug.LogError("Direction Handle Image needs a RectTransform!", this); }
        }
        else { Debug.LogError("Direction Handle Image not assigned in Inspector!", this); }

        // Setup Retreat Button
        if (retreatButton != null) { retreatButton.onClick.AddListener(OnRetreatClicked); }
        else { Debug.LogError("Retreat Button not assigned!", this); }

       


    }

    public void Initialize(Unit unit)
    {
        targetUnit = unit;
        targetUnitRange = unit.GetComponentInChildren<UnitRange>();
        Canvas localCanvas = GetComponent<Canvas>();
        if (localCanvas != null) { eventCamera = localCanvas.worldCamera; }
        else { Debug.LogError("DirectionControlUI requires a Canvas component on the same GameObject!", this); }

        if (targetUnit == null) { Destroy(gameObject); return; }

        // Reset handle visuals on init
        ResetHandleVisuals();

        if (targetUnitRange != null)
        {
            targetUnitRange.ShowRangePreview(false);
        }

        // Assign slier
        buffCooldownSlider = GameObject.Find("BuffCooldownSlider")?.GetComponent<Slider>();
        buffskillButton.interactable = true;

        dashCooldownSlider = GameObject.Find("DashCoolDownSkill")?.GetComponent<Slider>();
        dashskillButton.interactable = true;

        arrowCooldownSlider = GameObject.Find("ArrowSkillSlider")?.GetComponent<Slider>();
        arrowskillButton.interactable = true;

        chargeCooldownSlider = GameObject.Find("ChargeSkillSlider")?.GetComponent<Slider>();
        chargeskillButton.interactable = true;

        directionCycleButtonObject = GameObject.Find("DirectionButton");

        //buff skill button active
        if (buffskillButton != null)
        {
            buffskillButton.onClick.RemoveAllListeners();
            buffskillButton.onClick.AddListener(() => { 
                
                targetUnit.TryActivateBuffSkill();
                buffskillButton.interactable = false;
                if (buffCooldownSlider != null)
                {
                    SkillCooldownUI cooldownUI = buffCooldownSlider.GetComponent<SkillCooldownUI>();

                    cooldownUI.StartCoolDown(8f);
                    SkillCooldownManager.Instance.StartCooldown(buffskillButton, 8f);
                }


            });

           
            buffskillButton.gameObject.SetActive(targetUnit is BuffGeneralUnit);
        }

        //dash skill button active
        if (dashskillButton != null)
        {
            dashskillButton.onClick.RemoveAllListeners();
            dashskillButton.onClick.AddListener(() => { 
                
                targetUnit.TryActivateDashSkill();
                dashskillButton.interactable = false;
              
            if (dashCooldownSlider != null)
            {
                SkillCooldownUI cooldownUI = dashCooldownSlider.GetComponent<SkillCooldownUI>();
               
                cooldownUI.StartCoolDown(8f);
                 SkillCooldownManager.Instance.StartCooldown(dashskillButton, 8f);
                }

            });
               
           
            dashskillButton.gameObject.SetActive(targetUnit is DashGeneralUnit);
        }


        //arrow burst skill button active
        if (arrowskillButton != null)
        {
            arrowskillButton.onClick.RemoveAllListeners();
            arrowskillButton.onClick.AddListener(() => {

                targetUnit.TryActivateBurstSkill();
                arrowskillButton.interactable = false;

                if (arrowCooldownSlider != null)
                {
                    SkillCooldownUI cooldownUI = arrowCooldownSlider.GetComponent<SkillCooldownUI>();

                    cooldownUI.StartCoolDown(8f);
                    SkillCooldownManager.Instance.StartCooldown(arrowskillButton, 8f);
                }

            });


            arrowskillButton.gameObject.SetActive(targetUnit.GetComponent<UnitRangeGeneral>() != null);
        }

        if (chargeskillButton != null)
        {
            chargeskillButton.onClick.RemoveAllListeners();
            chargeskillButton.onClick.AddListener(() => {

                targetUnit.TryActivateChargeSkill();
                chargeskillButton.interactable = false;

                if (chargeCooldownSlider != null)
                {
                    SkillCooldownUI cooldownUI = chargeCooldownSlider.GetComponent<SkillCooldownUI>();

                    cooldownUI.StartCoolDown(8f);
                    SkillCooldownManager.Instance.StartCooldown(chargeskillButton, 8f);
                }

            });


            chargeskillButton.gameObject.SetActive(targetUnit is ChargeGeneralUnit);
        }

        //SET UP THE DIRECTIONAL BUTTON
        if (directionCycleButtonObject != null)
        {
            directionCycleButton = directionCycleButtonObject.GetComponent<Button>();
            directionCycleButton.onClick.RemoveAllListeners();
            directionCycleButton.onClick.AddListener(CycleDirection);

            directionCycleButtonObject.SetActive(true);
            StartCoroutine(CheckClickOutside());
        }
        else
        {
            Debug.LogError("Direction Cycle Button not assigned in Inspector!", this);
        }

    }

    // Reset handle to center/default rotation
    private void ResetHandleVisuals()
    {
        if (handleRectTransform == null) return;
        handleRectTransform.anchoredPosition = handleInitialLocalPos;
        float initialAngle = Mathf.Atan2(defaultDirection.x, defaultDirection.y) * Mathf.Rad2Deg;
        handleRectTransform.localRotation = Quaternion.Euler(0, 0, initialAngle);
    }

    // --- Methods Called by Event Trigger on DirectionHandle CHILD ---

    //public void HandleBeginDrag(BaseEventData baseData)
    //{
    //    PointerEventData eventData = baseData as PointerEventData;
    //    if (targetUnit == null) return;
    //    if (!RectTransformUtility.RectangleContainsScreenPoint(handleRectTransform, eventData.position, eventData.pressEventCamera ?? eventCamera)) { eventData.pointerDrag = null; return; } // Ensure drag starts on handle
    //    isDraggingHandle = true;
    //    InputManager.SignalUIDragActive();

    //    FindObjectOfType<SimpleCameraZoom>().ZoomTo(targetUnit.transform);
    //    originalTimeScale = Time.timeScale;
    //    Time.timeScale = dragTimeScale;

    //}



    //public void HandleDrag(BaseEventData baseData)
    //{
    //    if (!isDraggingHandle || targetUnit == null || handleRectTransform == null || panelRectTransform == null) return;
    //    PointerEventData eventData = baseData as PointerEventData;

    //    // Calculate pointer position relative to the PARENT PANEL's pivot
    //    Vector2 localPoint;
    //    RectTransformUtility.ScreenPointToLocalPointInRectangle(panelRectTransform, eventData.position, eventData.pressEventCamera ?? eventCamera, out localPoint);

    //    // Snap direction 
    //    Vector2 direction;
    //    if (Mathf.Abs(localPoint.x) > Mathf.Abs(localPoint.y))
    //        direction = (localPoint.x >= 0) ? Vector2.right : Vector2.left;
    //    else
    //        direction = (localPoint.y >= 0) ? Vector2.up : Vector2.down;

    //    // Clamp to drag radius
    //    Vector2 targetHandlePosition = direction * dragRadius;
    //    handleRectTransform.anchoredPosition = targetHandlePosition;

    //    // Update Handle Rotation
    //    float visualAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
    //    handleRectTransform.localRotation = Quaternion.Euler(0, 0, visualAngle - 90f);


    //    // Update unit range direction and visibility
    //    if (targetUnitRange != null)
    //    {
    //        bool outsideDeadZone = localPoint.magnitude >= deadZoneRadius;
    //        if (outsideDeadZone)
    //        {
    //            targetUnitRange.ShowRangePreview(true);
    //            targetUnitRange.transform.rotation = Quaternion.Euler(0, 0, visualAngle);
    //        }
    //    }

    //}



    //public void HandleEndDrag(BaseEventData baseData)
    //{
    //    if (!isDraggingHandle || targetUnit == null || handleRectTransform == null || panelRectTransform == null) return;
    //    isDraggingHandle = false;

    //    Time.timeScale = originalTimeScale;
    //    FindObjectOfType<SimpleCameraZoom>().ResetZoom(); // on drag end

    //    // Use the handle's FINAL Anchored Position relative to the panel center
    //    Vector2 finalHandlePosition = handleRectTransform.anchoredPosition;
    //    float finalDistance = finalHandlePosition.magnitude;

    //    float finalAngleDegrees; // The angle we will apply to the Unit's Z rotation

    //    // Inside HandleEndDrag method in DirectionControlUI.cs
    //    if (finalDistance < deadZoneRadius)
    //    {
    //        finalAngleDegrees = defaultAngleDegrees;
    //        ResetHandleVisuals();
    //    }
    //    else
    //    {
    //        // Existing code for outside deadzone...
    //        Vector2 finalDirection = finalHandlePosition.normalized;
    //        finalAngleDegrees = Mathf.Atan2(finalDirection.y, finalDirection.x) * Mathf.Rad2Deg;

    //        // Flip sprite based on drag direction
    //        targetUnit.SetFacingDirection(finalDirection);
    //    }

    //    // Create the final rotation for the UNIT using the calculated angle
    //    Quaternion finalUnitRotation = Quaternion.Euler(0, 0, finalAngleDegrees);

    //    // Hide the range preview now - before calling ConfirmPlacement
    //    if (targetUnitRange != null)
    //    {
    //        targetUnitRange.ShowRangePreview(false);
    //    }

    //    // Confirm Placement (Unit handles state change & destroying this UI)
    //    targetUnit.ConfirmPlacement(finalUnitRotation);

    //    // Register Deployment
    //    if (targetUnit.SourcePrefab != null) {

    //        DeploymentManager.Instance?.RegisterDeployment(targetUnit.SourcePrefab);

    //        //Call text update herreeeeeeee
    //        UnitIconData iconData = UnitIconData.GetIconDataByPrefab(targetUnit.SourcePrefab);
    //        if (iconData != null)
    //        {
    //            DeploymentAmountText deploymentText = iconData.GetComponentInChildren<DeploymentAmountText>();
    //            if (deploymentText != null)
    //            {
    //                deploymentText.UpdateDeployCountText();
    //            }
    //        }

    //    }
    //    else { /* Log Error */ }

    //    // Signal Drag End
    //    InputManager.SignalUIDragInactive();
    //    // This GameObject is destroyed by targetUnit.ConfirmPlacement()


    //    // Hande the flip of Unit according to direction 




    //}



    //HANDLE THE DIRECTION CHOOSING LOGIC 
    [SerializeField] private GameObject directionCycleButtonObject;
    private Button directionCycleButton;
    private int directionIndex = 0;
    private Vector2[] directions = new Vector2[] { Vector2.up, Vector2.right, Vector2.down, Vector2.left };
    private Quaternion[] directionRotations = new Quaternion[]
    {
    Quaternion.Euler(0, 0, 90),   // Up
    Quaternion.Euler(0, 0, 0),    // Right
    Quaternion.Euler(0, 0, -90),  // Down
    Quaternion.Euler(0, 0, 180),  // Left
    };

    public void CycleDirection()
    {
        directionIndex = (directionIndex + 1) % directions.Length;

        Vector2 newDirection = directions[directionIndex];
        float finalAngleDegrees = Mathf.Atan2(newDirection.y, newDirection.x) * Mathf.Rad2Deg;

        // Rotate range preview properly
        if (targetUnitRange != null)
        {
            targetUnitRange.ShowRangePreview(true);
            targetUnitRange.transform.rotation = Quaternion.Euler(0, 0, finalAngleDegrees);
        }

        // Flip sprite and set direction
        targetUnit.SetFacingDirection(newDirection);
    }

    private IEnumerator CheckClickOutside()
    {
        yield return null;

        while (true)
        {
            if (Input.GetMouseButtonDown(0))
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(
                    directionCycleButton.GetComponent<RectTransform>(),
                    Input.mousePosition,
                    eventCamera))
                {
                    ConfirmDirectionSelection();
                    break;
                }
            }
            yield return null;
        }
    }

    private void ConfirmDirectionSelection()
    {
        if (targetUnit == null) return;

        Vector2 finalDirection = directions[directionIndex];
        float finalAngleDegrees = Mathf.Atan2(finalDirection.y, finalDirection.x) * Mathf.Rad2Deg;
        Quaternion finalUnitRotation = Quaternion.Euler(0, 0, finalAngleDegrees);

        // Hide the range preview now
        if (targetUnitRange != null)
            targetUnitRange.ShowRangePreview(false);

        // Apply final placement
        targetUnit.ConfirmPlacement(finalUnitRotation);

        // Register deployment
        if (targetUnit.SourcePrefab != null)
        {
            DeploymentManager.Instance?.RegisterDeployment(targetUnit.SourcePrefab);

            UnitIconData iconData = UnitIconData.GetIconDataByPrefab(targetUnit.SourcePrefab);
            if (iconData != null)
            {
                DeploymentAmountText deploymentText = iconData.GetComponentInChildren<DeploymentAmountText>();
                if (deploymentText != null)
                    deploymentText.UpdateDeployCountText();
            }
        }

        directionCycleButtonObject.SetActive(false);
        InputManager.SignalUIDragInactive();
    }






    // Keep OnRetreatClicked, Initialize, Awake, OnDestroy...

    public void SetRetreatModeOnly(bool retreatOnly)
    {
        retreatModeOnly = retreatOnly;

        if (retreatOnly)
        {
            // Hide direction handle if present
            if (directionHandleImage != null)
                directionHandleImage.gameObject.SetActive(false);

            // Show only retreat button in a centered position
            if (retreatButton != null)
            {
                retreatButton.gameObject.SetActive(true);
            }
            
            
        }
    }


    

    public void HideAllControls()
    {
        if (directionHandleImage != null)
            directionHandleImage.gameObject.SetActive(false);
        if (retreatButton != null)
            retreatButton.gameObject.SetActive(false);
        if(buffskillButton != null)
            buffskillButton.gameObject.SetActive(false);
        if (dashskillButton != null)
            dashskillButton.gameObject.SetActive(false);
        if (arrowskillButton != null)
            arrowskillButton.gameObject.SetActive(false);
        if (chargeskillButton != null)
            chargeskillButton.gameObject.SetActive(false);

    }

    // Add this method to show skill buttons when a unit is selected
    public void ShowSkillButtons()
    {
        if (buffskillButton != null)
            buffskillButton.gameObject.SetActive(targetUnit is BuffGeneralUnit);
        if (dashskillButton != null)
            dashskillButton.gameObject.SetActive(targetUnit is DashGeneralUnit);
        if (arrowskillButton != null)
            arrowskillButton.gameObject.SetActive(targetUnit.GetComponent<UnitRangeGeneral>() != null);
        if (chargeskillButton != null)
            chargeskillButton.gameObject.SetActive(targetUnit is ChargeGeneralUnit);
    }

    private void OnRetreatClicked() 
    { /* ... Same ... */ 
        if (targetUnit != null) targetUnit.InitiateRetreat(); 
        else Destroy(gameObject); }
    void OnDestroy() 
    { /* ... Same ... */ 
        if (retreatButton != null) retreatButton.onClick.RemoveListener(OnRetreatClicked); 
        PlacementUIManager.Instance?.NotifyDirectionUIHidden(); InputManager.SignalUIDragInactive(); 
    }
}