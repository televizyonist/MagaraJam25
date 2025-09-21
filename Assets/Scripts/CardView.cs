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
            return;
        }

        string spriteName = ResolveSpriteName();
        Sprite sprite = LoadPortraitSprite(spriteName);

        portraitImage.sprite = sprite;
        portraitImage.enabled = sprite != null;
    }

    private Sprite LoadPortraitSprite(string spriteName)
    {
        if (string.IsNullOrWhiteSpace(spriteName))
        {
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

        sprite = Resources.Load<Sprite>(resourcePath);

        if (sprite != null)
        {
            return sprite;
        }

        if (!string.IsNullOrEmpty(folder))
        {
            Sprite[] candidates = Resources.LoadAll<Sprite>(folder);
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
                        sprite = candidate;
                        break;
                    }
                }
            }
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
}
