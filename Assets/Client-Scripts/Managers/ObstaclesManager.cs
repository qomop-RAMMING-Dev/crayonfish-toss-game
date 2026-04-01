using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 방해 요소(Obstacle)의 종류를 정의하는 열거형입니다.
/// </summary>
public enum ObstacleType { Blowfish, Seahorse, Octopus, Fisher }

/// <summary>
/// 특정 방해 요소 타입과 그 등장 확률(가중치)을 묶는 데이터 클래스입니다.
/// </summary>
[System.Serializable]
public class ObstacleSpawnInfo
{
    public ObstacleType type;

    [Tooltip("이 방해 요소가 선택될 확률 가중치 (합산 후 비율로 계산됨)")]
    public float spawnChance;
}

[System.Serializable]
public class ObstacleSpawnLevelData
{
    [Tooltip("이 설정이 적용될 플레이어 레벨")] 
    public int level;

    [Tooltip("다음 방해 요소 등장까지 최소 대기 시간 (초)")]
    public float minInterval;

    [Tooltip("다음 방해 요소 등장까지 최대 대기 시간 (초)")]
    public float maxInterval;

    [Tooltip("이 레벨에서 등장 가능한 방해 요소 목록 및 각 확률 가중치")]
    public List<ObstacleSpawnInfo> spawnChances;
}

/// <summary>
/// [방해 요소(Obstacle) 스폰 매니저]
///
/// 게임 내 모든 방해 요소의 생성 주기, 확률, 위치를 관리합니다.
/// - 플레이어 레벨에 따라 스폰 간격과 등장 가능한 방해 요소 종류가 달라집니다.
/// - 카메라 뷰포트를 기준으로 각 방해 요소 타입에 맞는 스폰 위치를 동적으로 계산합니다.
///
/// [외부 시스템 연동 지점]
/// - GetCurrentPlayerLevel() : 실제 프로젝트에서는 GameManager 등을 통해 플레이어 레벨을 가져옵니다.
/// </summary>
public class ObstaclesManager : MonoBehaviour
{
    // =====================================================================
    // Inspector Fields
    // =====================================================================

    [Header("Obstacle Prefabs")] 
    [SerializeField] [Tooltip("복어 방해 요소 프리팹")]
    private GameObject blowfishPrefab;

    [SerializeField] [Tooltip("해마 방해 요소 프리팹")]
    private GameObject seahorsePrefab;

    [SerializeField] [Tooltip("문어 방해 요소 프리팹")]
    private GameObject octopusPrefab;

    [SerializeField] [Tooltip("낚시꾼 방해 요소 프리팹")]
    private GameObject fisherPrefab;

    [Header("Spawn Settings")] 
    [SerializeField] [Tooltip("플레이어 레벨별 방해 요소 등장 정보")]
    private List<ObstacleSpawnLevelData> levelDataList;
    
    // =====================================================================
    // Private State
    // =====================================================================

    /// <summary> 현재 스폰 대기 타이머 (초) </summary>
    private float spawnTimer = 0f;

    /// <summary> 다음 스폰까지 남은 시간 간격 (초) </summary>
    private float currentSpawnInterval = 5f;

    private Camera mainCamera;
    
    // =====================================================================
    // Singleton
    // =====================================================================
    
