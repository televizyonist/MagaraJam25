using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class CardView : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text familyText;
    [SerializeField] private Image portraitImage;
    [SerializeField] private string characterSpriteResourceFolder = "Characters";

    private RectTransform _rectTransform;
    private CharacterCardDefinition _definition;

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
        if (_definition != null)
        {
            ApplyDefinition();
        }
        else
        {
            UpdatePortraitSprite();
        }
    }

    private void Reset()
    {
        _rectTransform = GetComponent<RectTransform>();
        EnsureTextReferences();
        EnsureImageReference();
        UpdatePortraitSprite();
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
        }
    }

    public void SetData(CharacterCardDefinition definition)
    {
        _definition = definition;
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

        if (nameText != null)
        {
            nameText.text = _definition != null ? _definition.name : string.Empty;
        }

        if (familyText != null)
        {
            familyText.text = _definition != null ? _definition.GetFamilyDisplayName() : string.Empty;
        }

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
    }

    private void EnsureImageReference()
    {
        if (portraitImage == null)
        {
            portraitImage = FindImageUnder("Image");
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

    private Image FindImageUnder(string path)
    {
        Transform target = transform.Find(path);
        if (target == null)
        {
            return null;
        }

        return target.GetComponentInChildren<Image>(true);
    }

    private string GetDebugContext()
    {
        string cardName = gameObject != null && !string.IsNullOrEmpty(gameObject.name)
            ? gameObject.name
            : "<unnamed>";

        return $"[CardView:{cardName}]";
    }
}
