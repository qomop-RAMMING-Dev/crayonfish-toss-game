<img width="1932" height="828" alt="크레용피쉬1" src="https://github.com/user-attachments/assets/63f7133c-a4fe-4eaf-9358-7bf41c71755d" />

> 본 프로젝트는 **앱인토스(app-in-toss) 게임**으로 실제 출시된 **2D 하이퍼캐쥬얼 아케이드 게임** '크레용피쉬'의 클라이언트 및 웹 통합 모듈을 포트폴리오 용으로 리팩토링한 결과물 입니다. 단기 인턴십 과정에서 담당했던 **방해 요소 스폰 시스템**과 **WebGL Safe Area 대응** 로직을 포함하고 있습니다.

## 📌 Project Overview
<div align="center">
  <table>
    <tr>
      <th>개발 기간</th><td align="center">2025.06.30 ~ 2025.07.29 (1개월)</td>
    </tr>
    <tr>
      <th>개발 인원</th><td align="center">4명 (클라이언트 4)</td>
    </tr>
    <tr>
      <th>담당 역할</th><td align="center">Main Developer (방해 요소 시스템, WebGL 플랫폼 대응)</td>
    </tr>    
    <tr>
      <th>기술 스택</th>
      <td align="center">
        <img src="https://img.shields.io/badge/Unity-2022.3.60f1-black?style=flat-square&logo=unity&logoColor=white">
        <img src="https://img.shields.io/badge/C%23-%23178600?style=flat-square&logo=csharp&logoColor=white">
        <img src="https://img.shields.io/badge/JavaScript-F7DF1E?style=flat-square&logo=javascript&logoColor=black">
      </td>
    </tr>    
    <tr>
      <th>타겟 플랫폼</th><td align="center">WebGL (Mobile Web Browser)</td>
    </tr>    
    <tr>
      <th>관련 페이지</th><td align="center"><a href="https://cham-qomop.itch.io/crayonfish">Itch.io에서 크레용피쉬 데모판 체험하기</a></td>
    </tr>
  </table>
</div>

## 🚀 Key Contributions
### 확률 가중치 기반 방해 요소 스폰 시스템
플레이어의 성장 속도에 맞춰 게임의 긴장감을 유지하기 위해 **레벨 데이터 기반 스폰 시스템**을 설계했습니다.
- **동적 난이도 조절:** `ObstacleSpawnLevelData`를 통해 레벨별 등장 확률(Weight)과 생성 주기(Interval)를 유연하게 관리합니다.
- **시스템 흐름도:**

### 상태 머신 기반 방해 요소 4종 로직
각 방해 요소는 독립적인 상태 머신을 가지며, 플레이어와의 다양한 인터랙션을 제공합니다.


## 📂 Project Structure (My Works)
본 리포지토리는 프로젝트 전체 코드 중 **직접 설계하고 구현한 핵심 시스템** 위주로 구성되어 있습니다.
```Plaintext
Assets/
 ├── Assets-Showcase/
 ├── Client-Scripts/
 │    ├── Managers/         
 │    │    └── ObstaclesManager.cs # 확률 기반 스폰 및 플레이어 레벨 데이터 관리
 │    └── Obstacles/        
 │         ├── Combat/
 │         │    ├── Blowfish.cs # 복어: 상태 머신 기반(경고/이동/폭발) 로직
 │         │    ├── Seahorse.cs # 해마: 셰이더 연동 및 물대포 발사 로직
 │         │    ├── Octopus.cs  # 문어: 탭 인터랙션 기반 먹물 발사 제어
 │         │    └── Fisher.cs   # 낚시꾼: 플레이어 레벨 연동형 낚싯줄 길이 제어
 │         └── Patterns/
 │              ├── Watercannon.cs # 물대포: 플레이어 밀치기 물리 처리 및 영역 제어
 │              └── InkTrap.cs     # 먹물: 화면 가림 UI 및 점진적 시야 회복 인터랙션
 └── Server-Scripts/
      ├── Managers/
      │    └── UnitySafeAreaController.cs # 동적 뷰포트 앵커 최적화 컨트롤러
      ├── WebBridge/
      │    └── NativeSafeAreaBridge.jslib
      └── Web-Frontend/
           ├── index.html             # WebGL 빌드 메인 페이지 및 캔버스 설정
           └── TossFrameworkBridge.js # Toss 프레임워크 API 연동 및 데이터 전송
```

## 💡 Technical Challenges
