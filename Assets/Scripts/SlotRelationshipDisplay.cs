using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class SlotRelationshipDisplay : MonoBehaviour
{
    private readonly Dictionary<int, CardSlot> _slotsByIndex = new Dictionary<int, CardSlot>();
    private readonly List<CardSlot> _allSlots = new List<CardSlot>();
    private readonly List<RelationshipConnection> _connections = new List<RelationshipConnection>();

    private CardDragHandler _hoveredCard;
    private CharacterCardDefinition _hoveredCardDefinition;
    private CardDragHandler _hoveredBoardCard;
    private CharacterCardDefinition _hoveredBoardDefinition;
    private CardSlot _hoveredBoardSlot;
    private CardDragHandler _draggedCard;
    private CharacterCardDefinition _draggedCardDefinition;

    private void Awake()
    {
        Initialize();
        UpdateConnections();
    }

    private void OnEnable()
    {
        CardDragHandler.PointerEntered += HandleCardPointerEnter;
        CardDragHandler.PointerExited += HandleCardPointerExit;
        CardDragHandler.DragStarted += HandleCardDragStarted;
        CardDragHandler.DragEnded += HandleCardDragEnded;

        Initialize();
        UpdateConnections();
    }

    private void OnDisable()
    {
        CardDragHandler.PointerEntered -= HandleCardPointerEnter;
        CardDragHandler.PointerExited -= HandleCardPointerExit;
        CardDragHandler.DragStarted -= HandleCardDragStarted;
        CardDragHandler.DragEnded -= HandleCardDragEnded;

        ClearHoverStates();
        UpdateConnections();
    }

    private void Update()
    {
        UpdateConnections();
    }

    private void Initialize()
    {
        ClearHoverStates();
        _slotsByIndex.Clear();
        _allSlots.Clear();
        CacheSlots();

        _connections.Clear();
        CacheConnections();

        foreach (RelationshipConnection connection in _connections)
        {
            SetConnectionActive(connection, false);
        }

        UpdateSlotGlows(null);
    }

    private void CacheSlots()
    {
        var slots = GetComponentsInChildren<CardSlot>(true);
        foreach (CardSlot slot in slots)
        {
            if (slot == null)
            {
                continue;
            }

            if (TryParseSlotIndex(slot.gameObject.name, out int index) && !_slotsByIndex.ContainsKey(index))
            {
                _slotsByIndex.Add(index, slot);
            }

            if (!_allSlots.Contains(slot))
            {
                _allSlots.Add(slot);
            }
        }
    }

    private void CacheConnections()
    {
        var texts = GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text text in texts)
        {
            if (text == null)
            {
                continue;
            }

            if (!TryParseConnectionName(text.gameObject.name, out int fromIndex, out int toIndex))
            {
                continue;
            }

            if (!_slotsByIndex.TryGetValue(fromIndex, out CardSlot fromSlot))
            {
                continue;
            }

            if (!_slotsByIndex.TryGetValue(toIndex, out CardSlot toSlot))
            {
                continue;
            }

            _connections.Add(new RelationshipConnection(fromSlot, toSlot, text));
        }
    }

    private void UpdateConnections()
    {
        if (_connections.Count == 0)
        {
            UpdateSlotGlows(null);
            return;
        }

        HashSet<CardSlot> highlightedSlots = null;
        foreach (RelationshipConnection connection in _connections)
        {
            UpdateConnection(connection);

            if (!connection.HighlightFrom && !connection.HighlightTo)
            {
                continue;
            }

            if (highlightedSlots == null)
            {
                highlightedSlots = new HashSet<CardSlot>();
            }

            if (connection.HighlightFrom && connection.FromSlot != null)
            {
                highlightedSlots.Add(connection.FromSlot);
            }

            if (connection.HighlightTo && connection.ToSlot != null)
            {
                highlightedSlots.Add(connection.ToSlot);
            }
        }

        UpdateSlotGlows(highlightedSlots);
    }

    private bool UpdateConnection(RelationshipConnection connection)
    {
        if (connection == null)
        {
            return false;
        }

        string previousRelationship = connection.RelationshipText;
        if (string.IsNullOrWhiteSpace(previousRelationship))
        {
            previousRelationship = connection.LastText;
        }

        CharacterCardDefinition fromDefinition = GetCardDefinition(connection.FromSlot);
        CharacterCardDefinition toDefinition = GetCardDefinition(connection.ToSlot);
        string slotRelation = string.Empty;
        if (fromDefinition != null && toDefinition != null)
        {
            string relationFromTo = GetDirectRelationship(fromDefinition, toDefinition);
            string relationToFrom = GetDirectRelationship(toDefinition, fromDefinition);
            slotRelation = SelectPreferredRelationshipText(relationFromTo, relationToFrom, previousRelationship);
        }

        CharacterCardDefinition activeDefinition = _hoveredCardDefinition ?? _draggedCardDefinition;
        CardSlot focusedSlot = _hoveredBoardSlot;

        string relationWithFrom = string.Empty;
        string relationWithTo = string.Empty;
        bool activeMatchesFrom = false;
        bool activeMatchesTo = false;

        if (activeDefinition != null)
        {
            if (fromDefinition != null)
            {
                relationWithFrom = ResolveRelationship(activeDefinition, fromDefinition);
                activeMatchesFrom = !string.IsNullOrWhiteSpace(relationWithFrom);
            }

            if (toDefinition != null)
            {
                relationWithTo = ResolveRelationship(activeDefinition, toDefinition);
                activeMatchesTo = !string.IsNullOrWhiteSpace(relationWithTo);
            }

            if (focusedSlot != null)
            {
                if (connection.FromSlot != focusedSlot)
                {
                    activeMatchesFrom = false;
                }

                if (connection.ToSlot != focusedSlot)
                {
                    activeMatchesTo = false;
                }
            }
        }

        bool slotsHaveRelationship = !string.IsNullOrWhiteSpace(slotRelation);
        bool hoverProvidesRelationship = activeMatchesFrom || activeMatchesTo;

        string relationToDisplay = slotRelation;
        if (string.IsNullOrWhiteSpace(relationToDisplay))
        {
            if (activeMatchesFrom)
            {
                relationToDisplay = relationWithFrom;
            }
            else if (activeMatchesTo)
            {
                relationToDisplay = relationWithTo;
            }
        }

        bool highlightFrom = false;
        bool highlightTo = false;

        if (activeMatchesFrom)
        {
            if (connection.FromSlot != null)
            {
                highlightFrom = true;
            }

            if (connection.ToSlot != null)
            {
                highlightTo = true;
            }
        }

        if (activeMatchesTo)
        {
            if (connection.ToSlot != null)
            {
                highlightTo = true;
            }

            if (connection.FromSlot != null)
            {
                highlightFrom = true;
            }
        }

        bool shouldDisplayLabel = slotsHaveRelationship || hoverProvidesRelationship;

        connection.RelationshipText = relationToDisplay;
        connection.HighlightFrom = highlightFrom;
        connection.HighlightTo = highlightTo;

        SetConnectionActive(connection, shouldDisplayLabel);

        if (shouldDisplayLabel)
        {
            SetConnectionText(connection, relationToDisplay);
        }

        return shouldDisplayLabel;
    }

    private void UpdateSlotGlows(ISet<CardSlot> highlightedSlots)
    {
        foreach (CardSlot slot in _allSlots)
        {
            if (slot == null)
            {
                continue;
            }

            bool shouldGlow = highlightedSlots != null && highlightedSlots.Contains(slot);
            slot.SetGlowActive(shouldGlow);
        }
    }

    private void HandleCardPointerEnter(CardDragHandler handler)
    {
        if (handler == null)
        {
            return;
        }

        if (handler == _draggedCard)
        {
            return;
        }

        if (IsHandCard(handler))
        {
            CharacterCardDefinition definition = GetCardDefinition(handler);
            if (definition == null)
            {
                return;
            }

            _hoveredCard = handler;
            _hoveredCardDefinition = definition;
            _hoveredBoardCard = null;
            _hoveredBoardDefinition = null;
            _hoveredBoardSlot = null;
            UpdateConnections();
            return;
        }

        if (_draggedCardDefinition == null)
        {
            return;
        }

        CardSlot slot = handler.CurrentSlot;
        if (slot == null)
        {
            return;
        }

        CharacterCardDefinition boardDefinition = GetCardDefinition(handler);
        if (boardDefinition == null)
        {
            return;
        }

        _hoveredBoardCard = handler;
        _hoveredBoardDefinition = boardDefinition;
        _hoveredBoardSlot = slot;
        UpdateConnections();
    }

    private void HandleCardPointerExit(CardDragHandler handler)
    {
        if (handler == null)
        {
            return;
        }

        bool stateChanged = false;

        if (handler == _hoveredCard)
        {
            _hoveredCard = null;
            _hoveredCardDefinition = null;
            stateChanged = true;
        }

        if (handler == _hoveredBoardCard)
        {
            _hoveredBoardCard = null;
            _hoveredBoardDefinition = null;
            _hoveredBoardSlot = null;
            stateChanged = true;
        }

        if (stateChanged)
        {
            UpdateConnections();
        }
    }

    private void HandleCardDragStarted(CardDragHandler handler)
    {
        if (handler == null)
        {
            return;
        }

        _draggedCard = handler;
        _draggedCardDefinition = GetCardDefinition(handler);

        if (_hoveredCard == handler)
        {
            _hoveredCard = null;
            _hoveredCardDefinition = null;
        }

        if (_hoveredBoardCard == handler)
        {
            _hoveredBoardCard = null;
            _hoveredBoardDefinition = null;
            _hoveredBoardSlot = null;
        }

        UpdateConnections();
    }

    private void HandleCardDragEnded(CardDragHandler handler)
    {
        if (handler == null || handler != _draggedCard)
        {
            return;
        }

        _draggedCard = null;
        _draggedCardDefinition = null;
        _hoveredBoardCard = null;
        _hoveredBoardDefinition = null;
        _hoveredBoardSlot = null;
        UpdateConnections();
    }

    private bool IsHandCard(CardDragHandler handler)
    {
        if (handler == null)
        {
            return false;
        }

        return handler.GetComponentInParent<HandAreaHover>() != null;
    }

    private CharacterCardDefinition GetCardDefinition(CardSlot slot)
    {
        if (slot == null)
        {
            return null;
        }

        Transform parent = slot.GetCardParent();
        if (parent == null)
        {
            return null;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child == null)
            {
                continue;
            }

            CardDragHandler handler = child.GetComponent<CardDragHandler>();
            if (handler == null)
            {
                continue;
            }

            if (handler.CurrentSlot != slot)
            {
                continue;
            }

            CardView view = handler.GetComponent<CardView>();
            if (view == null)
            {
                continue;
            }

            if (!view.gameObject.activeInHierarchy)
            {
                continue;
            }

            return view.Definition;
        }

        return null;
    }

    private CharacterCardDefinition GetCardDefinition(CardDragHandler handler)
    {
        if (handler == null)
        {
            return null;
        }

        CardView view = handler.GetComponent<CardView>();
        if (view == null)
        {
            view = handler.GetComponentInChildren<CardView>();
        }

        if (view == null || !view.gameObject.activeInHierarchy)
        {
            return null;
        }

        return view.Definition;
    }

    private string ResolveRelationship(CharacterCardDefinition from, CharacterCardDefinition to)
    {
        if (from == null || to == null)
        {
            return string.Empty;
        }

        string directRelationship = GetDirectRelationship(from, to);
        if (!string.IsNullOrWhiteSpace(directRelationship))
        {
            return directRelationship;
        }

        return GetDirectRelationship(to, from);
    }

    private string GetDirectRelationship(CharacterCardDefinition from, CharacterCardDefinition to)
    {
        if (from == null || to == null)
        {
            return string.Empty;
        }

        RelationshipInfo relationship = from.GetRelationshipById(to.id);
        if (relationship != null && !string.IsNullOrWhiteSpace(relationship.relation))
        {
            return relationship.relation;
        }

        return string.Empty;
    }

    private string SelectPreferredRelationshipText(string relationFromTo, string relationToFrom, string previousRelationship)
    {
        if (!string.IsNullOrWhiteSpace(previousRelationship))
        {
            if (!string.IsNullOrWhiteSpace(relationFromTo) && string.Equals(previousRelationship, relationFromTo, StringComparison.Ordinal))
            {
                return relationFromTo;
            }

            if (!string.IsNullOrWhiteSpace(relationToFrom) && string.Equals(previousRelationship, relationToFrom, StringComparison.Ordinal))
            {
                return relationToFrom;
            }
        }

        if (!string.IsNullOrWhiteSpace(relationFromTo))
        {
            return relationFromTo;
        }

        if (!string.IsNullOrWhiteSpace(relationToFrom))
        {
            return relationToFrom;
        }

        return string.Empty;
    }

    private void SetConnectionActive(RelationshipConnection connection, bool isActive)
    {
        if (connection.LastActive != isActive)
        {
            connection.LastActive = isActive;

            if (connection.Label != null)
            {
                connection.Label.gameObject.SetActive(isActive);
            }
        }

        if (!isActive)
        {
            SetConnectionText(connection, string.Empty);
        }
    }

    private void SetConnectionText(RelationshipConnection connection, string text)
    {
        string newText = text ?? string.Empty;
        if (string.Equals(connection.LastText, newText, StringComparison.Ordinal))
        {
            return;
        }

        connection.LastText = newText;

        if (connection.Label != null)
        {
            connection.Label.text = newText;
        }
    }

    private void ClearHoverStates()
    {
        _hoveredCard = null;
        _hoveredCardDefinition = null;
        _hoveredBoardCard = null;
        _hoveredBoardDefinition = null;
        _hoveredBoardSlot = null;
        _draggedCard = null;
        _draggedCardDefinition = null;
    }

    private bool TryParseSlotIndex(string name, out int index)
    {
        index = 0;
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        const string prefix = "Slot ";
        if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string suffix = name.Substring(prefix.Length).Trim();
        return int.TryParse(suffix, out index);
    }

    private bool TryParseConnectionName(string name, out int fromIndex, out int toIndex)
    {
        fromIndex = 0;
        toIndex = 0;

        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        string trimmed = name.Trim();
        int separatorIndex = trimmed.IndexOf(" to ", StringComparison.OrdinalIgnoreCase);
        if (separatorIndex <= 0)
        {
            return false;
        }

        string fromPart = trimmed.Substring(0, separatorIndex).Trim();
        string toPart = trimmed.Substring(separatorIndex + 4).Trim();

        return int.TryParse(fromPart, out fromIndex) && int.TryParse(toPart, out toIndex);
    }

    private sealed class RelationshipConnection
    {
        public RelationshipConnection(CardSlot fromSlot, CardSlot toSlot, TMP_Text label)
        {
            FromSlot = fromSlot;
            ToSlot = toSlot;
            Label = label;
            LastActive = label != null && label.gameObject.activeSelf;
            LastText = label != null ? label.text : string.Empty;
        }

        public CardSlot FromSlot { get; }
        public CardSlot ToSlot { get; }
        public TMP_Text Label { get; }
        public bool LastActive { get; set; }
        public string LastText { get; set; }
        public string RelationshipText { get; set; }
        public bool HighlightFrom { get; set; }
        public bool HighlightTo { get; set; }
    }
}
