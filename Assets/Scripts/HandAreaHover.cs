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

    private bool _isHovered;
    private int _hoveredIndex = -1;
    private float _nextHoverCheck;

    public void Awake()
    {
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
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovered = false;
        _hoveredIndex = -1;
    }

    public void OnPointerMove(PointerEventData eventData)
    {
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
    }

    private void UpdateHoveredCard(Vector2 pointerPosition)
    {
        _hoveredIndex = -1;

        for (int i = 0; i < _cardRects.Count; i++)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(_cardRects[i], pointerPosition, uiCamera))
            {
                _hoveredIndex = i;
                break;
            }
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

            if (_isHovered && i == _hoveredIndex)
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
}
