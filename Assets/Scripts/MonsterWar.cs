using System.Collections;
using UnityEngine;

public static class MonsterWar
{
    private const string LogPrefix = "[MonsterWar]";

    public static IEnumerator SelfMonsterWar(BattleResolver resolver)
    {
        if (resolver == null)
        {
            Debug.LogWarning($"{LogPrefix} BattleResolver reference was null when attempting to run a monster war.");
            yield break;
        }

        IEnumerator routine = resolver.RunSelfMonsterWar();
        if (routine == null)
        {
            Debug.LogWarning($"{LogPrefix} BattleResolver did not provide a combat routine.");
            yield break;
        }

        yield return routine;
    }
}
