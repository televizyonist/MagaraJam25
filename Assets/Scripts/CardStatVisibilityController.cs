using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class CardStatVisibilityController : MonoBehaviour
{
    [Header("Stat Roots")]
    [SerializeField] private GameObject extraAttackRoot;
    [SerializeField] private GameObject aoeRoot;
    [SerializeField] private GameObject regenerationRoot;
    [SerializeField] private GameObject luckRoot;
    [SerializeField] private GameObject scoreRoot;

    private readonly List<GameObject> _bonusStatRoots = new List<GameObject>(4);
    private CardDragHandler _dragHandler;

    private void Awake()
    {
        _dragHandler = GetComponent<CardDragHandler>();
        EnsureReferences();
        CacheBonusStatRoots();
        UpdateVisibility();
    }

    private void OnEnable()
    {
        if (_dragHandler == null)
        {
            _dragHandler = GetComponent<CardDragHandler>();
        }

        EnsureReferences();
        CacheBonusStatRoots();
        UpdateVisibility();
    }

    private void OnTransformParentChanged()
    {
        if (_dragHandler != null && _dragHandler.IsDragging)
        {
            return;
        }

        UpdateVisibility();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        EnsureReferences();
        CacheBonusStatRoots();
        if (!Application.isPlaying)
        {
            return;
        }

        UpdateVisibility();
    }
#endif

    private void EnsureReferences()
    {
        if (extraAttackRoot == null)
        {
            extraAttackRoot = FindChildGameObject("Canvas/Extra Attack");
        }

        if (aoeRoot == null)
        {
            aoeRoot = FindChildGameObject("Canvas/AOE");
        }

        if (regenerationRoot == null)
        {
            regenerationRoot = FindChildGameObject("Canvas/Regeneration");
        }

        if (luckRoot == null)
        {
            luckRoot = FindChildGameObject("Canvas/Luck");
        }

        if (scoreRoot == null)
        {
            scoreRoot = FindChildGameObject("Canvas/Score");
        }
    }

    private GameObject FindChildGameObject(string path)
    {
        Transform child = transform.Find(path);
        return child != null ? child.gameObject : null;
    }

    private void CacheBonusStatRoots()
    {
        _bonusStatRoots.Clear();
        AddBonusRoot(extraAttackRoot);
        AddBonusRoot(aoeRoot);
        AddBonusRoot(regenerationRoot);
        AddBonusRoot(luckRoot);
    }

    private void AddBonusRoot(GameObject root)
    {
        if (root == null || _bonusStatRoots.Contains(root))
        {
            return;
        }

        _bonusStatRoots.Add(root);
    }

    private void UpdateVisibility()
    {
        EnsureReferences();

        CardSlot slot = GetActiveSlot();
        CardContext context = DetermineContext(slot);
        ApplyContext(context);
    }

    private CardSlot GetActiveSlot()
    {
        if (_dragHandler != null && _dragHandler.CurrentSlot != null)
        {
            return _dragHandler.CurrentSlot;
        }

        return GetComponentInParent<CardSlot>();
    }

    private CardContext DetermineContext(CardSlot slot)
    {
        if (slot != null && TryGetSlotIndex(slot.gameObject.name, out int slotIndex))
        {
            if (slotIndex >= 4 && slotIndex <= 6)
            {
                return CardContext.BackSlot;
            }

            if (slotIndex >= 1 && slotIndex <= 3)
            {
                return CardContext.FrontSlot;
            }
        }

        if (GetComponentInParent<HandAreaHover>() != null)
        {
            return CardContext.Hand;
        }

        return CardContext.Other;
    }

    private bool TryGetSlotIndex(string slotName, out int slotIndex)
    {
        slotIndex = -1;
        if (string.IsNullOrEmpty(slotName))
        {
            return false;
        }

        int startIndex = -1;
        for (int i = 0; i < slotName.Length; i++)
        {
            if (char.IsDigit(slotName[i]))
            {
                startIndex = i;
                break;
            }
        }

        if (startIndex < 0)
        {
            return false;
        }

        int endIndex = startIndex;
        while (endIndex < slotName.Length && char.IsDigit(slotName[endIndex]))
        {
            endIndex++;
        }

        string numberPart = slotName.Substring(startIndex, endIndex - startIndex);
        return int.TryParse(numberPart, out slotIndex);
    }

    private void ApplyContext(CardContext context)
    {
        bool showBonusStats = context == CardContext.BackSlot;
        bool showScore = context != CardContext.BackSlot;

        SetBonusStatsActive(showBonusStats);
        SetActive(scoreRoot, showScore);
    }

    private void SetBonusStatsActive(bool isActive)
    {
        for (int i = 0; i < _bonusStatRoots.Count; i++)
        {
            SetActive(_bonusStatRoots[i], isActive);
        }
    }

    private void SetActive(GameObject target, bool isActive)
    {
        if (target != null && target.activeSelf != isActive)
        {
            target.SetActive(isActive);
        }
    }

    private enum CardContext
    {
        Hand,
        FrontSlot,
        BackSlot,
        Other
    }
}
