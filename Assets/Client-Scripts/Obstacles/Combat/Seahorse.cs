using System.Collections;
using UnityEngine;

/// <summary>
/// [해마(Seahorse) 방해 요소]
///
/// 동작 흐름:
///   1. 경고(Warning) : 물대포 발사 위치에 경고 UI를 표시하고 깜빡임 효과를 재생합니다.
///   2. 발사(Fire)    : 경고 종료 후 물대포 콜라이더를 활성화하고 물줄기 이펙트를 재생합니다.
///   3. 정리(Cleanup) : fireDuration이 지나면 이펙트를 비활성화하고 오브젝트를 제거합니다.
///   + 물대포에 접촉한 플레이어는 위아래 방향으로 밀려납니다.
///
/// 해마는 화면 좌우 중 한쪽 가장자리에서 등장하며,
/// 자신의 X 좌표를 기준으로 스프라이트 방향과 물대포 방향을 자동으로 결정합니다.
///
/// 물줄기 UV 스크롤은 Unity 표준 API(MaterialPropertyBlock)로 직접 구현합니다.
///
/// [외부 시스템 연동 지점]
/// - PlayAttackSound() : 실제 프로젝트에서는 AudioManager 등을 통해 공격 효과음을 재생합니다.
/// - StopAttackSound() : 실제 프로젝트에서는 재생 중인 효과음을 정지합니다.
/// </summary>
public class Seahorse : MonoBehaviour
{
    // =====================================================================
    // Inspector Fields
    // =====================================================================

    [Header("Behavior Settings")] 
    [SerializeField] [Tooltip("경고 표시 후 물대포 발사까지 대기하는 시간 (초)")]
    private float initialDelay = 2f;

    [SerializeField] [Tooltip("물대포가 발사되는 시간 (초)")]
    private float fireDuration = 5f;

    [SerializeField] [Tooltip("물대포에 닿은 플레이어에게 밀어내는 힘의 크기")]
    private float pushForce = 15f;

    [Header("Object References")] 
    [SerializeField] [Tooltip("물대포 충돌 범위를 담당하는 자식 오브젝트")]
    private GameObject waterCannonCollider;

    [SerializeField] [Tooltip("물대포 이펙트 프리팹 (SpriteRenderer 포함)")]
    private GameObject waterPrefab;

    [SerializeField] [Tooltip("물대포 이펙트가 시작되는 기준 Transform")]
    private Transform waterPoint;

    [Header("Water UV Scroll")] 
    [SerializeField] [Tooltip("물대포 텍스처가 흐르는 속도 (UV 단위/초)")]
    private float uvScrollSpeed = 1f;

    [Header("Warning")] 
    [SerializeField] [Tooltip("물대포 경고 UI 프리팹 (UI 캔버스에 인스턴스화 됨)")]
    private GameObject warningUIPrefab;
    
    // =====================================================================
    // Private State
    // =====================================================================

    /// <summary>현재 물대포가 발사 중인지 여부 (일시정지 처리에 사용)</summary>
    private bool isFiring;

    private GameObject warningInstance;
    private CanvasGroup warningCanvasGroup;
    private GameObject waterBeamInstance;
    private SpriteRenderer waterBeamRenderer;
    private MaterialPropertyBlock waterMPB;
    private Coroutine warnBlinkCo;
    private Coroutine uvScrollCo;
    
    // =====================================================================
    // Unity Lifecycle
    // =====================================================================

    private void Start()
    {
        if (waterCannonCollider) waterCannonCollider.SetActive(false);
        
        // 등장 위치(X 좌표)를 기준으로 스프라이트 방향 결정
        FlipTowardCenter();
        StartCoroutine(FireRoutine());
    }
    
    // =====================================================================
    // Direction
    // =====================================================================

    /// <summary>
    /// 화면 중앙을 향하도록 스프라이트 방향을 설정합니다.
    /// X 좌표가 양수(오른쪽)이면 좌측을 향하도록 반전합니다.
    /// </summary>
    private void FlipTowardCenter()
    {
        if (transform.position.x > 0)
            transform.localScale = new Vector3(-1f, 1f, 1f);
    }
    
    // =====================================================================
    // Behavior Coroutine
    // =====================================================================

