using System;
using System.Collections;
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
        { "kardes", CharacterStats.StatType.AreaDamage },
        { "anne", CharacterStats.StatType.Armor },
        { "arkadas", CharacterStats.StatType.Luck },
        { "es", CharacterStats.StatType.Regeneration },
        { "akraba", CharacterStats.StatType.Health }
    };

    [SerializeField] private bool enableDebugLogging = true;
    [SerializeField] private List<CardSlot> slotOverrides = new List<CardSlot>();
    [Header("Battle UI")]
    [SerializeField] private GameObject battleButtonRoot;
    [SerializeField] private GameObject handAreaRoot;

    [Header("Battle Presentation")]
    [SerializeField] private SlotRelationshipDisplay relationshipDisplayOverride;

    [Header("Camera Sequence")]
    [SerializeField] private Transform cameraTransformOverride;
    [SerializeField] private Transform secondCameraPosition;
    [SerializeField] private float secondCameraMoveSpeed = 5f;
    [SerializeField] private float delayBeforeThirdCameraMove = 0.5f;
    [SerializeField] private Transform thirdCameraPosition;
    [SerializeField] private float thirdCameraMoveSpeed = 5f;

    [Header("Combat")]
    [SerializeField] private float combatStartDelay = 5f;
    [SerializeField] private float attackStepDelay = 0.5f;

    private Coroutine _cameraSequenceRoutine;
    private Coroutine _combatRoutine;

    private const string LogPrefix = "[BattleResolver]";
    private const int FocusSlotMinimumIndex = 4;

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
        HideBattleButton();
        HideHandArea();
        StartCameraSequence();
        ExecuteBattle();
        StartCombatCountdown();
    }

    private void StartCombatCountdown()
    {
        if (_combatRoutine != null)
        {
            StopCoroutine(_combatRoutine);
        }

        LogDebug($"Combat countdown started. Combat will begin after {combatStartDelay:F2} second(s).");
        _combatRoutine = StartCoroutine(RunCombatAfterDelay());
    }

    private IEnumerator RunCombatAfterDelay()
    {
        if (combatStartDelay > 0f)
        {
            yield return new WaitForSeconds(combatStartDelay);
        }

        yield return MonsterWar.SelfMonsterWar(this);
        _combatRoutine = null;
    }

    internal IEnumerator RunSelfMonsterWar()
    {
        return RunCombatLoop();
    }

    private IEnumerator RunCombatLoop()
    {
        List<CharacterCombatant> characters = BuildCharacterCombatants();
        List<MonsterCombatant> monsters = BuildMonsterCombatants();

        if (characters.Count == 0)
        {
            LogWarning("Combat aborted because no character combatants were found.");
            yield break;
        }

        if (monsters.Count == 0)
        {
            LogWarning("Combat aborted because no monster combatants were found in the scene.");
            yield break;
        }

        LogDebug($"Combat starting with {characters.Count} character(s) and {monsters.Count} monster(s).");

        int roundIndex = 1;
        while (HasLivingCharacters(characters) && HasLivingMonsters(monsters))
        {
            LogDebug($"-- Combat Round {roundIndex} --");

            yield return ExecuteCharacterAttackPhase(roundIndex, characters, monsters);
            if (!HasLivingMonsters(monsters))
            {
                LogDebug($"Combat ended during round {roundIndex}; all monsters defeated.");
                break;
            }

            yield return ExecuteMonsterAttackPhase(roundIndex, monsters, characters);
            if (!HasLivingCharacters(characters))
            {
                LogDebug($"Combat ended during round {roundIndex}; all characters defeated.");
                break;
            }

            roundIndex++;
        }

        string winner = HasLivingCharacters(characters) ? "characters" : "monsters";
        LogDebug($"Combat finished after {roundIndex} round(s). Surviving team: {winner}.");
    }

    private IEnumerator ExecuteCharacterAttackPhase(int roundIndex, List<CharacterCombatant> characters, List<MonsterCombatant> monsters)
    {
        if (characters == null || monsters == null)
        {
            yield break;
        }

        for (int i = 0; i < characters.Count; i++)
        {
            CharacterCombatant attacker = characters[i];
            if (attacker == null || !attacker.IsAlive)
            {
                continue;
            }

            MonsterCombatant target = GetFirstLivingMonster(monsters);
            if (target == null)
            {
                LogDebug($"Round {roundIndex}: No living monster found for {attacker.Label} to attack.");
                break;
            }

            int damage = Mathf.Max(0, attacker.Attack);
            LogDebug($"Round {roundIndex}: {attacker.Label} attacks {target.Label} for {damage} damage.");

            if (damage > 0)
            {
                target.ApplyDamage(damage);
                if (!target.IsAlive)
                {
                    LogDebug($"Round {roundIndex}: {target.Label} has been defeated.");
                }
            }
            else
            {
                LogDebug($"Round {roundIndex}: {attacker.Label} has no attack value and deals no damage.");
            }

            if (attackStepDelay > 0f)
            {
                yield return new WaitForSeconds(attackStepDelay);
            }
            else
            {
                yield return null;
            }

            if (!HasLivingMonsters(monsters))
            {
                break;
            }
        }
    }

    private IEnumerator ExecuteMonsterAttackPhase(int roundIndex, List<MonsterCombatant> monsters, List<CharacterCombatant> characters)
    {
        if (monsters == null || characters == null)
        {
            yield break;
        }

        for (int i = 0; i < monsters.Count; i++)
        {
            MonsterCombatant attacker = monsters[i];
            if (attacker == null || !attacker.IsAlive)
            {
                continue;
            }

            CharacterCombatant target = GetFirstLivingCharacter(characters);
            if (target == null)
            {
                LogDebug($"Round {roundIndex}: No living character found for {attacker.Label} to attack.");
                break;
            }

            int damage = Mathf.Max(0, attacker.Attack);
            LogDebug($"Round {roundIndex}: {attacker.Label} attacks {target.Label} for {damage} damage.");

            if (damage > 0)
            {
                target.ApplyDamage(damage);
                if (!target.IsAlive)
                {
                    LogDebug($"Round {roundIndex}: {target.Label} has been defeated.");
                }
            }
            else
            {
                LogDebug($"Round {roundIndex}: {attacker.Label} has no attack value and deals no damage.");
            }

            if (attackStepDelay > 0f)
            {
                yield return new WaitForSeconds(attackStepDelay);
            }
            else
            {
                yield return null;
            }

            if (!HasLivingCharacters(characters))
            {
                break;
            }
        }
    }

    private static bool HasLivingCharacters(List<CharacterCombatant> characters)
    {
        if (characters == null)
        {
            return false;
        }

        for (int i = 0; i < characters.Count; i++)
        {
            if (characters[i] != null && characters[i].IsAlive)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasLivingMonsters(List<MonsterCombatant> monsters)
    {
        if (monsters == null)
        {
            return false;
        }

        for (int i = 0; i < monsters.Count; i++)
        {
            if (monsters[i] != null && monsters[i].IsAlive)
            {
                return true;
            }
        }

        return false;
    }

    private static CharacterCombatant GetFirstLivingCharacter(List<CharacterCombatant> characters)
    {
        if (characters == null)
        {
            return null;
        }

        for (int i = 0; i < characters.Count; i++)
        {
            CharacterCombatant combatant = characters[i];
            if (combatant != null && combatant.IsAlive)
            {
                return combatant;
            }
        }

        return null;
    }

    private static MonsterCombatant GetFirstLivingMonster(List<MonsterCombatant> monsters)
    {
        if (monsters == null)
        {
            return null;
        }

        for (int i = 0; i < monsters.Count; i++)
        {
            MonsterCombatant combatant = monsters[i];
            if (combatant != null && combatant.IsAlive)
            {
                return combatant;
            }
        }

        return null;
    }

    private List<CharacterCombatant> BuildCharacterCombatants()
    {
        var combatants = new List<CharacterCombatant>();

        if (_slotStates.Count == 0)
        {
            LogWarning("Combat requested but no populated character slots were detected.");
            return combatants;
        }

        for (int i = 0; i < _slotStates.Count; i++)
        {
            SlotBattleState state = _slotStates[i];
            if (state == null || !state.HasCardData)
            {
                continue;
            }

            CharacterStats sourceStats = state.FinalStats ?? state.BaseStats;
            if (sourceStats == null && state.View != null && state.View.Definition != null)
            {
                sourceStats = state.View.Definition.stats;
            }

            CharacterCombatant combatant = new CharacterCombatant(this, state.View, sourceStats, state.GetDebugLabel());
            combatants.Add(combatant);
            LogDebug($"Registered combatant {combatant.Label}: ATK {combatant.Attack} | HP {combatant.CurrentHealth} | ARM {combatant.CurrentArmor}.");
        }

        return combatants;
    }

    private List<MonsterCombatant> BuildMonsterCombatants()
    {
        var combatants = new List<MonsterCombatant>();

        MonsterView[] monsterViews = FindObjectsOfType<MonsterView>(true);
        if (monsterViews == null || monsterViews.Length == 0)
        {
            LogWarning("No MonsterView instances were found when building monster combatants.");
            return combatants;
        }

        for (int i = 0; i < monsterViews.Length; i++)
        {
            MonsterView view = monsterViews[i];
            if (view == null || !view.gameObject.activeInHierarchy)
            {
                continue;
            }

            MonsterCombatant combatant = new MonsterCombatant(this, view);
            combatants.Add(combatant);
            LogDebug($"Registered monster {combatant.Label}: ATK {combatant.Attack} | HP {combatant.CurrentHealth}.");
        }

        return combatants;
    }

    private void HideBattleButton()
    {
        if (battleButtonRoot == null)
        {
            return;
        }

        if (battleButtonRoot.activeSelf)
        {
            battleButtonRoot.SetActive(false);
        }
    }

    private void HideHandArea()
    {
        if (handAreaRoot == null)
        {
            return;
        }

        if (handAreaRoot.activeSelf)
        {
            handAreaRoot.SetActive(false);
        }
    }

    private void StartCameraSequence()
    {
        if (secondCameraPosition == null && thirdCameraPosition == null)
        {
            return;
        }

        if (_cameraSequenceRoutine != null)
        {
            StopCoroutine(_cameraSequenceRoutine);
        }

        _cameraSequenceRoutine = StartCoroutine(RunCameraSequence());
    }

    private IEnumerator RunCameraSequence()
    {
        Transform cameraTransform = ResolveCameraTransform();
        if (cameraTransform == null)
        {
            LogWarning("Battle camera sequence requested but no camera transform is available. Assign a transform in the inspector or ensure a main camera exists in the scene.");
            yield break;
        }

        if (secondCameraPosition != null)
        {
            yield return MoveTransform(cameraTransform, secondCameraPosition.position, secondCameraMoveSpeed);
        }

        if (thirdCameraPosition != null)
        {
            if (delayBeforeThirdCameraMove > 0f)
            {
                yield return new WaitForSeconds(delayBeforeThirdCameraMove);
            }

            ApplyThirdCameraFocus();

            yield return MoveTransform(cameraTransform, thirdCameraPosition.position, thirdCameraMoveSpeed);
        }
    }

    private static IEnumerator MoveTransform(Transform target, Vector3 destination, float speed)
    {
        if (target == null)
        {
            yield break;
        }

        if (speed <= 0f)
        {
            target.position = destination;
            yield break;
        }

        while ((target.position - destination).sqrMagnitude > 0.0001f)
        {
            target.position = Vector3.MoveTowards(target.position, destination, speed * Time.deltaTime);
            yield return null;
        }

        target.position = destination;
    }

    private Transform ResolveCameraTransform()
    {
        if (cameraTransformOverride != null)
        {
            return cameraTransformOverride;
        }

        Camera mainCamera = Camera.main;
        return mainCamera != null ? mainCamera.transform : null;
    }

    private List<CardSlot> ResolveCardSlots()
    {
        List<CardSlot> resolvedSlots = new List<CardSlot>();
        HashSet<CardSlot> uniqueSlots = new HashSet<CardSlot>();

        void TryAddSlot(CardSlot candidate, string context = null, int index = -1)
        {
            if (candidate == null)
            {
                if (!string.IsNullOrEmpty(context))
                {
                    string label = index >= 0 ? $"{context} at index {index}" : context;
                    LogWarning($"{label} is null. Skipping.");
                }

                return;
            }

            if (!uniqueSlots.Add(candidate))
            {
                if (!string.IsNullOrEmpty(context))
                {
                    string label = index >= 0 ? $"{context} at index {index}" : context;
                    LogDebug($"{label} produced duplicate CardSlot reference '{candidate.name}'. Skipping duplicate.");
                }

                return;
            }

            resolvedSlots.Add(candidate);
        }

        CardSlot[] slots = GetComponentsInChildren<CardSlot>(true);
        if (slots != null && slots.Length > 0)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                TryAddSlot(slots[i]);
            }

            if (resolvedSlots.Count > 0)
            {
                LogDebug($"Found {resolvedSlots.Count} CardSlot component(s) via hierarchy search.");
            }
        }
        else if (slotOverrides != null && slotOverrides.Count > 0)
        {
            for (int i = 0; i < slotOverrides.Count; i++)
            {
                TryAddSlot(slotOverrides[i], "Slot override", i);
            }

            if (resolvedSlots.Count > 0)
            {
                LogDebug($"Using {resolvedSlots.Count} CardSlot override reference(s).");
            }
        }
        else
        {
            CardSlot[] discoveredSlots = FindObjectsOfType<CardSlot>(true);
            if (discoveredSlots != null && discoveredSlots.Length > 0)
            {
                for (int i = 0; i < discoveredSlots.Length; i++)
                {
                    TryAddSlot(discoveredSlots[i]);
                }

                if (resolvedSlots.Count > 0)
                {
                    LogDebug($"Found {resolvedSlots.Count} CardSlot component(s) via global search fallback.");
                }
            }
        }

        return resolvedSlots;
    }

    private void BuildSlotStates()
    {
        _slotStates.Clear();

        List<CardSlot> resolvedSlots = ResolveCardSlots();
        if (resolvedSlots.Count == 0)
        {
            LogWarning("No CardSlot components found under BattleResolver and no slot overrides provided. Ensure slots are parented correctly or assign overrides.");
            return;
        }

        for (int i = 0; i < resolvedSlots.Count; i++)
        {
            CardSlot slot = resolvedSlots[i];
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

    private void ApplyThirdCameraFocus()
    {
        if (_hasAppliedThirdCameraFocus)
        {
            return;
        }

        _hasAppliedThirdCameraFocus = true;

        HideRelationshipDisplays();
        HideSlotDecorationsAndCards();
    }

    private void HideRelationshipDisplays()
    {
        SlotRelationshipDisplay[] displays = null;
        if (relationshipDisplayOverride != null)
        {
            displays = new[] { relationshipDisplayOverride };
        }
        else
        {
            displays = GetComponentsInChildren<SlotRelationshipDisplay>(true);
            if ((displays == null || displays.Length == 0))
            {
                SlotRelationshipDisplay fallback = FindObjectOfType<SlotRelationshipDisplay>(true);
                if (fallback != null)
                {
                    displays = new[] { fallback };
                }
            }
        }

        if (displays == null)
        {
            return;
        }

        for (int i = 0; i < displays.Length; i++)
        {
            SlotRelationshipDisplay display = displays[i];
            if (display == null)
            {
                continue;
            }

            display.SetForceHidden(true);
        }
    }

    private void HideSlotDecorationsAndCards()
    {
        List<CardSlot> slots = ResolveCardSlots();
        if (slots.Count == 0)
        {
            return;
        }

        for (int i = 0; i < slots.Count; i++)
        {
            CardSlot slot = slots[i];
            if (slot == null)
            {
                continue;
            }

            Transform slotTransform = slot.transform;
            SetImmediateChildActive(slotTransform, "Glow", false);
            SetImmediateChildActive(slotTransform, "Border", false);

            if (!TryParseSlotIndex(slot.gameObject.name, out int slotIndex))
            {
                continue;
            }

            if (slotIndex >= FocusSlotMinimumIndex)
            {
                continue;
            }

            HideCardsUnderSlot(slot);
        }
    }

    private void HideCardsUnderSlot(CardSlot slot)
    {
        if (slot == null)
        {
            return;
        }

        Transform parent = slot.GetCardParent();
        if (parent == null)
        {
            return;
        }

        CardDragHandler[] handlers = parent.GetComponentsInChildren<CardDragHandler>(true);
        if (handlers == null || handlers.Length == 0)
        {
            return;
        }

        for (int i = 0; i < handlers.Length; i++)
        {
            CardDragHandler handler = handlers[i];
            if (handler == null)
            {
                continue;
            }

            GameObject cardObject = handler.gameObject;
            if (cardObject != null && cardObject.activeSelf)
            {
                cardObject.SetActive(false);
            }
        }
    }

    private static void SetImmediateChildActive(Transform parent, string childName, bool isActive)
    {
        if (parent == null || string.IsNullOrEmpty(childName))
        {
            return;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child == null)
            {
                continue;
            }

            if (!string.Equals(child.name, childName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            GameObject childObject = child.gameObject;
            if (childObject != null && childObject.activeSelf != isActive)
            {
                childObject.SetActive(isActive);
            }
        }
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

    private sealed class CharacterCombatant
    {
        private readonly BattleResolver _owner;
        private readonly string _label;

        public CharacterCombatant(BattleResolver owner, CardView view, CharacterStats stats, string label)
        {
            _owner = owner;
            View = view;
            Stats = stats != null ? new CharacterStats(stats) : new CharacterStats();
            _label = !string.IsNullOrWhiteSpace(label) ? label : ResolveName();
            SyncView();
        }

        public CardView View { get; }
        public CharacterStats Stats { get; }

        public string Label => !string.IsNullOrWhiteSpace(_label) ? _label : ResolveName();

        public int Attack => Stats?.attack ?? 0;
        public int CurrentHealth => Stats?.health ?? 0;
        public int CurrentArmor => Stats?.armor ?? 0;
        public bool IsAlive => Stats != null && Stats.health > 0;

        public void ApplyDamage(int damage)
        {
            if (Stats == null || damage <= 0 || !IsAlive)
            {
                return;
            }

            int remaining = damage;
            if (Stats.armor > 0)
            {
                int absorbed = Math.Min(Stats.armor, remaining);
                Stats.armor -= absorbed;
                remaining -= absorbed;
                _owner?.LogDebug($"{Label} absorbed {absorbed} damage with armor. Remaining armor: {Stats.armor}.");
            }

            if (remaining > 0)
            {
                int previousHealth = Stats.health;
                Stats.health = Mathf.Max(0, Stats.health - remaining);
                int applied = previousHealth - Stats.health;
                _owner?.LogDebug($"{Label} took {applied} damage to health. Remaining health: {Stats.health}.");
            }

            SyncView();
        }

        private void SyncView()
        {
            if (View != null)
            {
                View.ApplyStats(Stats);
            }
        }

        private string ResolveName()
        {
            if (View != null && View.Definition != null)
            {
                if (!string.IsNullOrWhiteSpace(View.Definition.name))
                {
                    return View.Definition.name;
                }

                if (!string.IsNullOrWhiteSpace(View.Definition.id))
                {
                    return View.Definition.id;
                }
            }

            return View != null ? View.gameObject.name : "<character>";
        }
    }

    private sealed class MonsterCombatant
    {
        private readonly BattleResolver _owner;
        private readonly string _label;

        public MonsterCombatant(BattleResolver owner, MonsterView view)
        {
            _owner = owner;
            View = view;
            if (View != null)
            {
                View.ResetCombatStats();
            }

            _label = ResolveName();
        }

        public MonsterView View { get; }

        public string Label => _label;

        public int Attack => View != null ? View.CurrentAttack : 0;
        public int CurrentHealth => View != null ? View.CurrentHealth : 0;
        public bool IsAlive => View != null && (!View.HasHealthValue || View.CurrentHealth > 0);

        public void ApplyDamage(int damage)
        {
            if (View == null || damage <= 0 || !IsAlive)
            {
                return;
            }

            int previousHealth = View.CurrentHealth;
            View.ApplyDamage(damage);
            int applied = previousHealth - View.CurrentHealth;
            _owner?.LogDebug($"{Label} took {applied} damage. Remaining health: {View.CurrentHealth}.");
        }

        private string ResolveName()
        {
            if (View != null && View.gameObject != null && !string.IsNullOrWhiteSpace(View.gameObject.name))
            {
                return View.gameObject.name;
            }

            return "<monster>";
        }
    }
}

