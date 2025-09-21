using System;
using System.Collections.Generic;
using System.Text;
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

    [SerializeField] private bool enableDebugLogging = true;

    private const string LogPrefix = "[BattleResolver]";

    private readonly List<SlotBattleState> _slotStates = new List<SlotBattleState>();

    public void ExecuteBattle()
    {
        LogDebug("Starting battle execution.");

        BuildSlotStates();

        if (_slotStates.Count == 0)
        {
            LogWarning("No populated slots detected. Battle execution aborted.");
            return;
        }

        LogDebug($"Resolved {_slotStates.Count} populated slot(s) for battle.");

        for (int i = 0; i < _slotStates.Count; i++)
        {
            SlotBattleState source = _slotStates[i];
            if (!source.HasCardData)
            {
                LogDebug($"Skipping empty source slot at index {source.Index}.");
                continue;
            }

            LogDebug($"Processing source {source.GetDebugLabel()} with score {source.GetScoreValue()} and contributions {source.GetContributionSummary()}.");

            for (int j = i + 1; j < _slotStates.Count; j++)
            {
                SlotBattleState target = _slotStates[j];
                if (!target.HasCardData)
                {
                    LogDebug($"Skipping empty target slot at index {target.Index}.");
                    continue;
                }

                string relation = ResolveRelationship(source.Definition, target.Definition);
                if (string.IsNullOrWhiteSpace(relation))
                {
                    LogDebug($"No relationship found between {source.GetDebugLabel()} and {target.GetDebugLabel()}. Skipping contribution.");
                    continue;
                }

                if (!TryGetRelationStat(relation, out CharacterStats.StatType statType))
                {
                    LogWarning($"Relationship '{relation}' between {source.GetDebugLabel()} and {target.GetDebugLabel()} does not map to a stat. Skipping contribution.");
                    continue;
                }

                int scoreValue = source.GetScoreValue();
                if (scoreValue != 0)
                {
                    LogDebug($"{source.GetDebugLabel()} contributes {scoreValue} {statType} to {target.GetDebugLabel()} via relation '{relation}'.");
                    target.AddContribution(statType, scoreValue, source.GetDebugLabel());
                }
                else
                {
                    LogDebug($"{source.GetDebugLabel()} has zero score; no direct contribution to {target.GetDebugLabel()} for relation '{relation}'.");
                }

                target.AbsorbContributions(source);
            }
        }

        for (int i = 0; i < _slotStates.Count; i++)
        {
            SlotBattleState state = _slotStates[i];
            LogDebug($"Final contribution summary for {state.GetDebugLabel()}: {state.GetContributionSummary()}.");
        }

        for (int i = 0; i < _slotStates.Count; i++)
        {
            _slotStates[i].ApplyFinalStats();
        }

        LogDebug("Battle execution completed.");
    }

    public void OnBattleButtonPressed()
    {
        LogDebug("Battle button pressed; executing battle resolution.");
        ExecuteBattle();
    }

    private void BuildSlotStates()
    {
        _slotStates.Clear();

        CardSlot[] slots = GetComponentsInChildren<CardSlot>(true);
        if (slots == null || slots.Length == 0)
        {
            LogWarning("No CardSlot components found under BattleResolver. Ensure slots are parented correctly.");
            return;
        }

        LogDebug($"Found {slots.Length} CardSlot component(s) to inspect.");

        for (int i = 0; i < slots.Length; i++)
        {
            CardSlot slot = slots[i];
            if (slot == null)
            {
                LogWarning($"Encountered null CardSlot reference at index {i}. Skipping.");
                continue;
            }

            if (!TryParseSlotIndex(slot.gameObject.name, out int slotIndex))
            {
                LogWarning($"Unable to parse slot index from '{slot.gameObject.name}'. Expected format 'Slot <number>'. Skipping.");
                continue;
            }

            CardView view = GetActiveCardView(slot);
            if (view == null)
            {
                LogWarning($"Slot {slotIndex} has no active CardView. Skipping.");
                continue;
            }

            var state = new SlotBattleState(this, slotIndex, slot, view);
            state.Initialize();
            if (state.HasCardData)
            {
                _slotStates.Add(state);
                LogDebug($"Registered {state.GetDebugLabel()} with base stats {FormatStats(state.BaseStats)}.");
            }
            else
            {
                LogWarning($"Slot {slotIndex} did not provide valid card data after initialization.");
            }
        }

        if (_slotStates.Count > 1)
        {
            _slotStates.Sort((a, b) => a.Index.CompareTo(b.Index));
            LogDebug("Sorted slot states by ascending index.");
        }

        if (_slotStates.Count > 0)
        {
            var labels = new StringBuilder();
            for (int i = 0; i < _slotStates.Count; i++)
            {
                if (i > 0)
                {
                    labels.Append(", ");
                }

                labels.Append(_slotStates[i].GetDebugLabel());
            }

            LogDebug($"Prepared slot order: {labels}.");
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

    private void LogDebug(string message, UnityEngine.Object context = null)
    {
        if (!enableDebugLogging)
        {
            return;
        }

        Debug.Log($"{LogPrefix} {message}", context == null ? this : context);
    }

    private void LogWarning(string message, UnityEngine.Object context = null)
    {
        if (!enableDebugLogging)
        {
            return;
        }

        Debug.LogWarning($"{LogPrefix} {message}", context == null ? this : context);
    }

    private string FormatStats(CharacterStats stats)
    {
        if (stats == null)
        {
            return "<null>";
        }

        return $"ATK:{stats.attack} HP:{stats.health} ARM:{stats.armor} EXTRA:{stats.extraAttack} AOE:{stats.areaDamage} REG:{stats.regeneration} LUCK:{stats.luck} SCORE:{stats.score}";
    }

    private sealed class SlotBattleState
    {
        private readonly BattleResolver _owner;
        private readonly Dictionary<CharacterStats.StatType, int> _contributions = new Dictionary<CharacterStats.StatType, int>();

        public SlotBattleState(BattleResolver owner, int index, CardSlot slot, CardView view)
        {
            _owner = owner;
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
                _owner?.LogWarning($"{GetDebugLabel()} has no CardView assigned during initialization.", Slot);
                Definition = null;
                BaseStats = null;
                FinalStats = null;
                return;
            }

            Definition = View.Definition;
            View.ResetToBaseStats();
            BaseStats = View.GetBaseStatsClone();
            FinalStats = BaseStats != null ? new CharacterStats(BaseStats) : new CharacterStats();

            _owner?.LogDebug($"{GetDebugLabel()} initialization complete. Base stats {(_owner != null ? _owner.FormatStats(BaseStats) : "<unknown>")}.", View);
        }

        public int GetScoreValue()
        {
            return BaseStats != null ? BaseStats.score : 0;
        }

        public void AddContribution(CharacterStats.StatType statType, int amount, string sourceLabel = null)
        {
            if (amount == 0)
            {
                _owner?.LogDebug($"{GetDebugLabel()} received zero contribution for {statType} from {sourceLabel ?? "<unknown>"}; ignoring.", View);
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

            int total = FinalStats != null ? FinalStats.GetValue(statType) : amount;
            _owner?.LogDebug($"{GetDebugLabel()} updated {statType} by +{amount} (source: {sourceLabel ?? "<unknown>"}). New total: {total}.", View);
        }

        public void AbsorbContributions(SlotBattleState source)
        {
            if (source == null || source._contributions.Count == 0)
            {
                if (source != null)
                {
                    _owner?.LogDebug($"{GetDebugLabel()} found no stored contributions to absorb from {source.GetDebugLabel()}.", View);
                }
                return;
            }

            _owner?.LogDebug($"{GetDebugLabel()} absorbing contributions from {source.GetDebugLabel()}: {source.GetContributionSummary()}.", View);

            foreach (KeyValuePair<CharacterStats.StatType, int> pair in source._contributions)
            {
                AddContribution(pair.Key, pair.Value, source.GetDebugLabel());
            }
        }

        public void ApplyFinalStats()
        {
            if (View == null)
            {
                _owner?.LogWarning($"{GetDebugLabel()} has no CardView when applying final stats.", Slot);
                return;
            }

            View.ApplyStats(FinalStats);
            _owner?.LogDebug($"{GetDebugLabel()} final stats applied: {(_owner != null ? _owner.FormatStats(FinalStats) : "<unknown>")}.", View);
        }

        public string GetDebugLabel()
        {
            string name = Definition != null ? (!string.IsNullOrWhiteSpace(Definition.name) ? Definition.name : Definition.id) : "<empty>";
            return $"Slot {Index} ({name ?? "<null>"})";
        }

        public string GetContributionSummary()
        {
            if (_contributions.Count == 0)
            {
                return "<none>";
            }

            StringBuilder builder = new StringBuilder();
            int i = 0;
            foreach (KeyValuePair<CharacterStats.StatType, int> pair in _contributions)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(pair.Key);
                builder.Append(": ");
                builder.Append(pair.Value);
                i++;
            }

            return builder.ToString();
        }
    }
}

