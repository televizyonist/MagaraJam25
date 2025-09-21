using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BattleResolver : MonoBehaviour
{
    private static readonly Dictionary<string, CharacterStats.StatType> RelationStatMap = new Dictionary<string, CharacterStats.StatType>
    {
        { "baba", CharacterStats.StatType.Attack },
        { "evlat", CharacterStats.StatType.Health },
        { "kardes", CharacterStats.StatType.ExtraAttack },
        { "anne", CharacterStats.StatType.Armor },
        { "arkadas", CharacterStats.StatType.Luck },
        { "es", CharacterStats.StatType.Regeneration },
        { "akraba", CharacterStats.StatType.AreaDamage }
    };

    private readonly List<SlotBattleState> _slotStates = new List<SlotBattleState>();

    public void ExecuteBattle()
    {
        BuildSlotStates();

        if (_slotStates.Count == 0)
        {
            return;
        }

        for (int i = 0; i < _slotStates.Count; i++)
        {
            SlotBattleState source = _slotStates[i];
            if (!source.HasCardData)
            {
                continue;
            }

            for (int j = i + 1; j < _slotStates.Count; j++)
            {
                SlotBattleState target = _slotStates[j];
                if (!target.HasCardData)
                {
                    continue;
                }

                string relation = ResolveRelationship(source.Definition, target.Definition);
                if (string.IsNullOrWhiteSpace(relation))
                {
                    continue;
                }

                if (!TryGetRelationStat(relation, out CharacterStats.StatType statType))
                {
                    continue;
                }

                int scoreValue = source.GetScoreValue();
                if (scoreValue != 0)
                {
                    target.AddContribution(statType, scoreValue);
                }

                target.AbsorbContributions(source);
            }
        }

        for (int i = 0; i < _slotStates.Count; i++)
        {
            _slotStates[i].ApplyFinalStats();
        }
    }

    public void OnBattleButtonPressed()
    {
        ExecuteBattle();
    }

    private void BuildSlotStates()
    {
        _slotStates.Clear();

        CardSlot[] slots = GetComponentsInChildren<CardSlot>(true);
        if (slots == null || slots.Length == 0)
        {
            return;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            CardSlot slot = slots[i];
            if (slot == null)
            {
                continue;
            }

            if (!TryParseSlotIndex(slot.gameObject.name, out int slotIndex))
            {
                continue;
            }

            CardView view = GetActiveCardView(slot);
            if (view == null)
            {
                continue;
            }

            var state = new SlotBattleState(slotIndex, slot, view);
            state.Initialize();
            if (state.HasCardData)
            {
                _slotStates.Add(state);
            }
        }

        if (_slotStates.Count > 1)
        {
            _slotStates.Sort((a, b) => a.Index.CompareTo(b.Index));
        }
    }

    private CardView GetActiveCardView(CardSlot slot)
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
            if (child == null || !child.gameObject.activeInHierarchy)
            {
                continue;
            }

            CardDragHandler handler = child.GetComponent<CardDragHandler>();
            if (handler == null || handler.CurrentSlot != slot)
            {
                continue;
            }

            CardView view = handler.GetComponent<CardView>();
            if (view == null)
            {
                view = handler.GetComponentInChildren<CardView>(true);
            }

            if (view != null && view.gameObject.activeInHierarchy)
            {
                return view;
            }
        }

        return null;
    }

    private string ResolveRelationship(CharacterCardDefinition from, CharacterCardDefinition to)
    {
        if (from == null || to == null)
        {
            return string.Empty;
        }

        string relation = GetDirectRelationship(from, to);
        if (!string.IsNullOrWhiteSpace(relation))
        {
            return relation;
        }

        return GetDirectRelationship(to, from);
    }

    private string GetDirectRelationship(CharacterCardDefinition from, CharacterCardDefinition to)
    {
        if (from == null || to == null)
        {
            return string.Empty;
        }

        RelationshipInfo info = from.GetRelationshipById(to.id);
        if (info != null && !string.IsNullOrWhiteSpace(info.relation))
        {
            return info.relation;
        }

        return string.Empty;
    }

    private bool TryGetRelationStat(string relation, out CharacterStats.StatType statType)
    {
        statType = default;
        if (string.IsNullOrWhiteSpace(relation))
        {
            return false;
        }

        string key = NormalizeRelationshipKey(relation);
        return RelationStatMap.TryGetValue(key, out statType);
    }

    private static string NormalizeRelationshipKey(string value)
    {
        string normalized = value.Trim().ToLowerInvariant();
        normalized = normalized.Replace('ı', 'i')
            .Replace('ş', 's')
            .Replace('ğ', 'g')
            .Replace('ü', 'u')
            .Replace('ö', 'o')
            .Replace('ç', 'c');
        return normalized;
    }

    private bool TryParseSlotIndex(string slotName, out int index)
    {
        index = 0;
        if (string.IsNullOrWhiteSpace(slotName))
        {
            return false;
        }

        const string prefix = "Slot ";
        if (!slotName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string suffix = slotName.Substring(prefix.Length).Trim();
        return int.TryParse(suffix, out index);
    }

    private sealed class SlotBattleState
    {
        private readonly Dictionary<CharacterStats.StatType, int> _contributions = new Dictionary<CharacterStats.StatType, int>();

        public SlotBattleState(int index, CardSlot slot, CardView view)
        {
            Index = index;
            Slot = slot;
            View = view;
        }

        public int Index { get; }
        public CardSlot Slot { get; }
        public CardView View { get; }
        public CharacterCardDefinition Definition { get; private set; }
        public CharacterStats BaseStats { get; private set; }
        public CharacterStats FinalStats { get; private set; }

        public bool HasCardData => View != null && Definition != null;

        public void Initialize()
        {
            _contributions.Clear();

            if (View == null)
            {
                Definition = null;
                BaseStats = null;
                FinalStats = null;
                return;
            }

            Definition = View.Definition;
            View.ResetToBaseStats();
            BaseStats = View.GetBaseStatsClone();
            FinalStats = BaseStats != null ? new CharacterStats(BaseStats) : new CharacterStats();
        }

        public int GetScoreValue()
        {
            return BaseStats != null ? BaseStats.score : 0;
        }

        public void AddContribution(CharacterStats.StatType statType, int amount)
        {
            if (amount == 0)
            {
                return;
            }

            if (_contributions.TryGetValue(statType, out int existing))
            {
                _contributions[statType] = existing + amount;
            }
            else
            {
                _contributions[statType] = amount;
            }

            if (FinalStats == null)
            {
                FinalStats = new CharacterStats();
            }

            FinalStats.AddToStat(statType, amount);
        }

        public void AbsorbContributions(SlotBattleState source)
        {
            if (source == null || source._contributions.Count == 0)
            {
                return;
            }

            foreach (KeyValuePair<CharacterStats.StatType, int> pair in source._contributions)
            {
                AddContribution(pair.Key, pair.Value);
            }
        }

        public void ApplyFinalStats()
        {
            if (View == null)
            {
                return;
            }

            View.ApplyStats(FinalStats);
        }
    }
}

