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

    private Transform CardParent => cardParent != null ? cardParent : transform;

    public Transform GetCardParent()
    {
        return CardParent;
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
