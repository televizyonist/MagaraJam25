using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class CardView : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text familyText;
    [SerializeField] private TMP_Text attackText;
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private TMP_Text armorText;
    [SerializeField] private TMP_Text extraAttackText;
    [SerializeField] private TMP_Text aoeText;
    [SerializeField] private TMP_Text regenerationText;
    [SerializeField] private TMP_Text luckText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private Image portraitImage;
    [SerializeField] private string characterSpriteResourceFolder = "Characters";

    private RectTransform _rectTransform;
    private CharacterCardDefinition _definition;
    private CharacterStats _baseStats;
    private bool _baseStatsCached;

    public CharacterCardDefinition Definition => _definition;

    public RectTransform RectTransform
    {
        get
        {
            if (_rectTransform == null)
            {
                _rectTransform = GetComponent<RectTransform>();
            }

            return _rectTransform;
        }
    }

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        EnsureTextReferences();
        EnsureImageReference();
        CacheBaseStats();
        if (_definition != null)
        {
            ApplyDefinition();
        }
        else
        {
            UpdatePortraitSprite();
        }

        UpdateStatsText();
    }

    private void Reset()
    {
        _rectTransform = GetComponent<RectTransform>();
        EnsureTextReferences();
        EnsureImageReference();
        _baseStats = null;
        _baseStatsCached = false;
        UpdatePortraitSprite();
        UpdateStatsText();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            _rectTransform = GetComponent<RectTransform>();
            EnsureTextReferences();
            EnsureImageReference();
            if (_definition != null)
            {
                ApplyDefinition();
            }
            else
            {
                UpdatePortraitSprite();
            }

            UpdateStatsText();
        }
    }

    public void SetData(CharacterCardDefinition definition)
    {
        _definition = definition;
        _baseStats = null;
        _baseStatsCached = false;
        CacheBaseStats();
        ApplyDefinition();

        if (_definition != null && !string.IsNullOrEmpty(_definition.id))
        {
            gameObject.name = _definition.id;
        }
    }

    private void ApplyDefinition()
    {
        EnsureTextReferences();
        EnsureImageReference();
        CacheBaseStats();

        if (nameText != null)
        {
            nameText.text = _definition != null ? _definition.name : string.Empty;
        }

        if (familyText != null)
        {
            familyText.text = _definition != null ? _definition.GetFamilyDisplayName() : string.Empty;
        }

        UpdateStatsText();
        UpdatePortraitSprite();
    }

    private void EnsureTextReferences()
    {
        if (nameText == null)
        {
            nameText = FindTextUnder("Canvas/Name");
        }

        if (familyText == null)
        {
            familyText = FindTextUnder("Canvas/Family");
        }

        if (attackText == null)
        {
            attackText = FindTextUnder("Canvas/Attack");
        }

        if (healthText == null)
        {
            healthText = FindTextUnder("Canvas/Health");
        }

        if (armorText == null)
        {
            armorText = FindTextUnder("Canvas/Armor");
        }

        if (extraAttackText == null)
        {
            extraAttackText = FindTextUnder("Canvas/Extra Attack");
        }

        if (aoeText == null)
        {
            aoeText = FindTextUnder("Canvas/AOE");
        }

        if (regenerationText == null)
        {
            regenerationText = FindTextUnder("Canvas/Regeneration");
        }

        if (luckText == null)
        {
            luckText = FindTextUnder("Canvas/Luck");
        }

        if (scoreText == null)
        {
            scoreText = FindTextUnder("Canvas/Score");
        }
    }

    private void EnsureImageReference()
    {
        if (portraitImage == null)
        {
            portraitImage = FindImageUnder("Image");

            if (portraitImage == null)
            {
                portraitImage = FindImageUnder("Canvas/Image");
            }

            if (portraitImage == null)
            {
                portraitImage = GetComponentInChildren<Image>(true);
            }

            if (portraitImage == null)
            {
                Debug.LogWarning($"{GetDebugContext()} Unable to automatically find portrait Image component. Please assign it in the inspector.", this);
            }
        }
    }

    private void UpdatePortraitSprite()
    {
        if (portraitImage == null)
        {
            Debug.LogWarning($"{GetDebugContext()} Portrait image reference is missing; cannot load sprite.", this);
            return;
        }

        string spriteName = ResolveSpriteName();
        if (string.IsNullOrWhiteSpace(spriteName))
        {
            Debug.LogWarning($"{GetDebugContext()} Unable to resolve portrait sprite name. Check Name text or definition values.", this);
        }
        else
        {
            Debug.Log($"{GetDebugContext()} Resolving portrait sprite for '{spriteName}'.", this);
        }
        Sprite sprite = LoadPortraitSprite(spriteName);

        portraitImage.sprite = sprite;
        portraitImage.enabled = sprite != null;

        if (sprite != null)
        {
            Debug.Log($"{GetDebugContext()} Applied portrait sprite '{sprite.name}'.", this);
        }
        else if (!string.IsNullOrWhiteSpace(spriteName))
        {
            Debug.LogWarning($"{GetDebugContext()} No sprite could be found for '{spriteName}'.", this);
        }
    }

    private Sprite LoadPortraitSprite(string spriteName)
    {
        if (string.IsNullOrWhiteSpace(spriteName))
        {
            Debug.LogWarning($"{GetDebugContext()} Sprite name is empty; skipping load.", this);
            return null;
        }

        spriteName = spriteName.Trim();

        string folder = string.IsNullOrWhiteSpace(characterSpriteResourceFolder)
            ? "Characters"
            : characterSpriteResourceFolder.Trim();

        Sprite sprite = null;

        string resourcePath = string.IsNullOrEmpty(folder)
            ? spriteName
            : $"{folder}/{spriteName}";

        Debug.Log($"{GetDebugContext()} Attempting to load sprite at '{resourcePath}'.", this);
        sprite = Resources.Load<Sprite>(resourcePath);

        if (sprite != null)
        {
            Debug.Log($"{GetDebugContext()} Successfully loaded sprite '{sprite.name}' at '{resourcePath}'.", this);
            return sprite;
        }

        Debug.LogWarning($"{GetDebugContext()} Could not find sprite at '{resourcePath}'. Initiating fallback search.", this);

        if (!string.IsNullOrEmpty(folder))
        {
            Sprite[] candidates = Resources.LoadAll<Sprite>(folder);
            int candidateCount = candidates != null ? candidates.Length : 0;
            Debug.Log($"{GetDebugContext()} Loaded {candidateCount} candidate sprites from folder '{folder}'.", this);

            if (candidates != null)
            {
                for (int i = 0; i < candidates.Length; i++)
                {
                    Sprite candidate = candidates[i];
                    if (candidate == null)
                    {
                        continue;
                    }

                    if (string.Equals(candidate.name, spriteName, StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.Log($"{GetDebugContext()} Fallback matched sprite '{candidate.name}' (case-insensitive).", this);
                        sprite = candidate;
                        break;
                    }
                }
            }
            else
            {
                Debug.LogWarning($"{GetDebugContext()} Fallback load returned no sprites from folder '{folder}'.", this);
            }
        }
        else
        {
            Debug.LogWarning($"{GetDebugContext()} Character sprite resource folder is empty; fallback search skipped.", this);
        }

        if (sprite == null)
        {
            Debug.LogWarning($"{GetDebugContext()} Fallback search did not find a sprite matching '{spriteName}'.", this);
        }

        return sprite;
    }

    private string ResolveSpriteName()
    {
        if (nameText != null && !string.IsNullOrEmpty(nameText.text))
        {
            return nameText.text.Trim();
        }

        if (_definition != null)
        {
            if (!string.IsNullOrEmpty(_definition.name))
            {
                return _definition.name.Trim();
            }

            if (!string.IsNullOrEmpty(_definition.id))
            {
                return _definition.id.Trim();
            }
        }

        if (!string.IsNullOrEmpty(gameObject.name))
        {
            return gameObject.name.Trim();
        }

        return null;
    }

    private TMP_Text FindTextUnder(string path)
    {
        Transform target = transform.Find(path);
        if (target == null)
        {
            return null;
        }

        return target.GetComponentInChildren<TMP_Text>(true);
    }

    private void UpdateStatsText()
    {
        EnsureTextReferences();

        CharacterStats stats = _definition != null ? _definition.stats : null;
        if (stats != null && stats.HasValues)
        {
            SetStatText(attackText, stats.attack);
            SetStatText(healthText, stats.health);
            SetStatText(armorText, stats.armor, hideWhenZero: true);
            SetStatText(extraAttackText, stats.extraAttack);
            SetStatText(aoeText, stats.areaDamage);
            SetStatText(regenerationText, stats.regeneration);
            SetStatText(luckText, stats.luck);
            SetStatText(scoreText, stats.score);
        }
        else
        {
            SetStatText(attackText, null);
            SetStatText(healthText, null);
            SetStatText(armorText, null, hideWhenZero: true);
            SetStatText(extraAttackText, null);
            SetStatText(aoeText, null);
            SetStatText(regenerationText, null);
            SetStatText(luckText, null);
            SetStatText(scoreText, null);
        }
    }

    private void SetStatText(TMP_Text text, int? value, bool hideWhenZero = false)
    {
        if (text == null)
        {
            return;
        }

        bool hasValue = value.HasValue;
        int numericValue = value.GetValueOrDefault();
        bool showValue = hasValue;

        if (hideWhenZero && numericValue <= 0)
        {
            showValue = false;
        }

        if (hideWhenZero)
        {
            SetStatDisplayActive(text, showValue);
        }
        else
        {
            SetStatDisplayActive(text, true);
        }

        text.text = showValue ? numericValue.ToString(CultureInfo.InvariantCulture) : string.Empty;
    }

    private void SetStatDisplayActive(TMP_Text text, bool isActive)
    {
        if (text == null)
        {
            return;
        }

        Transform displayTransform = text.transform != null && text.transform.parent != null
            ? text.transform.parent
            : text.transform;

        if (displayTransform == null)
        {
            return;
        }

        GameObject displayObject = displayTransform.gameObject;
        if (displayObject.activeSelf != isActive)
        {
            displayObject.SetActive(isActive);
        }
    }

    private Image FindImageUnder(string path)
    {
        Transform target = transform.Find(path);
        if (target == null)
        {
            return null;
        }

        return target.GetComponentInChildren<Image>(true);
    }

    private void CacheBaseStats()
    {
        if (_baseStatsCached)
        {
            Debug.Log($"{GetDebugContext()} CacheBaseStats skipped; already cached values {FormatStats(_baseStats)}.", this);
            return;
        }

        if (_definition != null && _definition.stats != null)
        {
            _baseStats = new CharacterStats(_definition.stats);
            _baseStatsCached = true;
            Debug.Log($"{GetDebugContext()} CacheBaseStats captured stats {FormatStats(_baseStats)}.", this);
        }
        else
        {
            Debug.LogWarning($"{GetDebugContext()} CacheBaseStats could not cache values because definition or stats are missing.", this);
        }
    }

    public CharacterStats GetBaseStatsClone()
    {
        if (!_baseStatsCached)
        {
            CacheBaseStats();
        }

        return _baseStats != null ? new CharacterStats(_baseStats) : null;
    }

    public void ResetToBaseStats()
    {
        if (_definition == null)
        {
            Debug.LogWarning($"{GetDebugContext()} ResetToBaseStats called but definition is null.", this);
            return;
        }

        if (!_baseStatsCached)
        {
            CacheBaseStats();
        }

        if (_baseStats != null)
        {
            _definition.stats = new CharacterStats(_baseStats);
        }
        else if (_definition.stats != null)
        {
            _definition.stats = new CharacterStats(_definition.stats);
        }

        UpdateStatsText();

        Debug.Log($"{GetDebugContext()} ResetToBaseStats applied. Cached base: {FormatStats(_baseStats)} | Definition stats: {FormatStats(_definition.stats)}.", this);
    }

    public void ApplyStats(CharacterStats stats, bool updateBase = false)
    {
        if (_definition == null)
        {
            Debug.LogWarning($"{GetDebugContext()} ApplyStats called but definition is null.", this);
            return;
        }

        if (stats != null)
        {
            _definition.stats = new CharacterStats(stats);
            if (updateBase)
            {
                _baseStats = new CharacterStats(stats);
                _baseStatsCached = true;
            }

            Debug.Log($"{GetDebugContext()} ApplyStats received values {FormatStats(stats)} (updateBase={updateBase}).", this);
        }
        else
        {
            _definition.stats = null;
            if (updateBase)
            {
                _baseStats = null;
                _baseStatsCached = true;
            }

            Debug.LogWarning($"{GetDebugContext()} ApplyStats received null stats (updateBase={updateBase}).", this);
        }

        UpdateStatsText();

        Debug.Log($"{GetDebugContext()} ApplyStats completed. Base cache: {FormatStats(_baseStats)} | Definition stats: {FormatStats(_definition.stats)}.", this);
    }

    public void RefreshStatsDisplay()
    {
        UpdateStatsText();
    }

    private string GetDebugContext()
    {
        string cardName = gameObject != null && !string.IsNullOrEmpty(gameObject.name)
            ? gameObject.name
            : "<unnamed>";

        return $"[CardView:{cardName}]";
    }

    private string FormatStats(CharacterStats stats)
    {
        if (stats == null)
        {
            return "<null>";
        }

        return $"ATK:{stats.attack} HP:{stats.health} ARM:{stats.armor} EXTRA:{stats.extraAttack} AOE:{stats.areaDamage} REG:{stats.regeneration} LUCK:{stats.luck} SCORE:{stats.score}";
    }
}
