using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// [WebGL Safe Area 컨트롤러]
///
/// WebGL 빌드 환경에서 Toss 웹 프레임워크의 Safe Area 값을 수신하여 UI에 적용한다.
///
/// 동작 흐름:
///   1. Start()에서 NativeSafeAreaBridge.jslib를 통해 JavaScript에 Safe Area 데이터를 요청합니다.
///   2. JavaScript가 응답하면 OnSafeAreaReceived()가 호출되어 Safe Area를 UI에 적용합니다.
///   3. 일정 시간(fallbackTimeout) 내에 응답이 없으면 Fallback으로 UI를 기본 위치에 강제 표시합니다.
///
/// Safe Area 적용 방식:
/// 픽셀 단위의 inset 값을 뷰포트 비율(0~1)로 변환한 뒤,
/// 대상 RectTransform의 anchorMin / anchorMax를 조정하여 Safe Area를 구현합니다.
///
/// [외부 시스템 연동 지점]
/// - NativeSafeAreaBridge.jslib : C# DllImport와 연결되는 JavaScript 플러그인
/// - TossFrameworkBridge.js     : Toss 웹 프레임워크 API를 호출하고 Unity로 데이터를 전달하는 스크립트
/// </summary>
public class UnitySafeAreaController : MonoBehaviour
{
    // =====================================================================
    // Native Plugin Import
    // =====================================================================

    /// <summary>
    /// NativeSafeAreaBridge.jslib에 구현된 함수를 C#에서 호출하기 위한 선언입니다.
    /// WebGL 빌드 시 Unity가 jslib 파일과자동으로 연결합니다.
    /// </summary>
    /// <param name="gameObjectName">응답 받을 GameObject의 이름</param>
    /// <param name="methodName">응답 받을 C# 메서드 이름</param>
    [DllImport("__Internal")]
    private static extern void RequestSafeAreaInsets(string gameObjectName, string methodName);
    
    // =====================================================================
    // Inspector Fields
    // =====================================================================

    [Header("UI Settings")] 
    [SerializeField] [Tooltip("Safe Area를 적용할 UI의 최상위 RectTransform")]
    private RectTransform safeAreaRect;

    [Header("Fallback Settings")] 
    [SerializeField] [Tooltip("이 시간(초) 내에 JavaScript 응답이 없으면 UI를 기본 위치에 강제 표시")]
    private float fallbackTimeout = 3.0f;
    
    // =====================================================================
    // Private State
    // =====================================================================

    private CanvasGroup safeAreaCanvasGroup;

    /// <summary>Safe Area가 성공적으로 적용되었는지 여부 (Fallback 중복 실행 방지)</summary>
    private bool isSafeAreaApplied = false;
    
    // =====================================================================
    // Data Model
    // =====================================================================

    /// <summary>
    /// JavaScript로부터 전달받는 Safe Area JSON 데이터 모델.
    /// JsonUtility.FromJson으로 역직렬화됩니다.
    /// </summary>
    [System.Serializable]
    private class SafeAreaPayload
    {
        public int top = 0;
        public int bottom = 0;
        public int left = 0;
        public int right = 0;
        public int canvasWidth = 0;
        public int canvasHeight = 0;
    }
    
    // =====================================================================
    // Unity Lifecycle
    // =====================================================================

    private void Awake()
    {
        // Inspector에 safeAreaRect가 할당되지 않았다면 자신의 RectTransform을 사용
        if (safeAreaRect == null)
            safeAreaRect = GetComponent<RectTransform>();
        
        // Safe Area 적용 전까지 UI를 투명하게 숨겨 레이아웃 이동이 보이지 않도록 합니다.
        safeAreaCanvasGroup = safeAreaRect.GetComponent<CanvasGroup>()
                              ?? safeAreaRect.gameObject.AddComponent<CanvasGroup>();
        safeAreaCanvasGroup.alpha = 0f;
    }

