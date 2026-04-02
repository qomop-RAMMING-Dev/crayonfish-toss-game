/**
 * [NativeSafeAreaBridge.jslib]
 * 
 * Unity WebGL 빌드에 포함되는 네이티브 플러그인 파일입니다.
 * C# 코드(UnitySafeAreaController.cs)에서 DllImport로 선언한 함수를 이 파일에서 구현합니다.
 *
 * 역할:
 * - C#에서 전달된 포인터 타입 문자열을 JavaScript 문자열로 변환합니다.
 * - TossFrameworkBridge.js에 정의된 전역 함수(window.requestTossSafeArea)를 호출합니다.
 * - 전역 함수가 없을 경우(로드 실패 등) 기본값을 Unity로 즉시 전송하여 앱이 멈추지 않도록 합니다.
 *
 * 호출 이름:
 * UnitySafeAreaController.cs (C#) → RequestSafeAreaInsets() [해당 파일]
 * → window.requestTossSafeArea() [TossFrameworkBridge.js] → unityInstance.SendMessage()
 * → UnitySafeAreaController.OnSafeAreaReceived() (C#)
 */
mergeInto(LibraryManager.library, {
    
    /**
     * C#의 DllImport와 연결되는 함수
     * @param {number} gameObjectName - C# string 포인터 (Unity 내부 포맷)
     * @param {number} methodName     - C# string 포인터 (Unity 내부 포맷)
     */
    RequestSafeAreaInsets: function(gameObjectName, methodName)
    {
        // Unity 내부 포맷의 문자열 포인터를 JavaScript 문자열로 변환
        const objectName = UTF8ToString(gameObjectName);
        const method = UTF8ToString(methodName);
        
        if (window.requestTossSafeArea)
        {
            // TossFrameworkBridge.js의 전역 함수 호출
            window.requestTossSafeArea(objectName, method);
        }
        else
        {
            // TossFrameworkBridge.js가 로드되지 않았거나 함수가 정의되지 않은 경우
            console.error("[NativeSafeAreaBridge] window.requestTossSafeArea 함수를 찾을 수 없습니다. TossFrameworkBridge.js 로드 여부를 확인하세요.");
            
            // Fallback: 기본값(0)을 즉시 Unity로 전송하여 앱이 멈추지 않도록 처리
            if (window.unityInstance)
            {
                const fallbackPayload = JSON.stringify({ top: 0, bottom: 0, left: 0, right: 0 });
                window.unityInstance.SendMessage(objectName.method, fallbackPayload);
            }
        }
    },
});