    /// <summary> 
    /// 해마의 전체 행동(경고 → 발사 → 정리)을 관리하는 메인 코루틴
    /// </summary>
    private IEnumerator FireRoutine()
    {
        // ── 1단계: 경고 표시 ──────────────────────────────────────────────
        ShowWarning();
        yield return new WaitForSeconds(initialDelay);
        HideWarning();
        
        // ── 2단계: 물대포 발사 ────────────────────────────────────────────
        if (waterCannonCollider) waterCannonCollider.SetActive(true);

        CreateBeamIfNeeded();
        if (waterBeamInstance != null)
        {
            waterBeamInstance.SetActive(true);
            uvScrollCo = StartCoroutine(UVScrollRoutine());
        }

        isFiring = true;
        PlayAttackSound();

        yield return new WaitForSeconds(fireDuration);
        
        // ── 3단계: 정리 및 오브젝트 제거 ─────────────────────────────────
        isFiring = false;
        StopAttackSound();

        if (uvScrollCo != null) { StopCoroutine(uvScrollCo); uvScrollCo = null; }
        if (waterBeamInstance) waterBeamInstance.SetActive(false);
        if (waterCannonCollider) waterCannonCollider.SetActive(false);

        Destroy(gameObject);
    }
    
    // =====================================================================
    // Water Beam
    // =====================================================================

    /// <summary>
    /// 물대포 이펙트 오브젝트가 없을 경우 새로 생성하고, 화면 너비에 맞게 스케일을 설정합니다.
    /// Orthographic 카메라를 기준으로 물줄기가 화면 전체 너비를 채우도록 계산됩니다.
    /// </summary>
    private void CreateBeamIfNeeded()
    {
        if (waterBeamInstance) return;

        if (!waterPrefab || !waterPoint)
        {
            Debug.LogWarning("[Seahorse] waterPrefab 또는 waterPoint가 설정되지 않았습니다.", this);
            return;
        }

        waterBeamInstance = Instantiate(waterPrefab, waterPoint.position, Quaternion.identity, waterPoint);
        waterBeamRenderer = waterBeamInstance.GetComponent<SpriteRenderer>();
        waterMPB = new MaterialPropertyBlock();
        
        // 카메라 뷰포트 기준 화면 월드 폭 계산 (Orthographic 카메라 가정)
        Vector3 leftEdge = Camera.main.ViewportToWorldPoint(new Vector3(0f, 0f, 0f));
        Vector3 rightEdge = Camera.main.ViewportToWorldPoint(new Vector3(1f, 0f, 0f));
        float worldWidth = Mathf.Abs(rightEdge.x - leftEdge.x);

        if (waterBeamRenderer && waterBeamRenderer.sprite)
        {
            float spriteW = waterBeamRenderer.sprite.bounds.size.x;
            float lengthX = worldWidth / spriteW;
            
            // 해마가 바라보는 방향에 따라 물대포 Y 스케일을 반전하여 방향을 맟춤
            // (+1: 오른쪽 발사, -1: 왼쪽 발사)
            float dir = Mathf.Sign(transform.localScale.x);

            waterBeamInstance.transform.localScale = new Vector3(lengthX, dir > 0 ? 1f : -1f, 1f);
            waterBeamInstance.transform.localPosition = Vector3.zero;
        }
    }

    /// <summary>
    /// 물대포 텍스처의 UV 오프셋을 매 프레임 이동시켜 물이 흐르는 효과를 구현합니다.
    /// MaterialPropertyBlock을 사용하여 머티리얼 인스턴스를 생성하지 않고 처리합니다.
    ///
    /// 흐름 방향은 해마가 바라보는 방향(localScale.x 부호)을 기준으로 결정됩니다.
    /// </summary>
    /// <returns></returns>
    private IEnumerator UVScrollRoutine()
    {
        if (waterBeamRenderer == null) yield break;

        float offset = 0f;
        
        // 해마가 바라보는 방향으로 텍스처가 흘러가도록 부호 결정
        float flowDir = Mathf.Sign(transform.localScale.x);

        while (true)
        {
            offset += Time.deltaTime * uvScrollSpeed * flowDir;

            waterBeamRenderer.GetPropertyBlock(waterMPB);
            waterMPB.SetVector("_MainTex_ST", new Vector4(1f, 1f, offset, 0f));
            waterBeamRenderer.SetPropertyBlock(waterMPB);

            yield return null;
        }
    }
    
    // =====================================================================
    // Collision
    // =====================================================================

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (!waterCannonCollider) return;
        if (!collision.gameObject.CompareTag("Player")) return;

        Rigidbody2D rb = collision.rigidbody;
        if (!rb) return;
        
        // 플레이어가 물대포 콜라이더 중심보다 위에 있으면 위로, 아래에 있으면 아래로 밀어내기
        Vector2 pushDir = collision.transform.position.y > waterCannonCollider.transform.position.y
            ? Vector2.up
            : Vector2.down;

