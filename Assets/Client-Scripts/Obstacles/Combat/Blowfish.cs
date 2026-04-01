using System.Collections;
using UnityEngine;

/// <summary>
/// [복어(Blowfish) 방해 요소]
///
/// 동작 흐름:
///   1. 경고(Warning) : 목표 위치에 경고 아이콘을 먼저 표시하고, 복어 본체는 숨깁니다.
///                      Animation Event(OnWarningFinished)가 호출되면 다음 단계로 진행됩니다.
///   2. 이동(Move)    : 복어가 등장하여 목표 지점을 향해 헤엄칩니다.
///   3. 폭발(Explode) : 목표 도착 후 대기시간이 지나거나, 이동 중 플레이어와 충돌하면 폭발합니다.
///                      폭발 범위 내 플레이어가 있으면 피격 처리를 요청합니다.
///
/// [외부 시스템 연동 지점]
/// - GetCurrentPlayerLevel() : 실제 프로젝트에서는 GameManager 등으로부터 플레이어 레벨을 가져옵니다.
/// - OnPlayerHit()           : 실제 프로젝트에서는 PlayerController 등을 통해 사망 처리를 수행합니다.
/// - OnPlayerCollide()       : 실제 프로젝트에서는 PlayerController 등을 통해 둔화 효과를 적용합니다.
/// </summary>
[RequireComponent(typeof(Collider2D), typeof(SpriteRenderer), typeof(Rigidbody2D))]
public class Blowfish : MonoBehaviour
{
    // =====================================================================
    // Inspector Fields
    // =====================================================================

    [Header("Behavior Settings")] 
    [SerializeField] [Tooltip("복어가 헤엄치는 속도")]
    private float moveSpeed = 2f;

    [SerializeField] [Tooltip("목표 도착 후 폭발까지 대기하는 시간 (초)")]
    private float lifetime = 5f;

    [Header("Explosion Settings")] 
    [SerializeField] [Tooltip("충돌 후 폭발까지 걸리는 시간 (초) - 이 시간 동안 깜빡임 예고 효과가 재생됩니다.")]
    private float explosionDelay = 1.5f;

    [SerializeField] [Tooltip("기본 폭발 반경")] 
    private float explosionRadius = 2f;

    [SerializeField] [Tooltip("고레벨 도달 시 적용되는 폭발 반경")]
    private float highLevelExplosionRadius = 6f;

    [SerializeField] [Tooltip("고레벨 폭발 반경이 적용되기 시작하는 최소 플레이어 레벨")]
    private int highLevelThreshold = 9;

    [Header("Player Interaction Settings")] 
    [SerializeField] [Tooltip("플레이어와 충돌 시 적용되는 이동 속도 감소값")]
    private float slowSpeed = 2f;

    [SerializeField] [Tooltip("플레이어 이동 속도 감소가 유지되는 시간 (초)")]
    private float slowDuration = 3f;

    [SerializeField] [Tooltip("폭발 범위 내 플레이어를 감지하기 위한 레이어 마스크")]
    private LayerMask playerLayer;

    [Header("Effects")] 
    [SerializeField] [Tooltip("경고 아이콘을 포함하는 자식 오브젝트")]
    private GameObject warningFX;

    [SerializeField] [Tooltip("경고 아이콘 애니메이터 (애니메이션 종료 시 OnWarningFinished를 Animation Event로 호출")]
    private Animator warningAnimator;

    [SerializeField] [Tooltip("경고 중 복어 스프라이트를 숨길지 여부")]
    private bool hideDuringWarning = true;

    [SerializeField] [Tooltip("폭발 시 활성화될 이펙트 자식 오브젝트")]
    private GameObject explosionEffect;
    
    // =====================================================================
    // Private State
    // =====================================================================

    /// <summary>플레이어와 충돌이 발생했는지 여부</summary>
    private bool hasCollided = false;

    /// <summary>폭발 코루틴이 진행 중인지 여부 (중복 폭발 방지)</summary>
    private bool isExploding = false;

