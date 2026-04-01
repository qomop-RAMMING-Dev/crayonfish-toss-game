using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// [먹물 화면 이펙트(InkTrap) 관리 스크립트]
///
/// 문어(Octopus)가 먹물을 발사할 때 화면 전체를 덮는 반투명 UI 이펙트를 표시합니다.
/// 이펙트는 visibleDuration 동안 유지된 후 서서히 투명해집니다.
///
/// 플레이어가 화면을 탭하면 터치 횟수에 따라 이펙트가 점점 투명해지고,
/// touchThreshold 횟수에 도달하면 빠르게 페이드아웃되어 제거됩니다.
///
/// 성능 최적화를 위해 Octopus 스크립트에서 이 인스턴스를 캐시하여 재활용합니다.
///
/// [외부 시스템 연동 지점]
/// - PlayTouchSound()   : 실제 프로젝트에서는 AudioManager 등을 통해 터치 효과음을 재생합니다.
/// - DisableJoysticks() : 먹물 활성화 중 조이스틱 입력을 차단합니다. (다른 팀원 담당 Joystick 시스템과 연동)
/// - EnableJoysticks()  : 먹물 제거 후 조이스틱 입력을 다시 활성화합니다.
/// </summary>
public class InkTrap : MonoBehaviour, IPointerClickHandler
{
    // =====================================================================
    // Inspector Fields
    // =====================================================================

    [Header("UI References")] 
    [SerializeField] [Tooltip("화면을 덮는 반투명 UI Image 컴포넌트")]
    private Image inkImage;

    [Header("Timing Settings")] 
    [SerializeField] [Tooltip("먹물 이펙트가 유지되는 시간 (초)")]
    private float visibleDuration = 5f;

    [SerializeField] [Tooltip("이펙트가 서서히 사라지는 페이드아웃 시간 (초)")]
    private float fadeDuration = 3f;

    [Header("Touch Settings")] 
    [SerializeField] [Tooltip("이펙트를 조기에 제거하기 위해 필요한 탭 횟수")]
    private int touchThreshold = 3;

    [SerializeField] [Tooltip("탭으로 취소할 때 적용되는 빠른 페이드아웃 시간 (초)")]
    private float cancelFadeDuration = 0.5f;
    
    // =====================================================================
    // Private State
    // =====================================================================

    /// <summary>현재까지 화면을 탭한 횟수</summary>
    private int touchCount = 0;

    /// <summary>탭 취소 페이드아웃이 진행 중인지 여부 (중복 취소 방지)</summary>
    private bool isCancelling = false;
    
    private Coroutine _playRoutine;
    private Coroutine _cancelRoutine;
    
    // =====================================================================
    // Component References
    // =====================================================================

    private CanvasGroup cg;
    private Image image;
    
    // =====================================================================
    // Singleton Cache (Octopus에서 인스턴스를 재활용하기 위해 사용)
    // =====================================================================

    public static InkTrap cachedTrapInstance;
    
    // =====================================================================
    // Unity Lifecycle
    // =====================================================================

    private void Awake()
    {
        cachedTrapInstance = this;

        cg = GetComponent<CanvasGroup>();
        image = GetComponent<Image>();
        
        // 초기 상태: 비활성화 및 레이캐스트 차단 해제
        cg.alpha = 0f;
        cg.blocksRaycasts = false;
        image.enabled = false;
        image.raycastTarget = false;
        if (inkImage != null) inkImage.raycastTarget = false;
    }
    
    // =====================================================================
    // Public API
    // =====================================================================

