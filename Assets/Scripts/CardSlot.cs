using System;
using UnityEngine;

public class CardSlot : MonoBehaviour
{
    [Header("Slot Configuration")]
    [Tooltip("Optional transform that will become the parent of any dropped cards. If empty, the card will be parented to this transform.")]
    public Transform cardParent;

    [Tooltip("When disabled only one card can be held by this slot at a time.")]
    public bool allowMultipleCards = false;

    [Tooltip("Anchored position applied to the card when it is dropped on this slot.")]
    public Vector2 dropOffset = Vector2.zero;

    [Header("Visuals")]
    [Tooltip("Optional object that is activated to highlight this slot. Defaults to a child named 'Glow'.")]
    [SerializeField]
    private GameObject glowRoot;

    private Transform CardParent => cardParent != null ? cardParent : transform;

    public Transform GetCardParent()
    {
        return CardParent;
    }

    private void Awake()
    {
        EnsureGlowReference();
        SetGlowActive(false);
    }

    private void Reset()
    {
        EnsureGlowReference();
        SetGlowActive(false);
    }

    private void OnEnable()
    {
        EnsureGlowReference();
        SetGlowActive(false);
    }

    private void EnsureGlowReference()
    {
        if (glowRoot != null)
        {
            return;
        }

        Transform found = transform.Find("Glow");
        if (found == null)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child == null)
                {
                    continue;
                }

                if (string.Equals(child.name, "Glow", StringComparison.OrdinalIgnoreCase))
                {
                    found = child;
                    break;
                }
            }
        }

        if (found != null)
        {
            glowRoot = found.gameObject;
        }
    }

    public void SetGlowActive(bool isActive)
    {
        EnsureGlowReference();
        if (glowRoot == null)
        {
            return;
        }

        if (glowRoot.activeSelf != isActive)
        {
            glowRoot.SetActive(isActive);
        }
    }

    public bool TryAccept(CardDragHandler card)
    {
        if (card == null)
        {
            return false;
        }

        CardDragHandler existingCard = allowMultipleCards ? null : FindExistingCard(card);
        if (!allowMultipleCards && existingCard != null)
        {
            HandleOccupiedSlot(card, existingCard);
        }

        card.CompleteSlotDrop(this, CardParent, dropOffset);
        return true;
    }

    private CardDragHandler FindExistingCard(CardDragHandler ignoredCard)
    {
        foreach (Transform child in CardParent)
        {
            if (child == null || child == ignoredCard.transform)
            {
                continue;
            }

            CardDragHandler handler = child.GetComponent<CardDragHandler>();
            if (handler != null)
            {
                return handler;
            }
        }

        return null;
    }

    private void HandleOccupiedSlot(CardDragHandler incomingCard, CardDragHandler existingCard)
    {
        if (incomingCard == null || existingCard == null)
        {
            return;
        }

        CardSlot originSlot = incomingCard.OriginalSlot;
        Transform originParent = incomingCard.OriginalParent;
        Vector2 originAnchoredPosition = incomingCard.OriginalAnchoredPosition;
        int originSiblingIndex = incomingCard.OriginalSiblingIndex;

        if (originSlot != null && originSlot != this)
        {
            existingCard.CompleteSlotDrop(originSlot, originSlot.GetCardParent(), originSlot.dropOffset);
        }
        else if (originParent != null)
        {
            existingCard.CompleteNonSlotDrop(originParent, originAnchoredPosition, originSiblingIndex);
        }
        else
        {
            existingCard.RestoreOriginalPlacement();
        }
    }
}
