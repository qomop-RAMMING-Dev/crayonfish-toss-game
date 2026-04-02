/**
 * [TossFrameworkBidge.js]
 * 
 * Toss 웹 프레임워크 API와 Unity WebGL 인스턴스 사이의 브릿지 역할을 담당하는 스크립트입니다.
 * index.html에서 <script type="module">으로 로드됩니다.
 * 
 * 제공하는 전역 함수:
 * - window.requestTossSafeArea(gameObjectName, methodName)
 *   Toss 프레임워크의 getSafeAreaInsets()를 호출하여 Safe Area 값을 수집하고
 *   Unity의 SendMessage로 C# 콜백(OnSafeAreaReceived)에 전달합니다.
 *   
 * - window.openLeaderBoard()
 *   Toss 프레임워크의 게임센터 리더보드 화면을 엽니다.
 * 
 * - window.submitScore(score)
 *   Toss 프레임워크의 게임센터 리더보드에 점수를 제출합니다.
 * 
 * 지연 처리 이유:
 * Unity가 로드된 직후에는 브라우저가 CSS를 아직 완전히 적용하지 않아 캔버스 크기가 확정되지 않은 상태일 수 있습니다.
 * setTimeout(500ms)으로 대기한 후 캔버스 크기를 측정하여 정확한 뷰포트 기반 Safe Area 값을 계산합니다.
 * 
 * Unity 인스턴스 접근:
 * index.html에서 createUnityInstance() 완료 후 window.unityInstance에 할당된 인스턴스를 통해 SendMessage를 호출합니다.
 * 인스턴스가 아직 준비되지 않은 경우 100ms 간격으로 재시도합니다.
 */

import { getSafeAreaInsets } from '@apps-in-toss/web-framework';

// =====================================================================
// Safe Area
// =====================================================================

/**
 * NativeSafeAreaBridge.jslib에서 호출되는 전역 함수
 * Toss 프레임워크의 Safe Area 값을 수집하여 Unty C#으로 전달합니다.
 * 
 * @param gameObjectName - 결과를 받을 Unity GameObject 이름
 * @param methodName     - 결과를 받을 C# 메서드 이름 (OnSafeAreaReceived)
 */
window.requestTossSafeArea = function(gameObjectName, methodName) 
{
    // 브라우저가 CSS 적용 및 캔버스 크기 확정을 완료할 때까지 500ms 대기
    setTimeout(() =>
    {
        // Unity로 전달할 Safe Area 데이터 초기화
        let payload = {
            top: 0, bottom: 0, left: 0, right: 0,
            canvasWidth: 0, canvasHeight: 0
        };
        
        // Toss 프레임워크에서 Safe Area inset 값 수집
        try
        {
            console.log("[TossFrameworkBridge] Toss 웹 프레임워크에서 Safe Area 값 요청");
            const insets = getSafeAreaInsets();
            payload.top = insets.top;
            payload.bottom = insets.bottom;
            payload.left = insets.left;
            payload.right = insets.right;
        }
        catch (e)
        {
            console.error("[TossFrameworkBridge] getSafeAreaInsets 호출 중 오류 발생:", e);
        }
        
        // Unity 캔버스의 실제 렌더링 크기 측정
        // clientWidth/clientHeight는 CSS 적용 후의 최종 크기를 반환합니다.
        const canvas = document.querySelector("#unity-canvas");
        if (canvas)
        {
            payload.canvasWidth = canvas.clientWidth;
            payload.canvasHeight = canvas.clientHeight;
        }
        else
        {
            console.error("[TossFrameworkBridge] Unity 캔버스(#unity-canvas)를 찾을 수 없습니다.");
        }
        
        const payloadJson = JSON.stringify(payload);
        
        // Unity 인스턴스가 준비될 때까지 100ms 간격으로 재시도 후 SendMessage 호출
        const interval = setInterval(() =>
        {
            if (window.unityInstance)
            {
                clearInterval(interval);
                console.log(`[TossFrameworkBridge] Unity로 Safe Area 데이터 전송: ${payloadJson}`);
                window.unityInstance.SendMessage(gameObjectName, methodName, payloadJson);
            }
            else
            {
                console.warn("[TossFrameworkBridge] Unity 인스턴스가 아직 준비되지 않았습니다. 재시도 중...");
            }
        }, 100);
    }, 500);
};