    /// <summary>
    /// 먹물 화면 이펙트를 재생합니다.
    /// 이미 진행 중인 이펙트가 있다면 초기화 후 다시 시작합니다.
    /// 문어(Octopus) 스크립트에서 호출됩니다.
    /// </summary>
    public void InkTrapPlay()
    {
        isCancelling = false;
        touchCount = 0;

        gameObject.SetActive(true);
        
        // 진행 중인 코루틴 정리
        if (_playRoutine != null) { StopCoroutine(_playRoutine); _playRoutine = null; }
        if (_cancelRoutine != null) { StopCoroutine(_cancelRoutine); _cancelRoutine = null; }
        
        // UI 활성화 및 레이캐스트 차단 설정
        cg.alpha = 1f;
        cg.blocksRaycasts = true;
        image.enabled = true;
        image.raycastTarget = true;
        if (inkImage != null) inkImage.raycastTarget = true;
        
        // [연동 지점] 먹물 활성화 중 조이스틱 입력 차단 (다른 팀원 담당 Joystick 시스템과 연동)
        // TODO: DisableJoysticks();

        _playRoutine = StartCoroutine(PlayRoutine());
    }
    
    // =====================================================================
    // Coroutines
    // =====================================================================

    /// <summary>
    /// visibleDuration 동안 이펙트를 유지한 후 fadeDuration 동안 서서히 페이드아웃하는 코루틴
    /// </summary>
    private IEnumerator PlayRoutine()
    {
        if (inkImage != null) inkImage.enabled = true;

        cg.alpha = 1f;
        cg.blocksRaycasts = true;
        
        // 일정 시간 유지
        yield return new WaitForSeconds(visibleDuration);
        
        // 자연 페이드아웃
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            yield return null;
        }
        
        // 이펙트 완전히 비활성화
        cg.alpha = 0f;
        cg.blocksRaycasts = false;
        if (inkImage != null) inkImage.enabled = false;
    }

    /// <summary>
    /// 탭 취소 시 cancelFadeDuration 동안 빠르게 페이드아웃하고 오브젝트를 비활성화하는 코루틴
    /// </summary>
    private IEnumerator CancelFadeRoutine()
    {
        float startAlpha = cg.alpha;
        float elapsed = 0f;

        while (elapsed < cancelFadeDuration)
        {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / cancelFadeDuration);
            yield return null;
        }

        gameObject.SetActive(false);
        
        // [연동 지점] 먹물 제거 후 조이스틱 입력 재활성화 (다른 팀원 담당 Joystick 시스템과 연동)
        // TODO: EnableJoysticks();
    }
    
    // =====================================================================
    // Touch / Click
    // =====================================================================

    /// <summary>
    /// 화면 탭(클릭) 이벤트를 처리합니다.
    /// 탭할수록 이펙트가 점점 투명해지고, touchThreshold 도달 시 빠른 페이드아웃이 시작됩니다.
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (isCancelling) return;

        touchCount++;
        PlayTouchSound();

        if (touchCount < touchThreshold)
        {
            // 탭 횟수에 비례하여 점진적으로 투명해짐
            cg.alpha = 1f - (float)touchCount / touchThreshold;
        }
        else
        {
            // 임계값 도달 시 취소 처리 (한 번만 실행)
            isCancelling = true;
            CancelInk();
        }
    }

    /// <summary>
    /// 탭 취소 처리: 진행 중인 이펙트를 중단하고 빠른 페이드아웃을 시작합니다.
    /// </summary>
    private void CancelInk()
    {
        if (_playRoutine != null) { StopCoroutine(_playRoutine); _playRoutine = null; }
        
        // 추가 탭 입력 차단
        cg.blocksRaycasts = false;
        image.raycastTarget = false;
        if (inkImage != null) inkImage.raycastTarget = false;

        _cancelRoutine = StartCoroutine(CancelFadeRoutine());
    }
    
    // =====================================================================
    // External System Interface
    // =====================================================================

    /// <summary>
    /// 먹물 터치 효과음을 재생합니다.
    ///
    /// [연동 지점] 실제 프로젝트에서는 AudioManager 등을 통해 효과음을 재생합니다.
    /// 예: AudioManager.Instance.PlaySfx(sfxIndex);
    /// </summary>
    private void PlayTouchSound()
    {
        // TODO: AudioManager.Instance.PlaySfx(sfxIndex);
    }
}
