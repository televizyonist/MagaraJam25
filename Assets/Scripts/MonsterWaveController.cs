using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class MonsterWaveController : MonoBehaviour
{
    [SerializeField] private List<string> availableMonsterIds = new List<string>
    {
        "Monster_01",
        "Monster_02",
        "Monster_03",
        "Monster_04",
        "Monster_05"
    };

    [SerializeField] [Min(0)] private int initialWaveSize = 1;
    [SerializeField] private bool includeInactiveMonsters = true;

    private readonly List<MonsterView> _monsterSlots = new List<MonsterView>();
    private readonly Queue<string> _bagOfRandomMonsters = new Queue<string>();
    private readonly List<string> _monsterShuffleBuffer = new List<string>();
    private bool _initialWaveSpawned;
    private int _currentWaveSize;
    private int _currentWaveNumber;

    public IReadOnlyList<MonsterView> MonsterSlots => _monsterSlots;
    public int CurrentWaveSize => _currentWaveSize;
    public int CurrentWaveNumber => _currentWaveNumber;

    private void Awake()
    {
        CollectMonsterSlots();
        RefillMonsterBag();
        _currentWaveSize = Mathf.Clamp(initialWaveSize, 0, _monsterSlots.Count);
    }

    private void Start()
    {
        EnsureInitialWave();
    }

    public void EnsureInitialWave()
    {
        if (_initialWaveSpawned)
        {
            return;
        }

        if (_monsterSlots.Count == 0)
        {
            Debug.LogWarning("[MonsterWaveController] No MonsterView components were found to populate the initial wave.");
            return;
        }

        _currentWaveNumber = _currentWaveSize > 0 ? 1 : 0;
        SpawnWave(_currentWaveSize);
        _initialWaveSpawned = true;
    }

    [ContextMenu("Advance To Next Wave")]
    public void AdvanceToNextWave()
    {
        if (!_initialWaveSpawned)
        {
            EnsureInitialWave();
            return;
        }

        if (_monsterSlots.Count == 0)
        {
            Debug.LogWarning("[MonsterWaveController] Cannot advance waves because no MonsterView components are available.");
            return;
        }

        int desiredSize = Mathf.Clamp(_currentWaveSize + 1, 0, _monsterSlots.Count);
        if (desiredSize == 0)
        {
            desiredSize = Mathf.Min(1, _monsterSlots.Count);
        }

        if (desiredSize == _currentWaveSize)
        {
            SpawnWave(_currentWaveSize);
            _currentWaveNumber++;
            return;
        }

        _currentWaveSize = desiredSize;
        _currentWaveNumber++;
        SpawnWave(_currentWaveSize);
    }

    public void ResetWaves()
    {
        _currentWaveSize = Mathf.Clamp(initialWaveSize, 0, _monsterSlots.Count);
        _currentWaveNumber = 0;
        _initialWaveSpawned = false;
        EnsureInitialWave();
    }

    private void CollectMonsterSlots()
    {
        _monsterSlots.Clear();

        MonsterView[] foundMonsters = FindObjectsOfType<MonsterView>(includeInactiveMonsters);
        foreach (MonsterView monster in foundMonsters)
        {
            if (monster == null)
            {
                continue;
            }

            if (!monster.gameObject.scene.IsValid() || monster.gameObject.scene != gameObject.scene)
            {
                continue;
            }

            _monsterSlots.Add(monster);
        }

        if (_monsterSlots.Count == 0)
        {
            Debug.LogWarning("[MonsterWaveController] CollectMonsterSlots found no MonsterView instances in the active scene.");
        }
    }

    private void SpawnWave(int desiredActiveCount)
    {
        if (availableMonsterIds == null || availableMonsterIds.Count == 0)
        {
            Debug.LogWarning("[MonsterWaveController] No monster identifiers configured for spawning.");
            return;
        }

        if (_monsterSlots.Count == 0)
        {
            Debug.LogWarning("[MonsterWaveController] SpawnWave called with no MonsterView slots available.");
            return;
        }

        int clampedCount = Mathf.Clamp(desiredActiveCount, 0, _monsterSlots.Count);
        ShuffleMonsterSlots();

        for (int i = 0; i < _monsterSlots.Count; i++)
        {
            MonsterView slot = _monsterSlots[i];
            if (slot == null)
            {
                continue;
            }

            bool shouldBeActive = i < clampedCount;
            GameObject slotObject = slot.gameObject;
            if (slotObject.activeSelf != shouldBeActive)
            {
                slotObject.SetActive(shouldBeActive);
            }

            if (shouldBeActive)
            {
                string monsterId = SelectRandomMonsterId();
                if (string.IsNullOrEmpty(monsterId))
                {
                    Debug.LogWarning("[MonsterWaveController] Unable to assign a monster id because none were available in the bag.");
                    continue;
                }

                slot.SetMonsterId(monsterId);
            }
        }
    }

    private string SelectRandomMonsterId()
    {
        if (availableMonsterIds == null || availableMonsterIds.Count == 0)
        {
            return string.Empty;
        }

        if (_bagOfRandomMonsters.Count == 0)
        {
            RefillMonsterBag();
        }

        if (_bagOfRandomMonsters.Count == 0)
        {
            return availableMonsterIds[UnityEngine.Random.Range(0, availableMonsterIds.Count)];
        }

        return _bagOfRandomMonsters.Dequeue();
    }

    private void ShuffleMonsterSlots()
    {
        for (int i = _monsterSlots.Count - 1; i > 0; i--)
        {
            int swapIndex = UnityEngine.Random.Range(0, i + 1);
            MonsterView temp = _monsterSlots[i];
            _monsterSlots[i] = _monsterSlots[swapIndex];
            _monsterSlots[swapIndex] = temp;
        }
    }

    private void RefillMonsterBag()
    {
        _bagOfRandomMonsters.Clear();
        _monsterShuffleBuffer.Clear();

        if (availableMonsterIds == null)
        {
            return;
        }

        foreach (string monsterId in availableMonsterIds)
        {
            if (!string.IsNullOrWhiteSpace(monsterId))
            {
                _monsterShuffleBuffer.Add(monsterId);
            }
        }

        if (_monsterShuffleBuffer.Count == 0)
        {
            return;
        }

        for (int i = _monsterShuffleBuffer.Count - 1; i > 0; i--)
        {
            int swapIndex = UnityEngine.Random.Range(0, i + 1);
            string temp = _monsterShuffleBuffer[i];
            _monsterShuffleBuffer[i] = _monsterShuffleBuffer[swapIndex];
            _monsterShuffleBuffer[swapIndex] = temp;
        }

        for (int i = 0; i < _monsterShuffleBuffer.Count; i++)
        {
            _bagOfRandomMonsters.Enqueue(_monsterShuffleBuffer[i]);
        }
    }
}
