using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class CardView : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text familyText;

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
        if (_definition != null)
        {
            ApplyDefinition();
        }
    }

    private void Reset()
    {
        _rectTransform = GetComponent<RectTransform>();
        EnsureTextReferences();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            _rectTransform = GetComponent<RectTransform>();
            EnsureTextReferences();
            if (_definition != null)
            {
                ApplyDefinition();
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

        if (nameText != null)
        {
            nameText.text = _definition != null ? _definition.name : string.Empty;
        }

        if (familyText != null)
        {
            familyText.text = _definition != null ? _definition.GetFamilyDisplayName() : string.Empty;
        }
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

    private TMP_Text FindTextUnder(string path)
    {
        Transform target = transform.Find(path);
        if (target == null)
        {
            return null;
        }

        return target.GetComponentInChildren<TMP_Text>(true);
    }
}