        rb.AddForce(pushDir * pushForce, ForceMode2D.Force);
    }
    
    // =====================================================================
    // Warning UI
    // =====================================================================

    /// <summary>
    /// 물대포 발사 위치에 경고 UI를 생성하고 깜빡임 효과를 시작합니다.
    /// ScreenSpaceOverlay 모드의 UI 캔버스에만 생성됩니다.
    /// </summary>
    private void ShowWarning()
    {
        if (!warningUIPrefab || !waterPoint) return;

        var uiCanvasObj = GameObject.Find("UI_Canvas");
        if (!uiCanvasObj) return;

        var uiCanvas = uiCanvasObj.GetComponent<Canvas>();
        if (!uiCanvas || uiCanvas.renderMode != RenderMode.ScreenSpaceOverlay) return;

        warningInstance = Instantiate(warningUIPrefab, uiCanvas.transform);
        
        // 경고 UI의 위치를 waterPoint 월드 좌표 기준으로 변환하여 배치
        RectTransform rt = warningInstance.GetComponent<RectTransform>();
        float facingDir = Mathf.Sign(transform.localScale.x);
        Vector2 screenPos = Camera.main.WorldToScreenPoint(waterPoint.position);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            uiCanvas.transform as RectTransform, screenPos, null, out Vector2 localPoint);
        
        // 경고 UI의 너비 절반만큼 해바가 바라보는 방향으로 오프셋을 주어 물대포 방향에 맞춤
        float width = 1000f;
        localPoint.x += facingDir * (width * 0.5f);
        rt.anchoredPosition = localPoint;
        rt.sizeDelta = new Vector2(width, 100f);
        
        // CanvasGroup 캐싱 (HideWarning에서 알파 초기화에 재사용)
        warningCanvasGroup = warningInstance.GetComponent<CanvasGroup>()
                             ?? warningInstance.AddComponent<CanvasGroup>();
        
        // 깜빡임 효과 시작
        warnBlinkCo = StartCoroutine(BlinkRoutine(warningCanvasGroup, period: 1f, minAlpha: 0.3f));
    }

    /// <summary>
    /// 경고 UI 깜빡임을 정지하고, 알파를 0으로 즉시 초기화한 뒤 오브젝트를 제거합니다.
    /// 깜빡임 코루틴이 어느 타이밍에 중단되더라도 화면에 잔상이 남지 않도록 처리했습니다.
    /// </summary>
    private void HideWarning()
    {
        if (warnBlinkCo != null)
        {
            StopCoroutine(warnBlinkCo);
            warnBlinkCo = null;
        }
        
        // 코루틴 중단 시점에 알파가 어떤 값이든 0으로 즉시 초기화하여 잔상 방지
        if (warningCanvasGroup != null)
            warningCanvasGroup.alpha = 0f;

        if (warningInstance)
        {
            Destroy(warningInstance);
            warningInstance = null;
            warningCanvasGroup = null;
        }
    }

    /// <summary>
    /// CanvasGroup의 alpha를 period 주기로 maxAlpha ↔ minAlpha 사이에서 반복 변경하는 코루틴
    /// </summary>
    private IEnumerator BlinkRoutine(CanvasGroup cg, float period, float minAlpha)
    {
        var wait = new WaitForSeconds(period * 0.5f);
        while (true)
        {
            cg.alpha = 0.7f;
            yield return wait;
            cg.alpha = minAlpha;
            yield return wait;
        }
    }
    
    // =====================================================================
    // External System Interface
    // =====================================================================

    /// <summary>
    /// 물대포 공격 효과음을 재생합니다.
    ///
    /// [연동 지점] 실제 프로젝트에서는 AudioManager 등을 통해 효과음을 재생합니다.
    /// 예: attackSrc = AudioManager.Instance.PlaySfxReturn(sfxIndex, loop: false);
    /// </summary>
    private void PlayAttackSound()
    {
        // TODO: AudioManager.Instance.PlaySfxReturn(attackSfxIndex, loop: false);
    }

    /// <summary>
    /// 재생 중인 물대포 공격 효과음을 정지합니다.
    ///
    /// [연동 지점] 실제 프로젝트에서는 AudioSource를 정지합니다.
    /// 예: attackSrc?.Stop(); attackSrc = null;
    /// </summary>
    private void StopAttackSound()
    {
        // TODO: attackSrc?.Stop(); attackSrc = null;
    }
}
