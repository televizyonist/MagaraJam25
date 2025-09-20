using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class HandAreaHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    [Header("Layout")]
    public float defaultSpacing = 10f;
    public float expandedSpacing = 40f;
    public float hoverYOffset = 40f;

    [Header("Animation")]
    public float positionSmoothTime = 0.1f;
    public float hoverCheckInterval = 0.03f;
    public Camera uiCamera;

    private readonly List<RectTransform> _cardRects = new List<RectTransform>();
    private readonly List<Vector2> _velocity = new List<Vector2>();

    private RectTransform _rectTransform;
    private bool _isHovered;
    private int _hoveredIndex = -1;
    private int _activeCardIndex = -1;
    private float _nextHoverCheck;
    private Camera _runtimeCamera;

    public void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        CacheCards();
    }

    public void OnEnable()
    {
        CacheCards();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isHovered = true;
        _nextHoverCheck = 0f;
        _runtimeCamera = ResolveCamera(eventData);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovered = false;
        _hoveredIndex = -1;
        _activeCardIndex = -1;
        _runtimeCamera = null;
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        _runtimeCamera = ResolveCamera(eventData);
        UpdateHoveredCard(eventData.position);
    }

    private void Update()
    {
        if (_isHovered)
        {
            if (_nextHoverCheck <= 0f)
            {
                UpdateHoveredCard(Input.mousePosition);
                _nextHoverCheck = hoverCheckInterval;
            }
            else
            {
                _nextHoverCheck -= Time.unscaledDeltaTime;
            }
        }

        UpdateCardPositions();
    }

    private void CacheCards()
    {
        _cardRects.Clear();
        _velocity.Clear();

        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i) as RectTransform;
            if (child == null || !child.gameObject.activeInHierarchy)
            {
                continue;
            }

            _cardRects.Add(child);
            _velocity.Add(Vector2.zero);
        }

        if (_cardRects.Count == 0)
        {
            _hoveredIndex = -1;
            _activeCardIndex = -1;
        }
        else
        {
            _hoveredIndex = Mathf.Clamp(_hoveredIndex, -1, _cardRects.Count - 1);
            _activeCardIndex = Mathf.Clamp(_activeCardIndex, -1, _cardRects.Count - 1);
        }
    }

    private void UpdateHoveredCard(Vector2 pointerPosition)
    {
        if (_cardRects.Count == 0)
        {
            _hoveredIndex = -1;
            _activeCardIndex = -1;
            return;
        }

        int detectedIndex = DetermineHoveredCard(pointerPosition);

        if (detectedIndex != -1)
        {
            _hoveredIndex = detectedIndex;
            _activeCardIndex = detectedIndex;
        }
        else
        {
            _hoveredIndex = -1;
        }
    }

    private void UpdateCardPositions()
    {
        if (_cardRects.Count == 0)
        {
            return;
        }

        float spacing = _isHovered ? expandedSpacing : defaultSpacing;
        float totalWidth = spacing * (_cardRects.Count - 1);
        float startX = -totalWidth * 0.5f;

        for (int i = 0; i < _cardRects.Count; i++)
        {
            float targetX = startX + spacing * i;
            float targetY = 0f;

            int raisedIndex = _isHovered ? _activeCardIndex : -1;
            if (raisedIndex == i)
            {
                targetY += hoverYOffset;
            }

            RectTransform card = _cardRects[i];
            Vector2 target = new Vector2(targetX, targetY);
            Vector2 current = card.anchoredPosition;
            Vector2 velocity = _velocity[i];

            Vector2 newPosition = Vector2.SmoothDamp(current, target, ref velocity, positionSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
            card.anchoredPosition = newPosition;
            _velocity[i] = velocity;
        }
    }

    private int DetermineHoveredCard(Vector2 pointerPosition)
    {
        if (_rectTransform == null)
        {
            return -1;
        }

        Camera camera = uiCamera != null ? uiCamera : _runtimeCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rectTransform, pointerPosition, camera, out Vector2 localPoint))
        {
            return -1;
        }

        int closestIndex = -1;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < _cardRects.Count; i++)
        {
            RectTransform card = _cardRects[i];
            Vector2 cardPos = card.anchoredPosition;
            Vector2 halfSize = card.rect.size * 0.5f;

            float horizontalDelta = Mathf.Abs(localPoint.x - cardPos.x);
            float verticalDelta = Mathf.Abs(localPoint.y - cardPos.y);

            float verticalLimit = halfSize.y + hoverYOffset;

            if (horizontalDelta <= halfSize.x && verticalDelta <= verticalLimit)
            {
                return i;
            }

            if (horizontalDelta < closestDistance)
            {
                closestDistance = horizontalDelta;
                closestIndex = i;
            }
        }

        return closestIndex;
    }

    private Camera ResolveCamera(PointerEventData eventData)
    {
        if (uiCamera != null)
        {
            return uiCamera;
        }

        if (eventData != null)
        {
            if (eventData.enterEventCamera != null)
            {
                return eventData.enterEventCamera;
            }

            if (eventData.pressEventCamera != null)
            {
                return eventData.pressEventCamera;
            }
        }

        return null;
    }
}
