using System;
using System.Collections.Generic;

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
