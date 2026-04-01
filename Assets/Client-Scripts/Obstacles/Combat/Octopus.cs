using System.Collections;
using UnityEngine;

/// <summary>
/// [문어(Octopus) 방해 요소]
///
/// 동작 흐름:
///   1. 등장 후 initialDelay 초를 대기합니다.
///   2. 대기 시간이 지나면 UI 캔버스에 먹물(InkTrap) 이펙트를 재생하고 스스로 제거됩니다.
///   3. 플레이어가 touchThreshold 획수만큼 탭하면 먹물 발사 전에 문어가 제거됩니다.
///
/// [외부 시스템 연동 지점]
/// - PlayerAppearSound() : 실제 프로젝트에서는 AudioManager 등을 통해 등장 효과음을 재생합니다.
/// - PlayTouchSound()    : 실제 프로젝트에서는 AudioManager 등을 통해 터치 효과음을 재생합니다.
/// - PlayInkSound()      : 실제 프로젝트에서는 AudioManager 등을 통해 먹물 발사 효과음을 재생합니다. 
/// </summary>
public class Octopus : MonoBehaviour
{
    // =====================================================================
    // Inspector Fields
    // =====================================================================

    [Header("Screen Ink Effect (UI)")] 
    [SerializeField] [Tooltip("InkTrap 프리팹 (UI 캔버스에 인스턴스화 됨)")]
    private InkTrap inkTrapPrefab;

    [Header("Timing Settings")] 
    [SerializeField] [Tooltip("등장 후 먹물을 발사하기까지의 대기 시간 (초)")]
    private float initailDelay = 3f;

    [Header("Touch Settings")] 
    [SerializeField] [Tooltip("문어를 제거하기 위해 필요한 탭 횟수")]
    private int touchThreshold = 3;
    
    // =====================================================================
    // Private State
    // =====================================================================

    /// <summary>현재까지 문어를 탭한 횟수</summary>
    private int touchCount = 0;

    /// <summary>먹물 발사 코루틴 참조 (제거 시 중단하기 위해 보관)</summary>
    private Coroutine fireRoutine;

    /// <summary>
    /// InkTrap 인스턴스를 재활용하기 위한 캐시
    /// 매 문어 등장마다 새로 생성하지 않고, 한 번 생성한 인스턴스를 재사용합니다.
    /// </summary>
    private static InkTrap cachedTrapInstance;
    
    // =====================================================================
    // Unity Lifecycle
    // =====================================================================

    private void Start()
    {
        Debug.Log("[Octopus] 문어 등장!");
        PlayAppearSound();

        fireRoutine = StartCoroutine(FireOnceAndDisappearRoutine());
    }
    
    // =====================================================================
    // Input
    // =====================================================================

    private void OnMouseDown()
    {
        PlayTouchSound();
        touchCount++;
        
        // 탭 횟수가 임계값에 도달하면 먹물 발사 취소
        if (touchCount >= touchThreshold)
            CancelAndDestroy();
    }
    
    // =====================================================================
    // Behavior Coroutine
    // =====================================================================

    /// <summary>
    /// 대기 후 먹물 이펙트를 재생하고 오브젝트를 제거하는 코루틴
    /// </summary>
    private IEnumerator FireOnceAndDisappearRoutine()
    {
        yield return new WaitForSeconds(initailDelay);
        
        // UI 캔버스 검색
        var canvasGO = GameObject.Find("UI_Canvas");
        if (canvasGO == null)
        {
            Debug.LogError("[Octopus] 'UI_Canvas' 오브젝트를 찾을 수 없습니다. InkTrap을 생성하려면 씬에 UI_Canvas가 필요합니다.");
        }
        else
        {
            // InkTrap 인스턴스가 없을 대와 새로 생성하고, 이후에는 재활용
            if (cachedTrapInstance == null)
            {
                cachedTrapInstance = Instantiate(
                    inkTrapPrefab,
                    canvasGO.transform,
                    false // worldPositionStays: false → RectTransform이 부모 기준으로 설정됨
                );
            }
            
            // 먹물 이펙트 재생 (visibleDuration / fadeDuration 코루틴 내부에서 처리됨)
            cachedTrapInstance.InkTrapPlay();
            PlayInkSound();
        }
        
        Debug.Log("[Octopus] 문어 사라짐 (역할 완료)");
        Destroy(gameObject);
    }
    
    // =====================================================================
    // Cancel
    // =====================================================================

    /// <summary>
    /// 플레이어 탭으로 문어를 제거할 때 호출됩니다.
    /// 아직 먹물을 발사하지 않았다면 코루틴을 중단하고 오브젝트를 제거합니다.
    /// </summary>
    private void CancelAndDestroy()
    {
        if (fireRoutine != null)
            StopCoroutine(fireRoutine);
        
        Debug.Log("[Octopus] 터치로 취소됨.");
        Destroy(gameObject);
    }
    
    // =====================================================================
    // External System Interface
    // =====================================================================

    /// <summary>
    /// 문어 등장 효과음을 재생합니다.
    ///
    /// [연동 지점] 실제 프로젝트에서는 AudioManager 등을 통해 효과음을 재생합니다.
    /// 예: AudioManager.Instance.PlaySfx(sfxIndex);
    /// </summary>
    private void PlayAppearSound()
    {
        // TODO: AudioManager.Instance.PlaySfx(sfxIndex);
    }

    /// <summary>
    /// 문어 터치 효과음을 재생합니다.
    ///
    /// [연동 지점] 실제 프로젝트에서는 AudioManager 등을 통해 효과음을 재생합니다.
    /// 예: AudioManager.Instance.PlaySfx(sfxIndex);
    /// </summary>
    private void PlayTouchSound()
    {
        // TODO: AudioManager.Instance.PlaySfx(sfxIndex);
    }

    /// <summary>
    /// 먹물 발사 효과음을 재생합니다.
    ///
    /// [연동 지점] 실제 프로젝트에서는 AudioManager 등을 통해 효과음을 재생합니다.
    /// 예: AudioManager.Instance.PlaySfx(sfxIndex);
    /// </summary>
    private void PlayInkSound()
    {
        // TODO: AudioManager.Instance.PlaySfx(sfxIndex);
    }
}