    private void Start()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        // WebGL 빌드 환경: JavsScript에 Safe Area 값 요청 및 Fallback 타이머 시작
        StartCoroutine(RequestWithFallback());
#else
        // 에디터 환경: Safe Area를 적용할 수 없으므로 UI를 즉시 표시
        Debug.Log("[UnitySafeAreaController] 에디터 환경에서는 Safe Area를 테스트할 수 없습니다. UI를 기본 위치에 표시합니다.");
        if (safeAreaCanvasGroup != null) safeAreaCanvasGroup.alpha = 1f;
#endif
    }
    
    // =====================================================================
    // Request & Fallback
    // =====================================================================

    /// <summary>
    /// JavaScript에 Safe Area 데이터를 요청하고, fallbackTimeout 내에 응답이 없으면
    /// UI를 기본 위치에 강제 표시하는 Fallback처리를 수행하는 코루틴.
    ///
    /// 지연 요청 이유:
    /// 브라우저가 CSS를 적용하고 Unity 캔버스 크기를 확정하기까지 시간이 필요하기 때문에
    /// Start()에서 즉시 요청하지 않고 이 코루틴을 통해 요청합니다.
    /// (실제 지연 시간은 TossFrameworkBridge.js 내부의 setTimeout에서 추가로 적용됩니다.)
    /// </summary>
    private IEnumerator RequestWithFallback()
    {
        // JavaScript에 Safe Area 데이터 요청
        // 응답은 비동기로 OnSafeAreaReceived()에 전달됩니다.
        RequestSafeAreaInsets(gameObject.name, nameof(OnSafeAreaReceived));
        
        // Fallback 타이머 대기
        yield return new WaitForSeconds(fallbackTimeout);
        
        // 타이머 종료 시점까지 응답이 없으면 UI 강제 표시
        if (!isSafeAreaApplied)
        {
            Debug.LogWarning("[UnitySafeAreaController] Safe Area 데이터를 받지 못했습니다. UI를 기본 위치에 강제 표시합니다.");
            if (safeAreaCanvasGroup != null) safeAreaCanvasGroup.alpha = 1f;
        }
    }
    
    // =====================================================================
    // JavaScript Callback
    // =====================================================================

    /// <summary>
    /// TossFrameworkBridge.js로부터 Safe Area값을 JSON 문자열로 수신하는 콜백 함수
    /// RequestSafeAreaInsets() 호출 시 전달한 메서드 이름과 반드시 일치해야 합니다.
    /// Unity의 SendMessage 메커니즘으로 호출되므로 public으로 선언합니다.
    /// </summary>
    /// <param name="jsonPayload"></param>
    public void OnSafeAreaReceived(string jsonPayload)
    {
        // Fallback으로 이미 UI가 표시된 경우 중복 실행 방지
        if (isSafeAreaApplied) return;
        isSafeAreaApplied = true;

        if (string.IsNullOrEmpty(jsonPayload))
        {
            Debug.LogError("[UnitySafeAreaController] JavaScript로부터 받은 데이터가 비어 있습니다.");
        }
        else
        {
            SafeAreaPayload payload = JsonUtility.FromJson<SafeAreaPayload>(jsonPayload);

            if (safeAreaRect != null && payload.canvasWidth > 0 && payload.canvasHeight > 0)
            {
                ApplySafeArea(payload);
            }
            else
            {
                Debug.LogWarning("[UnitySafeAreaController] Safe Area 적용에 필요한 데이터가 유효하지 않습니다.");
            }
        }
        
        // 성공 여부와 관계없이 데이터 수신 시 UI 즉시 표시
        if (safeAreaCanvasGroup != null)
        {
            safeAreaCanvasGroup.alpha = 1f;
            Debug.Log("[UnitySafeAreaController] 데이터 수신 완료. UI 표시.");
        }
    }
    
    // =====================================================================
    // Safe Area Application
    // =====================================================================

    /// <summary>
    /// 수신한 Safe Area 픽셀 값을 뷰포트 비율로 변환하여 RectTransform의 앵커에 적용합니다.
    ///
    /// 변환 방식:
    /// inset(px) / canvasSize(px) → 뷰포트 비율(0~1)
    ///
    /// 앵커 적용 방식:
    /// anchorMin = (left 비율, bottom 비율)
    /// anchorMax = (1 - right 비율, 1 - top 비율)
    /// offsetMin / offsetMax = Vector2.zero (앵커 기준으로 크기가 결정되도록)
    /// </summary>
    /// <param name="payload">픽셀 단위의 Safe Area inset 값 및 캔버스 크기</param>
    private void ApplySafeArea(SafeAreaPayload payload)
    {
        Debug.Log($"[UnitySafeAreaController] Safe Area 적용 시작 - 캔버스 크기: {payload.canvasWidth}x{payload.canvasHeight}");
        
        // 픽셀 단위 inset → 뷰포트 비율(0~1) 변환
        float topInset = (float)payload.top / payload.canvasHeight;
        float bottomInset = (float)payload.bottom / payload.canvasHeight;
        float leftInset = (float)payload.left / payload.canvasWidth;
        float rightInset = (float)payload.right / payload.canvasWidth;
        
        // RectTransform 앵커 조정
        safeAreaRect.anchorMin = new Vector2(leftInset, bottomInset);
        safeAreaRect.anchorMax = new Vector2(1f - rightInset, 1f - topInset);
        
        // 앵커 기준으로 크기가 결정되도록 offset 초기화
        safeAreaRect.offsetMin = Vector2.zero;
        safeAreaRect.offsetMax = Vector2.zero;
        
        Debug.Log($"[UnitySafeAreaController] Safe Area 적용 완료 - anchorMin: {safeAreaRect.anchorMin}, anchorMax: {safeAreaRect.anchorMax}");
    }
}
