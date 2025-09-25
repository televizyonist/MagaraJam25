using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
[DisallowMultipleComponent]
public class MonsterView : MonoBehaviour
{
    [SerializeField] private string monsterId = "Monster_01";
    [SerializeField] private string monstersResourcePath = "Data/monsters";
    [SerializeField] private string spriteResourceFolder = string.Empty;
    [SerializeField] private bool randomizeMonsterOnPlay;
    [SerializeField] private float randomizeIntervalSeconds = 1f;
    [SerializeField] private string[] randomMonsterIds =
    {
        "Monster_01",
        "Monster_02",
        "Monster_03",
        "Monster_04",
        "Monster_05"
    };
    [SerializeField] private Image portraitImage;
    [SerializeField] private TMP_Text healthValueText;
    [SerializeField] private TMP_Text attackValueText;

    private static readonly Dictionary<string, Dictionary<string, MonsterStats>> Cache = new Dictionary<string, Dictionary<string, MonsterStats>>(StringComparer.OrdinalIgnoreCase);

    public int BaseAttack => _baseAttack;
    public int BaseHealth => _baseHealth;
    public int CurrentAttack => _currentAttack;
    public int CurrentHealth => _currentHealth;
    public bool HasAttackValue => _hasAttackValue;
    public bool HasHealthValue => _hasHealthValue;

    private int _baseAttack;
    private int _baseHealth;
    private int _currentAttack;
    private int _currentHealth;
    private bool _hasAttackValue;
    private bool _hasHealthValue;

    private Coroutine _randomizationCoroutine;

    private void Awake()
    {
        EnsureReferences();
        if (Application.isPlaying)
        {
            TryRandomizeMonsterId();
        }
        ApplyMonsterData();
    }

    private void OnEnable()
    {
        if (Application.isPlaying)
        {
            StartRandomizationIfNeeded();
        }
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
        {
            StopRandomizationCoroutine();
        }
    }

    private void Reset()
    {
        EnsureReferences();
        EnsureRandomMonsterDefaults();
        EnsureRandomizationInterval();
        ApplyMonsterData();
    }

    private void OnValidate()
    {
        EnsureReferences();
        EnsureRandomMonsterDefaults();
        EnsureRandomizationInterval();

        ApplyMonsterData();

        if (Application.isPlaying && isActiveAndEnabled)
        {
            if (randomizeMonsterOnPlay)
            {
                StartRandomizationIfNeeded();
            }
            else
            {
                StopRandomizationCoroutine();
            }
        }
    }

    private void EnsureReferences()
    {
        if (portraitImage == null)
        {
            Transform imageTransform = transform.Find("Canvas/Image");
            if (imageTransform != null)
            {
                portraitImage = imageTransform.GetComponent<Image>();
            }
        }

        if (attackValueText == null)
        {
            Transform attackTextTransform = transform.Find("Canvas/Attack/Text (TMP)");
            if (attackTextTransform != null)
            {
                attackValueText = attackTextTransform.GetComponent<TMP_Text>();
            }
        }

        if (healthValueText == null)
        {
            Transform healthTextTransform = transform.Find("Canvas/Health/Text (TMP)");
            if (healthTextTransform != null)
            {
                healthValueText = healthTextTransform.GetComponent<TMP_Text>();
            }
        }
    }

    private void ApplyMonsterData()
    {
        ResetRuntimeCache();

        if (string.IsNullOrWhiteSpace(monsterId))
        {
            RefreshStatTexts();
            return;
        }

        MonsterStats stats = GetMonsterStats(monsterId);
        if (stats == null)
        {
            RefreshStatTexts();
            return;
        }

        _hasAttackValue = stats.attackAssigned;
        _hasHealthValue = stats.healthAssigned;
        _baseAttack = stats.attack;
        _baseHealth = stats.health;
        _currentAttack = _baseAttack;
        _currentHealth = _baseHealth;

        RefreshStatTexts();

        UpdatePortrait(monsterId);
        gameObject.name = monsterId;
    }

    public void SetMonsterId(string id)
    {
        string normalizedId = id ?? string.Empty;
        if (string.Equals(monsterId, normalizedId, StringComparison.Ordinal))
        {
            ApplyMonsterData();
            return;
        }

        monsterId = normalizedId;
        ApplyMonsterData();
    }

