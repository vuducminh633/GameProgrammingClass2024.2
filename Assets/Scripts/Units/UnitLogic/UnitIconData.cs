// UnitIconData.cs (Updated)
using UnityEngine;

public class UnitIconData : MonoBehaviour
{
    [Tooltip("The Unit Prefab this UI icon represents.")]
    public GameObject unitPrefab;

    public Sprite unitIcon;

    public Transform originalParent { get; private set; }
    public int originalSiblingIndex { get; private set; }
    public Vector2 originalAnchoredPosition { get; private set; }

    private bool initialStateStored = false;
    private RectTransform rectTransform; // Cache RectTransform

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        StoreInitialState(); // Store state when the icon first wakes up
        unitIcon = unitPrefab.GetComponent<SpriteRenderer>().sprite;
      
    }

    public void StoreInitialState()
    {
        // Store only once, or if parent seems invalid (e.g., after being reparented during a failed drag reset)
        if (!initialStateStored || transform.parent == null || transform.parent == DragToScreenManager.Instance?.dragGhostIconParent) // Heuristic check
        {
            if (rectTransform != null && transform.parent != null) // Need parent to store state
            {
                originalParent = transform.parent;
                originalSiblingIndex = transform.GetSiblingIndex();
                originalAnchoredPosition = rectTransform.anchoredPosition;
                initialStateStored = true;

            }
        }
    }

    public void EnsureInitialStateStored()
    {
        StoreInitialState();
    }

    public static UnitIconData GetIconDataByPrefab(GameObject prefab)
    {
        foreach (var icon in FindObjectsOfType<UnitIconData>())
        {
            if (icon.unitPrefab == prefab)
                return icon;
        }
        return null;
    }
}