    /// <summary>경고 애니메이션이 완료되었는지 여부 (Animation Event로 설정됨)</summary>
    private bool warningDone = false;
    
    // =====================================================================
    // Component References
    // =====================================================================

    private Rigidbody2D rb;
    private Collider2D col2d;
    private SpriteRenderer spriteRenderer;
    private Camera mainCamera;
    
    // =====================================================================
    // Unity Lifecycle
    // =====================================================================

    private void Awake()
    {
        mainCamera = Camera.main;
        rb = GetComponent<Rigidbody2D>();
        col2d = GetComponent<Collider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        // 고레벨 달성 시 폭발 반경 확장 적용
        if (GetCurrentPlayerLevel() >= highLevelThreshold)
        {
            explosionRadius = highLevelExplosionRadius;
            Debug.Log($"[Blowfish] 플레이어 레벨 {highLevelThreshold} 이상: 폭발 범위 증가!");
        }

        StartCoroutine(BehaviorRoutine());
    }
    
    // =====================================================================
    // Behavior Coroutines
    // =====================================================================

    /// <summary>
    /// 복어의 전체 행동(경고 → 이동 → 폭발 대기)을 순서대로 관리하는 메인 코루틴
    /// </summary>
    private IEnumerator BehaviorRoutine()
    {
        // ── 1단계: 경고 표시 ──────────────────────────────────────────────
        // 화면 안쪽 랜덤 위치를 목표 위치로 설정
        Vector3 viewportTarget = new Vector3(
            Random.Range(0.2f, 0.8f),
            Random.Range(0.2f, 0.8f),
            -mainCamera.transform.position.z
        );
        Vector2 targetPosition = mainCamera.ViewportToWorldPoint(viewportTarget);

        if (warningFX != null)
        {
            // 경고 아이콘을 목표 위치에 먼저 표시
            warningFX.transform.position = targetPosition;
            warningFX.SetActive(true);
            if (warningAnimator) warningAnimator.Play(0, -1, 0f);
            
            // 경고 중 복어 본체 비활성화
            rb.simulated = false;
            col2d.enabled = false;
            if (hideDuringWarning) spriteRenderer.enabled = false;
            
            // OnWarningFinished()가 Animation Event로 호출될 때까지 대기
            yield return new WaitUntil(() => warningDone);

            warningFX.SetActive(false);
        }
        
        // ── 2단계: 복어 등장 및 이동 ─────────────────────────────────────
        spriteRenderer.enabled = true;
        col2d.enabled = true;
        rb.simulated = true;
        
        // 이동 방향에 따라 스프라이트 좌우 반전
        if (targetPosition.x < transform.position.x)
            spriteRenderer.flipX = true;
        
        // 목표 지점 도달 또는 플레이어 충돌 전까지 매 프레임 이동
        while (Vector2.Distance(transform.position, targetPosition) > 0.5f && !hasCollided)
        {
            Vector2 direction = (targetPosition - (Vector2)transform.position).normalized;
            rb.velocity = direction * moveSpeed;
            yield return null;
        }

        rb.velocity = Vector2.zero;
        
        // 이동 중 충돌이 발생했다면 코루틴 종료 (폭발은 OnCollisionEnter2D에서 처리)
        if (hasCollided) yield break;
        
        // ── 3단계: 목표 도달 후 대기 → 시간 초과 폭발 ───────────────────
        yield return new WaitForSeconds(lifetime);

        if (!isExploding)
        {
            Debug.Log("[Blowfish] 대기 시간 초과, 폭발!");
            StartCoroutine(ExplodeRoutine());
        }
    }