    public void ResetCombatStats()
    {
        _currentAttack = _baseAttack;
        _currentHealth = _baseHealth;
        RefreshStatTexts();
    }

    public void SetCurrentAttack(int value)
    {
        _currentAttack = Mathf.Max(0, value);
        RefreshStatTexts();
    }

    public void SetCurrentHealth(int value)
    {
        _currentHealth = Mathf.Max(0, value);
        RefreshStatTexts();
    }

    public void ApplyDamage(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        SetCurrentHealth(_currentHealth - amount);
    }

    private void UpdatePortrait(string id)
    {
        if (portraitImage == null || string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        string resourcePath = BuildSpriteResourcePath(id);
        Sprite sprite = Resources.Load<Sprite>(resourcePath);
        if (sprite == null && !string.Equals(resourcePath, id, StringComparison.Ordinal))
        {
            sprite = Resources.Load<Sprite>(id);
        }

        if (sprite != null)
        {
            portraitImage.sprite = sprite;
        }
        else
        {
            Debug.LogWarning($"Sprite for monster '{id}' could not be found at Resources/{resourcePath}.");
        }
    }

    private string BuildSpriteResourcePath(string id)
    {
        if (string.IsNullOrWhiteSpace(spriteResourceFolder))
        {
            return id;
        }

        if (spriteResourceFolder.EndsWith("/", StringComparison.Ordinal))
        {
            return spriteResourceFolder + id;
        }

        return spriteResourceFolder + "/" + id;
    }

    private void RefreshStatTexts()
    {
        SetText(attackValueText, _hasAttackValue ? _currentAttack.ToString() : string.Empty);
        SetText(healthValueText, _hasHealthValue ? _currentHealth.ToString() : string.Empty);
    }

    private void TryRandomizeMonsterId()
    {
        if (!randomizeMonsterOnPlay)
        {
            return;
        }

        EnsureRandomMonsterDefaults();
        EnsureRandomizationInterval();
        if (randomMonsterIds == null || randomMonsterIds.Length == 0)
        {
            Debug.LogWarning("Random monster list is empty; cannot randomize monster id.");
            return;
        }

        string selectedId = GetRandomMonsterId();
        if (string.IsNullOrWhiteSpace(selectedId))
        {
            Debug.LogWarning("Random monster selection resulted in an empty id; skipping randomization.");
            return;
        }

        selectedId = selectedId.Trim();
        if (!string.Equals(monsterId, selectedId, StringComparison.Ordinal))
        {
            monsterId = selectedId;
        }
    }

    private void EnsureRandomMonsterDefaults()
    {
        if (randomMonsterIds == null || randomMonsterIds.Length == 0)
        {
            randomMonsterIds = new[] { "Monster_01", "Monster_02", "Monster_03", "Monster_04", "Monster_05" };
        }
    }

    private void EnsureRandomizationInterval()
    {
        if (randomizeIntervalSeconds <= 0f)
        {
            randomizeIntervalSeconds = 1f;
        }
    }

    private string GetRandomMonsterId()
    {
        if (randomMonsterIds == null || randomMonsterIds.Length == 0)
        {
            return null;
        }

        int index = UnityEngine.Random.Range(0, randomMonsterIds.Length);
        return randomMonsterIds[index];
    }

    private void StartRandomizationIfNeeded()
    {
        if (!randomizeMonsterOnPlay)
        {
            return;
        }

        EnsureRandomMonsterDefaults();
        EnsureRandomizationInterval();

        if (randomMonsterIds == null || randomMonsterIds.Length == 0)
        {
            Debug.LogWarning("Random monster list is empty; cannot start continuous randomization.");
            return;
        }

        if (_randomizationCoroutine != null)
        {
            StopCoroutine(_randomizationCoroutine);
        }

        _randomizationCoroutine = StartCoroutine(RandomizeMonsterContinuously());
    }

    private void StopRandomizationCoroutine()
    {
        if (_randomizationCoroutine != null)
        {
            StopCoroutine(_randomizationCoroutine);
            _randomizationCoroutine = null;
        }
    }

    private IEnumerator RandomizeMonsterContinuously()
    {
        while (true)
        {
            string selectedId = GetRandomMonsterId();
            if (!string.IsNullOrWhiteSpace(selectedId))
            {
                SetMonsterId(selectedId.Trim());
            }

            EnsureRandomizationInterval();
            yield return new WaitForSeconds(randomizeIntervalSeconds);
        }
    }

    private void ResetRuntimeCache()
    {
        _baseAttack = 0;
        _baseHealth = 0;
        _currentAttack = 0;
        _currentHealth = 0;
        _hasAttackValue = false;
        _hasHealthValue = false;
    }

    private static void SetText(TMP_Text textComponent, string value)
    {
        if (textComponent != null)
        {
            textComponent.text = value ?? string.Empty;
        }
    }

    private MonsterStats GetMonsterStats(string id)
    {
        if (string.IsNullOrWhiteSpace(monstersResourcePath))
        {
            Debug.LogWarning("Monsters resource path is empty.");
            return null;
        }

        if (!Cache.TryGetValue(monstersResourcePath, out Dictionary<string, MonsterStats> lookup) || lookup == null)
        {
            lookup = LoadMonsterStats(monstersResourcePath);
            Cache[monstersResourcePath] = lookup;
        }

        if (lookup != null && lookup.TryGetValue(id, out MonsterStats stats))
        {
            return stats;
        }

        Debug.LogWarning($"Monster stats for '{id}' could not be found in {monstersResourcePath}.");
        return null;
    }

    private static Dictionary<string, MonsterStats> LoadMonsterStats(string resourcePath)
    {
        var result = new Dictionary<string, MonsterStats>(StringComparer.OrdinalIgnoreCase);

        TextAsset monstersAsset = Resources.Load<TextAsset>(resourcePath);
        if (monstersAsset == null)
        {
            Debug.LogWarning($"Monster data could not be loaded from Resources/{resourcePath}.");
            return result;
        }

        foreach (Match monsterMatch in MonsterRegex.Matches(monstersAsset.text))
        {
            if (!monsterMatch.Success)
            {
                continue;
            }

            string id = monsterMatch.Groups["id"].Value;
            string body = monsterMatch.Groups["body"].Value;

            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(body))
            {
                continue;
            }

            MonsterStats stats = ParseMonsterStats(body);
            if (stats != null && stats.HasValues)
            {
                result[id] = stats;
            }
        }

        return result;
    }

