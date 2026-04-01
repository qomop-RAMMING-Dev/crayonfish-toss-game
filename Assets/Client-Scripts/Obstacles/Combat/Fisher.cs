using System.Collections;
using UnityEngine;

/// <summary>
/// [낚시꾼(Fisher) 방해 요소]
///
/// 동작 흐름:
///   1. 화면 상단에 등장하여 낚싯줄을 아래로 내립니다.
///   2. 낚싯줄이 최대 깊이까지 내려오는 동안 좌우로 흔들립니다.
///   3. 설정된 지속 시간(duration)이 지나면 오브젝트가 제거됩니다.
///   4. 낚싯바늘에 플레이어가 닿으면 피격 처리를 요청합니다.
///
/// 낚싯줄 길이는 플레이어 레벨에 비례하여 증가하므로,
/// 고레벨일수록 더 깊이까지 위험 범위가 확장됩니다.
///
/// [외부 시스템 연동 지점]
/// - GetCurrentPlayerLevel() : 실제 프로젝트에서는 GameManager 등으로부터 플레이어 레벨을 가져옵니다.
/// - OnPlayerHit()           : 실제 프로젝트에서는 PlayerController 등을 통해 사망 처리를 수행합니다.
/// </summary>
public class Fisher : MonoBehaviour
{
    // =====================================================================
    // Inspector Fields
    // =====================================================================

    [Header("Object References")] 
    [SerializeField] [Tooltip("낚싯줄 오브젝트의 Transform (Y 스케일을 조절하며 길이를 표현)")]
    private Transform lineTransform;

    [SerializeField] [Tooltip("낚싯바늘 오브젝트의 Transform (낚싯줄 끝에 고정)")]
    private Transform hookTransform;

    [Header("Fisher Behavior Settings")] 
    [SerializeField] [Tooltip("낚시꾼이 화면에 머무는 총 시간 (초)")]
    private float duration = 7f;

    [SerializeField] [Tooltip("낚싯줄이 내려올 수 있는 최대 길이 (유닛 단위)")]
    private float maxLineLength = 8f;

    [SerializeField] [Tooltip("플레이어 레벨 1 증가당 낚싯줄이 추가로 길어지는 양 (유닛 단위)")]
    private float lengthIncreasePerLevel = 1.5f;

    [SerializeField] [Tooltip("낚싯줄이 최대 깊이까지 내려오는 데 걸리는 시간 (초)")]
    private float descentTime = 2f;

    [SerializeField] [Tooltip("낚시꾼이 좌우로 흔들리는 최대 거리 (유닛 단위)")]
    private float swayAmount = 0.5f;

    [SerializeField] [Tooltip("낚시꾼이 좌우로 흔들리는 속도")]
    private float swaySpeed = 2f;
    
    // =====================================================================
    // Unity Lifecycle
    // =====================================================================

    private void Start()
    {
        StartCoroutine(FishingRoutine());
    }
    
    // =====================================================================
    // Behavior Coroutine
    // =====================================================================

    /// <summary>
    /// 낚시꾼의 전체 행동(줄 내리기 → 흔들기 → 소멸)을 관리하는 메인 코루틴
    /// </summary>
    private IEnumerator FishingRoutine()
    {
        // ── 1단계: 초기 상태 설정 ────────────────────────────────────────
        // 낚싯줄 길이를 0으로 초기화
        lineTransform.localScale = new Vector3(lineTransform.localScale.x, 0f, 1f);
        hookTransform.gameObject.SetActive(true);
        
        // 낚시꾼의 흔들림 기준이 될 초기 월드 위치 저장
        Vector3 initialPosition = transform.position;
        
        // ── 2단계: 플레이어 레벨 기반 낚싯줄 목표 길이 계산 ─────────────
        // 레벨이 높을수록 낚싯줄이 더 길어져 위험 범위가 증가합니다.
        int playerLevel = GetCurrentPlayerLevel();
        float adjustedMaxLength = maxLineLength + ((playerLevel - 1) * lengthIncreasePerLevel);
        float targetLength = Random.Range(1f, adjustedMaxLength);
        
        // TODO: AudioManager.Instance.PlaySfx(sfxIndex); 등으로 낚시 사운드 재생
        
        // ── 3단계: duration 동안 낚싯줄 내리기 + 흔들기 반복 ────────────
        float elapsedTime = 0f;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            
            // descentTime 동안 낚싯줄 길이를 목표 길이까지 부드럽게 증가
            float descentRatio = Mathf.Clamp01(elapsedTime / descentTime);
            float currentLength = Mathf.Lerp(0f, targetLength, descentRatio);
            
            // 낚싯줄 길이 적용 (Y 스케일 조절, Pivot이 상단 기준이므로 아래로 자람)
            lineTransform.localScale = new Vector3(lineTransform.localScale.x, currentLength, 1f);
            
            // 낚싯줄 오브젝트 위치: 늘어난 길이만큼 아래로 이동
            lineTransform.localPosition = new Vector3(0f, -currentLength, 0f);
            
            // 낚싯바늘 위치: 낚싯줄 끝에 고정
            hookTransform.localPosition = lineTransform.localPosition;
            
            // 낚시꾼 전체를 좌우로 흔들어 줄과 바늘이 함께 움직이도록
            float swayX = Mathf.Sin(Time.time * swaySpeed) * swayAmount;
            transform.position = new Vector3(initialPosition.x + swayX, initialPosition.y, initialPosition.z);

            yield return null;
        }
        
        // ── 4단계: 지속 시간 종료 → 오브젝트 제거 ───────────────────────
        Destroy(gameObject);
    }
    
    // =====================================================================
    // Collision
    // =====================================================================

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
            OnPlayerHit(collision);
    }
    
    // =====================================================================
    // External System Interface
    // =====================================================================

    /// <summary>
    /// 현재 플레이어 레벨을 반환합니다.
    ///
    /// [연동 지점] 실제 프로젝트에서는 GameManager 등에서 레벨을 가져옵니다.
    /// 예: return GameManager.Instance.player.Level;
    /// </summary>
    private int GetCurrentPlayerLevel()
    {
        // TODO: return GameManager.Instance.player.Level;
        return 1;
    }

    /// <summary>
    /// 낚싯바늘과 플레이어 충돌 시 피격 처리를 수행합니다.
    ///
    /// [연동 지점] 실제 프로젝트에서는 PlayerController.Die() 등을 호출합니다.
    /// 예: collision.gameObject.GetComponent<PlayerController>()?.Die();
    /// </summary>
    private void OnPlayerHit(Collision2D collision)
    {
        // TODO: collision.gameObject.GetComponent<PlayerController>()?.Die();
        Debug.Log($"[Fisher] 낚싯바늘에 플레이어 피격: {collision.gameObject.name}");
    }
}
