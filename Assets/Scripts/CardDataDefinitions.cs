using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

[Serializable]
public class RelationshipInfo
{
    public string targetId;
    public string targetName;
    public string relation;
}

[Serializable]
public class CharacterCardDefinition
{
    public string id;
    public string name;
    public List<string> families = new List<string>();
    public List<RelationshipInfo> relationships = new List<RelationshipInfo>();
    public CharacterStats stats;

    public string GetFamilyDisplayName()
    {
        if (families == null || families.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(", ", families);
    }

    public RelationshipInfo GetRelationshipById(string targetId)
    {
        if (relationships == null || string.IsNullOrEmpty(targetId))
        {
            return null;
        }

        for (int i = 0; i < relationships.Count; i++)
        {
            RelationshipInfo relationship = relationships[i];
            if (relationship == null)
            {
                continue;
            }

            if (string.Equals(relationship.targetId, targetId, StringComparison.OrdinalIgnoreCase))
            {
                return relationship;
            }
        }

        return null;
    }
}

[Serializable]
public class CharacterRelationshipDatabase
{
    public List<CharacterCardDefinition> characters = new List<CharacterCardDefinition>();
}

[Serializable]
public class CharacterStats
{
    public int attack;
    public int health;
    public int armor;
    public int extraAttack;
    public int areaDamage;
    public int regeneration;
    public int luck;
    public int score;

    private bool _hasValues;

    public bool HasValues => _hasValues;

    public CharacterStats()
    {
    }

    public CharacterStats(CharacterStats other)
    {
        if (other == null)
        {
            return;
        }

        attack = other.attack;
        health = other.health;
        armor = other.armor;
        extraAttack = other.extraAttack;
        areaDamage = other.areaDamage;
        regeneration = other.regeneration;
        luck = other.luck;
        score = other.score;
        _hasValues = other._hasValues;
    }

    public void AssignValue(string key, int value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        string normalized = NormalizeKey(key);

        switch (normalized)
        {
            case "hasar":
            case "atak":
                attack = value;
                break;
            case "can":
                health = value;
                break;
            case "kalkan":
                armor = value;
                break;
            case "ekstra atak":
                extraAttack = value;
                break;
            case "alan hasari":
                areaDamage = value;
                break;
            case "alan hasarı":
                areaDamage = value;
                break;
            case "can yenileme":
                regeneration = value;
                break;
            case "lootlama":
                luck = value;
                break;
            case "score":
                score = value;
                break;
            default:
                return;
        }

        _hasValues = true;
    }

    private static string NormalizeKey(string value)
    {
        string normalized = value.Trim().ToLowerInvariant();
        return normalized.Replace('ı', 'i');
    }
}

public static class CharacterStatsParser
{
    private static readonly Regex CharacterRegex = new Regex("\"(?<id>[^\"]+)\"\\s*:\\s*\{(?<body>[^}]*)\}", RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex FieldRegex = new Regex("\"(?<key>[^\"]+)\"\\s*:\\s*(?<value>-?\\d+)", RegexOptions.Compiled | RegexOptions.Multiline);

    public static Dictionary<string, CharacterStats> Parse(string json)
    {
        var result = new Dictionary<string, CharacterStats>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(json))
        {
            return result;
        }

        foreach (Match characterMatch in CharacterRegex.Matches(json))
        {
            if (!characterMatch.Success)
            {
                continue;
            }

            string id = characterMatch.Groups["id"].Value;
            string body = characterMatch.Groups["body"].Value;

            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(body))
            {
                continue;
            }

            var stats = new CharacterStats();

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

            if (!stats.HasValues)
            {
                continue;
            }

            result[id] = new CharacterStats(stats);
        }

        return result;
    }
}