    private static MonsterStats ParseMonsterStats(string body)
    {
        if (string.IsNullOrEmpty(body))
        {
            return null;
        }

        var stats = new MonsterStats();

        foreach (Match fieldMatch in FieldRegex.Matches(body))
        {
            if (!fieldMatch.Success)
            {
                continue;
            }

            string key = fieldMatch.Groups["key"].Value;
            string valueText = fieldMatch.Groups["value"].Value;

            if (!int.TryParse(valueText, out int value))
            {
                continue;
            }

            stats.AssignValue(key, value);
        }

        return stats;
    }

    [Serializable]
    private class MonsterStats
    {
        public int attack;
        public int health;
        public int value;

        public bool attackAssigned;
        public bool healthAssigned;
        public bool valueAssigned;

        public bool HasValues => attackAssigned || healthAssigned || valueAssigned;

        public void AssignValue(string key, int amount)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            string normalized = NormalizeKey(key);
            switch (normalized)
            {
                case "can":
                case "health":
                case "hp":
                    health = amount;
                    healthAssigned = true;
                    break;
                case "saldiri":
                case "atak":
                case "hasar":
                case "attack":
                case "damage":
                    attack = amount;
                    attackAssigned = true;
                    break;
                case "deger":
                case "value":
                    value = amount;
                    valueAssigned = true;
                    break;
            }
        }
    }

    private static string NormalizeKey(string value)
    {
        string normalized = value.Trim().ToLowerInvariant();
        normalized = normalized.Replace('ı', 'i');
        normalized = normalized.Replace('ğ', 'g');
        normalized = normalized.Replace('ş', 's');
        normalized = normalized.Replace('ç', 'c');
        normalized = normalized.Replace('ö', 'o');
        normalized = normalized.Replace('ü', 'u');
        return normalized;
    }

    private static readonly Regex MonsterRegex = new Regex("\"(?<id>[^\"]+)\"\\s*:\\s*\\{(?<body>[^}]*)\\}", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex FieldRegex = new Regex("\"(?<key>[^\"]+)\"\\s*:\\s*(?<value>-?\\d+)", RegexOptions.Compiled | RegexOptions.Multiline);
}