    /// <summary>
    /// 폭발 예고 깜빡임 효과 재생 후 실제 폭발 처리를 수행하는 코루틴
    /// </summary>
    private IEnumerator ExplodeRoutine()
    {
        if (isExploding) yield break;
        isExploding = true;
        
        // 물리 및 충돌 비활성화
        rb.isKinematic = true;
        rb.velocity = Vector2.zero;
        col2d.enabled = false;
        
        // 폭발 예고: explosionDelay 동안 스프라이트 깜빡임
        float timer = 0f;
        float blinkSpeed = 10f;
        Color origianlColor = spriteRenderer.color;

        while (timer < explosionDelay)
        {
            float alpha = Mathf.Lerp(0.3f, 1f, Mathf.PingPong(Time.time * blinkSpeed, 1f));
            spriteRenderer.color = new Color(origianlColor.r, origianlColor.g, origianlColor.b, alpha);
            timer += Time.deltaTime;
            yield return null;
        }
        
        Debug.Log("[Blowfish] 폭발!");
        
        // 폭발 이펙트 활성화
        // 복어 오브젝트와 부모-자식 관계를 끊어, 복어 파괴 후에도 이펙트가 독립적으로 재생되도록 합니다.
        if (explosionEffect != null)
        {
            explosionEffect.transform.SetParent(null);
            explosionEffect.SetActive(true);
            // TODO: AudioManager.Instance.PlaySfx(sfxIndex); 등으로 폭발 사운드 재생
        }

        spriteRenderer.enabled = false;
        
        // 폭발 범위 내 플레이어 감지 후 피격 처리
        Collider2D playerCollider = Physics2D.OverlapCircle(transform.position, explosionRadius, playerLayer);
        if (playerCollider != null)
            OnPlayerHit(playerCollider);
        
        // 이펙트 재생 후 오브젝트 파괴
        Invoke(nameof(DestroySelf), 1f);
    }
    
    // =====================================================================
    // Collision
    // =====================================================================

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // 이미 폭발 중이거나 충돌 대상이 플레이어가 아니면 무시
        if (isExploding || !collision.gameObject.CompareTag("Player")) return;

        hasCollided = true;
        StopAllCoroutines();
        
        // 플레이어 둔화 효과 적용
        OnPlayerCollide(collision);

        StartCoroutine(ExplodeRoutine());
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
    /// 폭발 범위 내 플레이어 피격 처리를 수행합니다.
    ///
    /// [연동 지점] 실제 프로젝트에서는 PlayerController.Die() 등을 호출합니다.
    /// 예: playerCollider.GetComponent<PlayerController>()?.Die();
    /// </summary>
    private void OnPlayerHit(Collider2D playerCollider)
    {
        // TODO: playerCollider.GetComponent<PlayerController>()?.Die();
        Debug.Log($"[Blowfish] 폭발 범위 내 플레이어 피격: {playerCollider.gameObject.name}");
    }

    /// <summary>
    /// 복어와 플레이어의 직접 충돌 시 플레이어 둔화 효과를 적용합니다.
    ///
    /// [연동 지점] 실제 프로젝트에서는 PlayerController.SlowDownForSeconds() 등을 호출합니다.
    /// 예: collision.gameObject.GetComponent<PlayerController>()?.SlowDownForSeconds(slowSpeed, slowDuration);
    /// </summary>
    private void OnPlayerCollide(Collision2D collision)
    {
        // TODO: collision.gameObject.GetComponent<PlayerController>()?.SlowDownForSeconds(slowSpeed, slowDuration);
        Debug.Log($"[Blowfish] 플레이어 충돌 - 둔화 효과 적용 예정 (속도: {slowSpeed}, 시간: {slowDuration}초)");
    }
    
    // =====================================================================
    // Animation Event Callback
    // =====================================================================

    /// <summary>
    /// 경고 애니메이션 클립의 마지막 프레임에서 Animation Event로 호출됩니다.
    /// 이 메서드가 호출되어야 BehaviorRoutine이 경고 단계를 완료하고 다음 단계로 진행합니다.
    /// </summary>
    public void OnWarningFinished()
    {
        warningDone = true;
    }
    
    // =====================================================================
    // Cleanup & Gizmos
    // =====================================================================

    private void DestroySelf()
    {
        Destroy(gameObject);
    }
    
    #if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
    #endif
}
