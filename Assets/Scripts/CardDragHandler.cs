using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class CardDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Dependencies")]
    [Tooltip("Canvas used to position the card while it is being dragged. Defaults to the first parent canvas if not assigned.")]
    public Canvas dragCanvas;

    [Header("Behaviour")]
    [Tooltip("If enabled the card returns to its previous slot when it is not dropped on a valid slot.")]
    public bool returnToOriginalSlotIfRejected = true;

    private RectTransform _rectTransform;
    private CanvasGroup _canvasGroup;
    private Vector3 _dragOffset;

    private Transform _originalParent;
    private int _originalSiblingIndex;
    private Vector2 _originalAnchoredPosition;
    private CardSlot _originalSlot;
    private CardSlot _currentSlot;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
        {
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        if (dragCanvas == null)
        {
            dragCanvas = GetComponentInParent<Canvas>();
        }

        _currentSlot = GetComponentInParent<CardSlot>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        _originalParent = _rectTransform.parent;
        _originalSiblingIndex = _rectTransform.GetSiblingIndex();
        _originalAnchoredPosition = _rectTransform.anchoredPosition;
        _originalSlot = _currentSlot;

        Vector3 pointerPosition = GetPointerWorldPosition(eventData);
        _dragOffset = _rectTransform.position - pointerPosition;

        _canvasGroup.blocksRaycasts = false;

        if (dragCanvas != null)
        {
            _rectTransform.SetParent(dragCanvas.transform, true);
            _rectTransform.SetAsLastSibling();
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        Vector3 pointerPosition = GetPointerWorldPosition(eventData);
        _rectTransform.position = pointerPosition + _dragOffset;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        _canvasGroup.blocksRaycasts = true;

        CardSlot targetSlot = null;
        if (eventData.pointerEnter != null)
        {
            targetSlot = eventData.pointerEnter.GetComponentInParent<CardSlot>();
        }

        if (targetSlot != null && targetSlot.TryAccept(this))
        {
            return;
        }

        if (returnToOriginalSlotIfRejected)
        {
            RestoreOriginalPlacement();
        }
    }

    public void RestoreOriginalPlacement()
    {
        _currentSlot = _originalSlot;

        if (_originalParent != null)
        {
            _rectTransform.SetParent(_originalParent, false);
            _rectTransform.SetSiblingIndex(_originalSiblingIndex);
            _rectTransform.anchoredPosition = _originalAnchoredPosition;
        }
    }

    public void CompleteSlotDrop(CardSlot slot, Transform parent, Vector2 anchoredPosition)
    {
        _currentSlot = slot;

        if (parent != null)
        {
            _rectTransform.SetParent(parent, false);
        }

        _rectTransform.SetAsLastSibling();
        _rectTransform.anchoredPosition = anchoredPosition;
    }

    public void CompleteNonSlotDrop(Transform parent, Vector2 anchoredPosition, int siblingIndex)
    {
        _currentSlot = null;

        if (parent == null)
        {
            return;
        }

        _rectTransform.SetParent(parent, false);

        if (siblingIndex >= 0 && siblingIndex < parent.childCount)
        {
            _rectTransform.SetSiblingIndex(siblingIndex);
        }
        else
        {
            _rectTransform.SetAsLastSibling();
        }

        _rectTransform.anchoredPosition = anchoredPosition;
    }

    public RectTransform RectTransform => _rectTransform;
    public CardSlot CurrentSlot => _currentSlot;
    public CardSlot OriginalSlot => _originalSlot;
    public Transform OriginalParent => _originalParent;
    public int OriginalSiblingIndex => _originalSiblingIndex;
    public Vector2 OriginalAnchoredPosition => _originalAnchoredPosition;

    private Vector3 GetPointerWorldPosition(PointerEventData eventData)
    {
        Vector3 worldPoint = _rectTransform.position;
        Camera eventCamera = eventData.pressEventCamera ?? dragCanvas?.worldCamera;
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(_rectTransform, eventData.position, eventCamera, out var result))
        {
            worldPoint = result;
        }

        return worldPoint;
    }
}