    public static ObstaclesManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }
    
    // =====================================================================
    // Unity Lifecycle
    // =====================================================================

    private void Start()
    {
        mainCamera = Camera.main;
        SetNextSpawnInterval();
    }

    private void Update()
    {
        spawnTimer += Time.deltaTime;
        
        // 설정된 간격에 도달하면 방해 요소 스폰 시도
        if (spawnTimer >= currentSpawnInterval)
        {
            spawnTimer = 0f;
            SetNextSpawnInterval();
            TrySpawnRandomObstacle();
        }
    }
    
    // =====================================================================
    // Spawn Logic
    // =====================================================================

    /// <summary>
    /// 현재 플레이어 레벨에 맞는 레벨 데이터를 조회하여 다음 스폰 간격을 설정합니다.
    /// 해당 레벨 데이터가 없으면 기본값(5초)을 사용합니다.
    /// </summary>
    private void SetNextSpawnInterval()
    {
        int level = GetCurrentPlayerLevel();
        ObstacleSpawnLevelData data = GetLevelData(level);

        if (data != null)
        {
            currentSpawnInterval = Random.Range(data.minInterval, data.maxInterval);
        }
        else
        {
            Debug.LogWarning($"[ObstaclesManager] 레벨 {level}에 대한 스폰 데이터가 없습니다. 기본값(5초)을 사용합니다.");
            currentSpawnInterval = 5f;
        }
    }

    /// <summary>
    /// 현재 플레이어 레벨 기준으로 확률 가중치를 적용하여 방해 요소 스폰을 시도합니다.
    /// </summary>
    private void TrySpawnRandomObstacle()
    {
        int level = GetCurrentPlayerLevel();
        ObstacleSpawnLevelData data = GetLevelData(level);
        if (data == null) return;
        
        // 가중치 기반 랜덤으로 방해 요소 타입 선택
        ObstacleType? selectedType = GetRandomObstacleTypeByWeight(data.spawnChances);

        if (selectedType.HasValue)
            SpawnObstacle(selectedType.Value);
    }

    /// <summary>
    /// 지정된 타입의 방해 요소를 적절한 위치에 생성합니다.
    /// </summary>
    /// <param name="type">생성할 방해 요소 타입</param>
    private void SpawnObstacle(ObstacleType type)
    {
        GameObject prefab = GetPrefabByType(type);
        if (prefab == null)
        {
            Debug.LogWarning($"[ObstaclesManager] 타입 '{type}'에 대한 프리팹이 설정되지 않았습니다.");
            return;
        }

        Vector2 spawnPosition = GetSpawnPositionByType(type);
        Instantiate(prefab, spawnPosition, Quaternion.identity);
    }
    
    // =====================================================================
    // Spawn Position
    // =====================================================================

    /// <summary>
    /// 방해 요소 타입에 따라 카메라 뷰포트 기준의 스폰 위치를 계산하여 반환합니다.
    ///
    /// 스폰 위치 규칙:
    /// - Blowfish (복어) : 화면 내부 임의 위치 (경고 표시 후 해당 위치에 등장)
    /// - Seahorse (해마) : 화면 좌측 또는 우측 (랜덤 Y, 경계 X) - 50% 확률로 방향 결정
    /// - Octopus (문어)  : 화면 하단 (랜덤 X, 하단 Y)
    /// - Fisher (낚시꾼) : 화면 상단 (랜덤 X, 상단 Y) 
    /// </summary>
    /// <param name="type">방해 요소 타입</param>
    /// <returns>월드 좌표 기준의 스폰 위치</returns>
    private Vector2 GetSpawnPositionByType(ObstacleType type)
    {
        Vector3 viewportPos = Vector3.zero;
        
        // 화면 가장자리 기준 여백 비율 (5%)
        float padding = 0.05f;

        switch (type)
        {
            case ObstacleType.Blowfish:
                viewportPos = new Vector3(
                    Random.Range(padding, 1f - padding),
                    Random.Range(padding, 1f - padding),
                    0f
                );
                break;
            case ObstacleType.Seahorse:
                float seahorseX = Random.value < 0.5f ? padding : 1f - padding;
                viewportPos = new Vector3(seahorseX, Random.Range(0f, 1f), 0f);
                break;
            case ObstacleType.Octopus:
                viewportPos = new Vector3(Random.Range(0f, 1f), padding, 0f);
                break;
            case ObstacleType.Fisher:
                viewportPos = new Vector3(Random.Range(0f, 1f), 1f - padding, 0f);
                break;
        }
        
        // Z값을 카메라 거리에 맞게 보정 후 월드 좌표로 반환
        viewportPos.z = -mainCamera.transform.position.z;
        return mainCamera.ViewportToWorldPoint(viewportPos);
    }
    
    // =====================================================================
    // Utility
    // =====================================================================

    /// <summary>
    /// 확률 가중치 목록을 기반으로 방해 요소 타입을 랜덤 선택합니다.
    /// 모든 가중치의 합을 기준으로 선택 확률이 결정됩니다.
    /// </summary>
    /// <param name="chances">방해 요소별 가중치 목록</param>
    /// <returns>선택된 방해 요소 타입, 유효한 가중치가 없으면 null</returns>
    private ObstacleType? GetRandomObstacleTypeByWeight(List<ObstacleSpawnInfo> chances)
    {
        float total = chances.Sum(c => c.spawnChance);
        if (total <= 0f) return null;

        float roll = Random.Range(0f, total);
        float cumulative = 0f;

        foreach (var info in chances)
        {
            cumulative += info.spawnChance;
            if (roll <= cumulative) return info.type;
        }

        return null;
    }

    /// <summary>
    /// 방해 요소 타입에 해당하는 프리팹을 반환합니다.
    /// </summary>
    private GameObject GetPrefabByType(ObstacleType type)
    {
        return type switch
        {
            ObstacleType.Blowfish => blowfishPrefab,
            ObstacleType.Seahorse => seahorsePrefab,
            ObstacleType.Octopus => octopusPrefab,
            ObstacleType.Fisher => fisherPrefab,
            _ => null
        };
    }

    /// <summary>
    /// 지정한 레벨에 해당하는 스폰 설정 데이터를 반환합니다.
    /// </summary>
    private ObstacleSpawnLevelData GetLevelData(int level)
    {
        return levelDataList.FirstOrDefault(d => d.level == level);
    }
    
    // =====================================================================
    // External System Interface
    // =====================================================================

    /// <summary>
    /// 현재 플레이어 레벨을 반환합니다.
    ///
    /// [연동 지점]
    /// 실제 프로젝트에서는 GameManager 등 외부 시스템과 연결하여 플레이어 레벨을 가져옵니다.
    /// 예: return GameManager.Instance.player.Level;
    /// </summary>
    private int GetCurrentPlayerLevel()
    {
        // TODO: return GameManager.Instance.player.Level;
        return 1;
    }
}
