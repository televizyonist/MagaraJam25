using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class HandAreaHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    [Header("Card Generation")]
    [Tooltip("Resource path used to load the card prefab.")]
    public string cardPrefabResourcePath = "Prefabs/Card";

    [Tooltip("Resource path used to load the relationship data json.")]
    public string relationshipsResourcePath = "Data/relationships";

    [Tooltip("Amount of cards that will be dealt to the player's hand when the game starts.")]
    public int startingHandSize = 10;

    [Tooltip("Optional container used to keep cards that are not currently in the player's hand.")]
    public RectTransform deckContainer;

    [Header("Layout")]
    public float defaultSpacing = 10f;
    public float expandedSpacing = 40f;
    public float hoverYOffset = 40f;

    [Header("Animation")]
    public float positionSmoothTime = 0.1f;
    public float hoverCheckInterval = 0.03f;
    public Camera uiCamera;

    [Header("Preview")]
    [Tooltip("Optional container used to display the hovered card preview.")]
    public RectTransform previewContainer;

    [Tooltip("Offset applied to the preview relative to its parent container.")]
    public Vector2 previewOffset = new Vector2(0f, 260f);

    [Tooltip("Scale applied to the preview card instance.")]
    public float previewScale = 1.5f;

    private readonly List<RectTransform> _cardRects = new List<RectTransform>();
    private readonly List<Vector2> _velocity = new List<Vector2>();

    private readonly List<CardView> _drawPile = new List<CardView>();
    private readonly List<CardView> _handCards = new List<CardView>();
    private readonly List<CharacterCardDefinition> _cardDefinitions = new List<CharacterCardDefinition>();

    private GameObject _cardPrefab;
    private System.Random _random;
    private bool _cardsInitialized;
    private bool _initializationInProgress;

    private RectTransform _rectTransform;
    private bool _isHovered;
    private int _hoveredIndex = -1;
    private int _activeCardIndex = -1;
    private float _nextHoverCheck;
    private Camera _runtimeCamera;
    private Transform _cachedParent;
    private CardView _previewCardView;
    private RectTransform _previewRectTransform;
    private CharacterCardDefinition _activePreviewDefinition;

    public void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _cachedParent = _rectTransform.parent;
        _random = new System.Random();
        InitializeCards();
        CacheCards();
        EnsureTopmost();
    }

    public void OnEnable()
    {
        CacheCards();
        EnsureTopmost();
    }

    private void OnTransformChildrenChanged()
    {
        InitializeCards();
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
        HidePreview();
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        _runtimeCamera = ResolveCamera(eventData);
        UpdateHoveredCard(eventData.position);
    }

    private void OnDisable()
    {
        _isHovered = false;
        _hoveredIndex = -1;
        _activeCardIndex = -1;
        _runtimeCamera = null;
        HidePreview();
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

    private void LateUpdate()
    {
        EnsureTopmost();
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

    private void InitializeCards()
    {
        if (_cardsInitialized || _initializationInProgress)
        {
            return;
        }

        _initializationInProgress = true;

        try
        {
            if (_random == null)
            {
                _random = new System.Random();
            }

            EnsureDeckContainer();
            LoadCardPrefab();
            LoadCardDefinitions();

            if (_cardDefinitions.Count == 0)
            {
                _cardsInitialized = true;
                return;
            }

            PrepareExistingHandCards();
            PopulateDrawPile();
            DealStartingHand();

            _cardsInitialized = true;
        }
        finally
        {
            _initializationInProgress = false;
        }
    }

    private void EnsureDeckContainer()
    {
        if (deckContainer != null)
        {
            return;
        }

        var deckObject = new GameObject("DeckContainer", typeof(RectTransform));
        deckContainer = deckObject.GetComponent<RectTransform>();

        Transform parent = _rectTransform != null && _rectTransform.parent != null
            ? _rectTransform.parent
            : transform.parent;

        if (parent != null)
        {
            deckContainer.SetParent(parent, false);
        }
        else
        {
            deckContainer.SetParent(transform, false);
        }

        deckContainer.anchorMin = Vector2.zero;
        deckContainer.anchorMax = Vector2.zero;
        deckContainer.pivot = new Vector2(0.5f, 0.5f);
        deckContainer.anchoredPosition = Vector2.zero;
        deckContainer.sizeDelta = Vector2.zero;
    }

    private void LoadCardPrefab()
    {
        if (_cardPrefab != null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(cardPrefabResourcePath))
        {
            Debug.LogError("Card prefab resource path is empty.");
            return;
        }

        _cardPrefab = Resources.Load<GameObject>(cardPrefabResourcePath);

        if (_cardPrefab == null)
        {
            Debug.LogError($"Card prefab could not be loaded from Resources/{cardPrefabResourcePath}.");
        }
    }

    private void LoadCardDefinitions()
    {
        _cardDefinitions.Clear();

        if (string.IsNullOrWhiteSpace(relationshipsResourcePath))
        {
            Debug.LogError("Relationships resource path is empty.");
            return;
        }

        TextAsset relationshipsAsset = Resources.Load<TextAsset>(relationshipsResourcePath);
        if (relationshipsAsset == null)
        {
            Debug.LogError($"Relationships data could not be loaded from Resources/{relationshipsResourcePath}.");
            return;
        }

        try
        {
            var database = JsonUtility.FromJson<CharacterRelationshipDatabase>(relationshipsAsset.text);
            if (database != null && database.characters != null)
            {
                _cardDefinitions.AddRange(database.characters);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to parse relationships data: {ex.Message}");
        }

        if (_cardDefinitions.Count > 1)
        {
            Shuffle(_cardDefinitions);
        }
    }

    private void PrepareExistingHandCards()
    {
        _handCards.Clear();

        var toDisable = new List<GameObject>();

        foreach (Transform child in transform)
        {
            if (child == null)
            {
                continue;
            }

            var dragHandler = child.GetComponent<CardDragHandler>();
            if (dragHandler == null)
            {
                continue;
            }

            var view = child.GetComponent<CardView>();
            if (view == null)
            {
                view = child.gameObject.AddComponent<CardView>();
            }

            _handCards.Add(view);
        }

        int configuredHandSize = Mathf.Min(startingHandSize, _cardDefinitions.Count);

        if (_handCards.Count > configuredHandSize)
        {
            for (int i = configuredHandSize; i < _handCards.Count; i++)
            {
                if (_handCards[i] != null)
                {
                    toDisable.Add(_handCards[i].gameObject);
                }
            }

            _handCards.RemoveRange(configuredHandSize, _handCards.Count - configuredHandSize);
        }

        if (_handCards.Count < configuredHandSize && _cardPrefab != null)
        {
            int cardsToCreate = configuredHandSize - _handCards.Count;
            for (int i = 0; i < cardsToCreate; i++)
            {
                GameObject cardObject = Instantiate(_cardPrefab, transform);
                var view = cardObject.GetComponent<CardView>();
                if (view == null)
                {
                    view = cardObject.AddComponent<CardView>();
                }

                _handCards.Add(view);
            }
        }

        foreach (GameObject obj in toDisable)
        {
            if (obj != null)
            {
                obj.SetActive(false);
            }
        }
    }

    private void PopulateDrawPile()
    {
        _drawPile.Clear();

        if (_cardPrefab == null)
        {
            return;
        }

        int startIndex = Mathf.Min(startingHandSize, _cardDefinitions.Count);

        for (int i = startIndex; i < _cardDefinitions.Count; i++)
        {
            CharacterCardDefinition definition = _cardDefinitions[i];
            GameObject cardObject = Instantiate(_cardPrefab, deckContainer);
            cardObject.name = definition != null ? definition.id : $"Card_{i}";

            var view = cardObject.GetComponent<CardView>();
            if (view == null)
            {
                view = cardObject.AddComponent<CardView>();
            }

            view.SetData(definition);
            cardObject.SetActive(false);
            _drawPile.Add(view);
        }

        if (_drawPile.Count > 1)
        {
            Shuffle(_drawPile);
        }
    }

    private void DealStartingHand()
    {
        if (_cardDefinitions.Count == 0 || _handCards.Count == 0)
        {
            return;
        }

        int cardsToDeal = Mathf.Min(startingHandSize, _cardDefinitions.Count, _handCards.Count);

        for (int i = 0; i < cardsToDeal; i++)
        {
            CharacterCardDefinition definition = _cardDefinitions[i];
            CardView view = _handCards[i];
            if (view == null)
            {
                continue;
            }

            view.gameObject.SetActive(true);
            view.SetData(definition);
            UpdateCardTransform(view.RectTransform);
        }

        if (_drawPile.Count > 0)
        {
            for (int i = _drawPile.Count - 1; i >= 0; i--)
            {
                _drawPile[i].gameObject.SetActive(false);
            }
        }
    }

    private void UpdateCardTransform(RectTransform rect)
    {
        if (rect == null)
        {
            return;
        }

        rect.SetParent(transform, false);
        rect.localScale = Vector3.one;
        rect.anchoredPosition = Vector2.zero;
        rect.localRotation = Quaternion.identity;
    }

    private RectTransform ResolvePreviewContainer()
    {
        if (previewContainer == null)
        {
            previewContainer = FindPreviewContainerInScene();
        }

        return previewContainer;
    }

    private CardView EnsurePreviewInstance()
    {
        if (_previewCardView != null)
        {
            ApplyPreviewTransform();
            return _previewCardView;
        }

        RectTransform container = ResolvePreviewContainer();
        if (container == null)
        {
            return null;
        }

        _previewRectTransform = container;
        ApplyPreviewTransform();

        CardDragHandler referenceHandler = null;
        var dragHandlers = container.GetComponentsInChildren<CardDragHandler>(true);
        foreach (CardDragHandler handler in dragHandlers)
        {
            if (handler != null)
            {
                handler.enabled = false;
                if (referenceHandler == null)
                {
                    referenceHandler = handler;
                }
            }
        }

        var raycasters = container.GetComponentsInChildren<GraphicRaycaster>(true);
        foreach (GraphicRaycaster raycaster in raycasters)
        {
            if (raycaster != null)
            {
                raycaster.enabled = false;
            }
        }

        CanvasGroup group = container.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = container.gameObject.AddComponent<CanvasGroup>();
        }

        group.interactable = false;
        group.blocksRaycasts = false;
        group.alpha = 1f;

        _previewCardView = container.GetComponentInChildren<CardView>(true);
        if (_previewCardView == null)
        {
            Transform targetTransform = referenceHandler != null ? referenceHandler.transform : container.transform;
            _previewCardView = targetTransform.GetComponent<CardView>();
            if (_previewCardView == null)
            {
                _previewCardView = targetTransform.gameObject.AddComponent<CardView>();
            }
        }

        container.gameObject.SetActive(false);
        return _previewCardView;
    }

    private RectTransform FindPreviewContainerInScene()
    {
        if (!gameObject.scene.IsValid())
        {
            return null;
        }

        foreach (GameObject rootObject in gameObject.scene.GetRootGameObjects())
        {
            RectTransform candidate = FindPreviewContainerRecursive(rootObject.transform);
            if (candidate != null)
            {
                return candidate;
            }
        }

        return null;
    }

    private RectTransform FindPreviewContainerRecursive(Transform current)
    {
        if (current == null)
        {
            return null;
        }

        if (string.Equals(current.name, "CardPreview", StringComparison.Ordinal))
        {
            return current as RectTransform;
        }

        for (int i = 0; i < current.childCount; i++)
        {
            RectTransform found = FindPreviewContainerRecursive(current.GetChild(i));
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private void ApplyPreviewTransform()
    {
        if (_previewRectTransform == null)
        {
            return;
        }

        _previewRectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        _previewRectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        _previewRectTransform.pivot = new Vector2(0.5f, 0.5f);
        _previewRectTransform.anchoredPosition = previewOffset;

        float scale = Mathf.Approximately(previewScale, 0f) ? 1f : previewScale;
        _previewRectTransform.localScale = Vector3.one * scale;

        RectTransform container = ResolvePreviewContainer();
        if (container != null)
        {
            container.SetAsLastSibling();
        }

        if (_previewRectTransform.parent != null)
        {
            _previewRectTransform.SetAsLastSibling();
        }
    }

    private void ShowPreviewForHoveredCard()
    {
        if (_hoveredIndex < 0 || _hoveredIndex >= _handCards.Count)
        {
            HidePreview();
            return;
        }

        CardView sourceView = _handCards[_hoveredIndex];
        if (sourceView == null || sourceView.Definition == null)
        {
            HidePreview();
            return;
        }

        if (_activePreviewDefinition == sourceView.Definition && _previewRectTransform != null && _previewRectTransform.gameObject.activeSelf)
        {
            return;
        }

        CardView previewView = EnsurePreviewInstance();
        if (previewView == null)
        {
            return;
        }

        _activePreviewDefinition = sourceView.Definition;
        previewView.SetData(_activePreviewDefinition);
        ApplyPreviewTransform();

        if (_previewRectTransform != null && !_previewRectTransform.gameObject.activeSelf)
        {
            _previewRectTransform.gameObject.SetActive(true);
        }

        if (!previewView.gameObject.activeSelf)
        {
            previewView.gameObject.SetActive(true);
        }
    }

    private void HidePreview()
    {
        _activePreviewDefinition = null;

        if (_previewRectTransform != null)
        {
            _previewRectTransform.gameObject.SetActive(false);
        }
    }

    private void Shuffle<T>(IList<T> collection)
    {
        if (collection == null)
        {
            return;
        }

        for (int i = collection.Count - 1; i > 0; i--)
        {
            int swapIndex = _random.Next(i + 1);
            (collection[i], collection[swapIndex]) = (collection[swapIndex], collection[i]);
        }
    }

    private void UpdateHoveredCard(Vector2 pointerPosition)
    {
        if (_cardRects.Count == 0)
        {
            _hoveredIndex = -1;
            _activeCardIndex = -1;
            HidePreview();
            return;
        }

        int detectedIndex = DetermineHoveredCard(pointerPosition);

        if (detectedIndex != -1)
        {
            _hoveredIndex = detectedIndex;
            _activeCardIndex = detectedIndex;
            ShowPreviewForHoveredCard();
        }
        else
        {
            if (_hoveredIndex != -1)
            {
                HidePreview();
            }
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
        int activeCount = 0;
        for (int i = 0; i < _cardRects.Count; i++)
        {
            if (!IsRectTransformBeingDragged(_cardRects[i]))
            {
                activeCount++;
            }
        }

        if (activeCount == 0)
        {
            return;
        }

        float totalWidth = spacing * (activeCount - 1);
        float startX = -totalWidth * 0.5f;

        int raisedIndex = -1;
        if (_isHovered && _activeCardIndex >= 0 && _activeCardIndex < _cardRects.Count)
        {
            RectTransform candidate = _cardRects[_activeCardIndex];
            if (!IsRectTransformBeingDragged(candidate))
            {
                raisedIndex = _activeCardIndex;
            }
        }

        int layoutIndex = 0;
        for (int i = 0; i < _cardRects.Count; i++)
        {
            RectTransform card = _cardRects[i];
            if (IsRectTransformBeingDragged(card))
            {
                continue;
            }

            float targetX = startX + spacing * layoutIndex;
            float targetY = 0f;

            if (raisedIndex == i)
            {
                targetY += hoverYOffset;
            }

            Vector2 target = new Vector2(targetX, targetY);
            Vector2 current = card.anchoredPosition;
            Vector2 velocity = _velocity[i];

            Vector2 newPosition = Vector2.SmoothDamp(current, target, ref velocity, positionSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
            card.anchoredPosition = newPosition;
            _velocity[i] = velocity;

            layoutIndex++;
        }
    }

    private static bool IsRectTransformBeingDragged(RectTransform rect)
    {
        if (rect == null)
        {
            return false;
        }

        CardDragHandler handler = rect.GetComponent<CardDragHandler>();
        return handler != null && handler.IsDragging;
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

    private void EnsureTopmost()
    {
        if (_rectTransform == null)
        {
            return;
        }

        if (_cachedParent != _rectTransform.parent)
        {
            _cachedParent = _rectTransform.parent;
        }

        if (_cachedParent == null)
        {
            return;
        }

        int lastIndex = _cachedParent.childCount - 1;
        if (_rectTransform.GetSiblingIndex() != lastIndex)
        {
            _rectTransform.SetSiblingIndex(lastIndex);
        }
    }
}
