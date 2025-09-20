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

    public bool TryAccept(CardDragHandler card)
    {
        if (card == null)
        {
            return false;
        }

        if (!allowMultipleCards && HasAnotherCard(card))
        {
            return false;
        }

        card.CompleteSlotDrop(this, CardParent, dropOffset);
        return true;
    }

    private bool HasAnotherCard(CardDragHandler ignoredCard)
    {
        foreach (Transform child in CardParent)
        {
            if (child == null || child == ignoredCard.transform)
            {
                continue;
            }

            if (child.GetComponent<CardDragHandler>() != null)
            {
                return true;
            }
        }

        return false;
    }
}
