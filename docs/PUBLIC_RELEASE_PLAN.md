# VirtualQHDMirror 공개 배포 전환 계획

기준 버전: **v0.14.1 stable prototype / v0.20 public-release migration**

현재 공개화 작업 브랜치: **`gpt/public-release-v0.20`**

진행 상태:

- ✅ v0.14.1 runnable fallback 보존 (`archive/v0.14.1/`)
- ✅ v0.20 .NET 8/WPF solution bootstrap
- ✅ read-only **System Preflight + `QueryDisplayConfig` 기반 DisplayDiscovery** — 실제 IDD/물리 모니터 식별 및 v0.14.1 mirror handoff 검증 완료
- 🟡 **DriverManager + elevated compatibility helper** — 설치/활성/config/QHD mode, 전체 1:1 coverage 계산, 안전한 config 변경/backup/정확한 IDD restart 경로 구현; 실제 다중 모니터 privileged apply 검증 대기
- ✅ v0.14.1 compatibility session 재탐지/Stop 제어 — v0.20 창을 닫아도 mirror 유지, 재실행 시 단일 기존 session 재부착
- ✅ **public driver strategy fixed** — Microsoft official IddSample 기반 VQM-owned IDD, ge9는 development compatibility only
- ✅ **public install/upgrade/repair/uninstall contract** 문서화
- 🟡 **v0.14.1 GDI renderer .NET backend 이식** — CreateDC/GetDC/HALFTONE StretchBlt/render timing 분리 완료, native session 연결 전
- ⏭ 다음 구현: Portal/native input을 .NET session으로 모듈 이식

이 문서는 현재의 PowerShell + 동적 C# PoC를 일반 사용자가 설치하고 사용할 수 있는 Windows 애플리케이션으로 끌어올리기 위한 작업 계획이다.

---

## 1. 현재 기준선

v0.13.1에서 확보된 기능:

- 2560×1440 가상 디스플레이를 1920×1080 물리 모니터에 축소 표시
- 2번 물리 화면에서 3번 가상 화면으로 마우스 입력 전달
- 기본 클릭 / 우클릭 / 가운데 클릭
- 휠
- XButton1 / XButton2
- 일반 드래그
- 제목 표시줄 드래그
- 제목 표시줄 더블클릭 최대화/복원
- 작업 표시줄 클릭 보조
- 표준 최소화 / 최대화·복원 / 닫기 버튼의 non-client hit-test + system command 처리
- Unity / Electron 등 일부 커스텀 타이틀바용 실제 입력 주입 fallback
- DWM Peek에서 mirror window 제외
- virtual-desktop absolute SendInput 기반 native pointer fallback
- Per-Monitor DPI Aware V2
- adaptive 60 / 30 / 15 FPS
- 출력 DC 재사용
- 진단 오버레이
- 가상 화면에 실제 커서가 이동했을 때 미러 화면에 커서 합성

현재 핵심 한계:

- 디스플레이 탐지가 해상도 기준에 크게 의존
- IDD 가상 디스플레이를 사용자가 별도 설치/설정해야 함
- PowerShell에서 C#을 런타임 컴파일
- GDI `StretchBlt` 기반 렌더링
- 앱별 입력 호환성이 완전히 일반화되어 있지 않음
- 커스텀 커서 동기화 미완성
- 설치 프로그램 / 자동 업데이트 / 트레이 UI 없음
- 드라이버 설치, 복구, 제거 lifecycle 없음
- 일반 사용자 대상 오류 메시지와 진단 수집 부족

---

## 2. 공개판 목표 UX

최종적으로는 다음 흐름을 목표로 한다.

### 첫 실행

1. 설치 프로그램 실행
2. 필요한 경우 관리자 권한 요청
3. 가상 디스플레이 드라이버 상태 확인
4. 드라이버가 없으면 설치 안내 또는 자동 설치
5. 사용 가능한 물리 모니터 자동 탐지
6. 가상 QHD 화면 생성 및 2560×1440 설정
7. 적합한 물리 FHD 모니터 자동 선택
8. 미러링 시작

### 이후 실행

- 트레이에서 자동 실행
- 마지막으로 사용한 가상/물리 모니터 기억
- 모니터가 바뀌어도 재탐지
- 필요한 경우 사용자에게 대상 모니터 선택 UI 제공
- Windows 시작 시 자동 시작 옵션
- 종료 시 원래 디스플레이 상태 안전 복구

목표는 **설치 후 사용자가 CRU, 레지스트리, Windows 디스플레이 설정, 수동 display number 확인을 하지 않아도 되는 상태**다.

---

## 3. 권장 프로젝트 구조

과거 PowerShell 실행본은 `archive/`에 고정하고, .NET 기반 정식 앱을 `src/`에서 개발한다.

```text
VirtualQHDMirror/
├─ src/
│  ├─ VirtualQHDMirror.App/
│  │  ├─ App.xaml
│  │  ├─ MainWindow.xaml
│  │  └─ Tray/
│  ├─ VirtualQHDMirror.Core/
│  │  ├─ Displays/
│  │  ├─ Capture/
│  │  ├─ Presentation/
│  │  ├─ Input/
│  │  ├─ Cursor/
│  │  ├─ Configuration/
│  │  └─ Diagnostics/
│  ├─ VirtualQHDMirror.DriverManager/
│  └─ VirtualQHDMirror.Tests/
├─ driver/
│  └─ VirtualDisplay/
├─ installer/
├─ archive/
│  ├─ v0.12/
│  ├─ v0.13.1/
│  └─ v0.14.1/
├─ docs/
├─ run.cmd
├─ run-v020.cmd
├─ .github/
│  └─ workflows/         # release-only automation when explicitly approved
├─ LICENSE
└─ README.md
```

권장 기술:

- .NET 8 또는 당시 지원 중인 LTS
- C#
- WinUI 3 또는 WPF
- P/Invoke는 Core 계층에 격리
- 렌더링 경로는 D3D11 기반으로 분리

---

## 4. 단계별 개발 계획

## Phase A — 동작 기준선 보존

목적: 현재 동작하는 프로토타입을 잃지 않고 정식 앱 전환의 비교 기준으로 사용.

상태: **보존 작업 완료, 회귀 검증은 지속 진행**

작업:

- `archive/v0.12/` historical baseline 유지
- `archive/v0.13.1/` 이전 안정판 유지
- `archive/v0.14.1/` 현재 runnable fallback 유지
- 사용 중인 단축키, 입력 동작, 렌더 모드 문서화
- 알려진 문제 목록 작성
- 테스트 환경 기록
- .NET 이식 단계별 smoke/regression check 추가

완료 조건:

- 새 구현에서 기능 회귀가 발생하면 v0.12와 즉시 비교 가능
- historical PowerShell baselines are preserved only under `archive/`

---

## Phase B — System Preflight + Display Discovery 재작성

현재의 “첫 번째 2560×1440 / 첫 번째 1920×1080” 선택을 제거한다.

이 단계는 기존 미러링 코어를 변경하기 전에 **읽기 전용 사전 점검(preflight)** 계층을 만든다.

Preflight가 먼저 확인할 것:

- Windows / 디스플레이 topology 읽기
- virtual display adapter/monitor 존재 여부
- 필요한 QHD source mode 사용 가능 여부
- 물리 destination 후보
- source/destination 자동 선택 가능 여부
- 모호하거나 잘못된 구성에 대한 사용자용 상태 메시지
- 드라이버가 없거나 비활성일 때 다음 설치 안내로 연결할 수 있는 상태 코드

이 단계에서는 드라이버 설치, 레지스트리 수정, EDID 수정 등 시스템 변경을 수행하지 않는다.

사용할 정보:

- `QueryDisplayConfig`
- `DisplayConfigGetDeviceInfo`
- source / target adapter LUID
- source id / target id
- output technology
- target friendly name
- monitor device path
- active mode
- desktop coordinates
- connector type
- primary 여부
- virtual / physical 후보 구분

자동 선택 정책 예시:

### Source 후보

1. IDD / virtual adapter
2. 2560×1440 지원
3. 물리 출력 connector가 아닌 대상 우선
4. 제품이 설치한 virtual display device path 우선

### Destination 후보

1. 실제 물리 모니터
2. 가상 디스플레이 제외
3. 현재 공개판 기본 profile에서는 1920×1080 물리 화면
4. 내장/외장은 후보 제외 기준이 아니라 placement/preference metadata로만 사용
5. 마지막 사용 monitor device path / persistent identity 일치 우선

### Topology 판정

단일 모니터 노트북을 가정하지 않는다.

- 물리 FHD 1 + 가상 QHD 1: 단일 pair 자동 추천 가능
- 물리 FHD 2+ + 가상 QHD 1: 자동 pairing 금지; 단일 대상 선택 / 동일 source 복제 / 추가 virtual source 생성 중 정책 필요
- 물리 FHD 1 + 가상 QHD 2+: source 선택 필요
- 물리 FHD 2+ + 가상 QHD 2+: 최대 `min(physical, virtual)`개의 독립 1:1 session 구성 가능
- 모든 물리 FHD를 독립적인 QHD 작업공간으로 쓰려면 물리 target 수만큼 virtual QHD source가 필요

여러 후보가 있으면 임의 순번으로 선택하지 않고 전체 후보와 topology 상태를 반환하며, 이후 UI/저장 설정에서 pair를 결정한다.

완료 조건:

- DISPLAY1 / DISPLAY2 / DISPLAY5처럼 번호가 바뀌어도 정상 동작
- 재부팅 후에도 같은 물리 모니터를 다시 찾음
- 듀얼/트리플 외부 모니터에서도 오탐지하지 않음

---

## Phase C — 정식 Capture / Presentation 엔진

GDI `StretchBlt`는 프로토타입 fallback으로 남기고 GPU 경로를 기본으로 만든다.

우선 검토:

1. D3D11 Desktop Duplication
2. Windows Graphics Capture
3. 필요 시 DXGI swap chain 기반 출력

목표:

- CPU 점유율 감소
- 장시간 실행 시 안정적인 메모리 사용
- 60 FPS 대응
- resize / display reconnect 대응
- display loss / device removed 복구
- tearing 최소화
- 불필요한 full-frame CPU copy 제거

렌더 모드:

- Adaptive
- 30 FPS
- 60 FPS
- 향후 Uncapped 옵션은 필요성이 검증될 때만 추가

Adaptive 정책은 v0.12의 60/30/15 FPS 아이디어를 유지하되 GPU 기반 이벤트/dirty region 최적화를 추가한다.

완료 조건:

- 8시간 soak test에서 메모리 지속 증가 없음
- 화면 정지/재개 반복 후 정상 복구
- monitor hot-plug 후 앱 재시작 없이 복구 가능
- FHD destination에서 QHD source가 안정적으로 축소 표시

---

## Phase D — 입력 엔진 정식화

현재 입력 경로를 명확히 3단계로 나눈다.

### 1. Standard client input

- 일반 mouse move
- click
- double click
- wheel
- XButton
- drag

### 2. Standard non-client input

- caption drag
- minimize
- maximize
- close
- resize border
- system menu

### 3. Compatibility fallback

- Unity
- Electron
- Chromium custom chrome
- 자체 title bar
- shell/taskbar

가능하면 임의의 화면 위치를 고정 크기 영역으로 추측하는 방식보다 실제 HWND / hit-test / accessibility / real SendInput fallback 순으로 처리한다.

주의:

- UAC secure desktop은 지원 대상에서 제외 가능
- 관리자 권한 앱과 일반 권한 앱 간 입력 제한을 명확히 문서화
- 게임 anti-cheat 환경은 기본 지원 범위에서 제외

완료 조건:

- Explorer
- Chrome / Edge
- VS Code / Cursor
- Unity Editor
- 일반 Win32
- WPF
- Electron
- Windows taskbar

에서 기본 입력 회귀 테스트 통과

---

## Phase E — 커서 엔진

별도 모듈로 분리한다.

지원 목표:

- Arrow
- I-beam
- Hand
- horizontal resize
- vertical resize
- diagonal resize
- busy / wait
- 앱에서 제공하는 custom cursor

구현 방향:

- 실제 source cursor handle 추적
- cursor hotspot 보존
- scaling 시 hotspot 보정
- software cursor 합성
- 동일 cursor handle 캐싱
- 매 프레임 불필요한 GDI object 생성 금지

완료 조건:

- source와 destination에서 cursor type / hotspot이 시각적으로 일치
- 장시간 사용 시 GDI handle 누수 없음

---

## Phase F — Virtual Display Driver

공개 배포의 가장 중요한 외부 의존성이며, **구현 순서상 Phase B 직후 전략을 확정해야 하는 P0 gate**다.

상세 온보딩 계획은 `docs/DRIVER_ONBOARDING_PLAN.md`를 따른다.

결정해야 할 사항:

### 선택지 A — 기존 오픈소스 IDD 사용

장점:

- 개발 비용 감소
- 이미 검증된 구현 활용 가능

확인 필요:

- 라이선스
- 재배포 허용 여부
- 수정본 배포 조건
- 서명 상태
- 자동 설치 허용 여부
- 원하는 해상도 설정 API 제공 여부

### 선택지 B — 자체 IDD 구축

장점:

- 제품에 최적화 가능
- device identity와 lifecycle 완전 제어
- 설치/업데이트 통합 용이

단점:

- 드라이버 개발 / 테스트 비용
- 서명 비용
- Windows 버전별 QA 부담 증가

### 서명

일반 사용자에게 test-signing 강요 금지.

공개 Release 조건:

- 정식 서명 드라이버
- 설치 시 UAC 한 번으로 완료
- 제거 가능
- 업그레이드 가능
- 재부팅 요구 여부 명확화

완료 조건:

- 새 PC에서 수동 장치 관리자 조작 없이 설치
- uninstall 시 깨끗하게 제거
- 재설치 가능
- Windows Update / 재부팅 이후에도 정상

---

## Phase G — Driver / Display 자동 설정

DriverManager 책임:

- 드라이버 설치 상태 확인
- virtual adapter 존재 확인
- virtual monitor 활성 상태 확인
- 필요한 target resolution 생성/설정
- 실패 원인 사용자 메시지 변환
- 복구 / 제거

DisplayManager 책임:

- source / destination 자동 탐지
- configuration validation
- 마지막 구성 저장
- display topology 변경 감지
- source disconnect 시 자동 대기
- reconnect 시 자동 복구

안전 원칙:

- 물리 모니터 EDID override 자동 수정 금지
- CRU 자동 설치/자동 수정 금지
- GraphicsDrivers registry 강제 삭제 금지
- 사용자의 기존 display topology를 임의로 파괴하지 않음

---

## Phase H — 사용자 UI

최소 UI:

```text
VirtualQHDMirror

Virtual source
  Virtual Display — 2560 × 1440

Physical output
  PLX-MNT27VJWH — 1920 × 1080

Render
  Adaptive

[ Start Mirror ]  [ Stop ]

[ ] Start with Windows
[ ] Auto start mirroring
[ ] Show diagnostics
```

추가:

- tray icon
- source selector
- destination selector
- FPS selector
- input enable toggle
- driver status
- diagnostics export
- reset configuration

설정 저장:

- JSON
- 사용자 AppData 아래
- device path / persistent identifier 저장
- DISPLAY 번호는 저장하지 않음

---

## Phase I — Installer

권장 후보:

- WiX Toolset
- MSIX는 driver 배포 요구사항 검토 후 결정

Installer 책임:

- .NET runtime 정책 결정
- app install
- driver install
- Start Menu shortcut
- uninstall
- optional startup
- upgrade
- rollback

금지:

- SmartScreen 회피 목적의 임의 우회 안내
- unsigned driver 자동 강제 설치
- 보안 기능 비활성화 요구

---

## Phase J — 로그와 복구

로그 항목:

- app version
- Windows version
- display topology
- adapter / target ID
- source / destination 선택 근거
- rendering backend
- FPS mode
- device lost
- input fallback
- driver state

개인정보 원칙:

- 기본은 로컬 로그
- 자동 업로드 금지
- 사용자가 직접 “진단 로그 내보내기”를 선택할 때만 파일 생성
- 창 제목, 키 입력, 사용자 콘텐츠는 로그에 저장하지 않음

복구:

- capture device lost
- destination disconnected
- source disconnected
- sleep / resume
- display mode change
- Explorer restart

---

## Phase K — 테스트 계획

필수 QA matrix:

### OS

- Windows 11 현재 지원 버전
- 가능하면 Windows 10 지원 여부 별도 결정

### GPU

- Intel
- NVIDIA
- AMD

### 물리 연결

- HDMI
- DisplayPort
- USB-C DP Alt Mode

### destination

- 1920×1080 60 Hz
- 1920×1080 고주사율
- 다른 FHD scaling 설정

### source

- 2560×1440
- 향후 2560×1600 / 3440×1440 등은 별도 feature로 취급

### 앱 입력

- Explorer
- Chrome / Edge
- Cursor / VS Code
- Unity
- 일반 Win32 앱
- Electron 앱

### 장시간

- 1시간
- 8시간
- overnight

측정:

- CPU
- GPU
- private working set
- handles
- GDI objects
- frame latency
- dropped frames

---

## Phase L — GitHub / Release Automation

기본 원칙은 `docs/REPOSITORY_OPERATION_RULES.md`를 따른다.

개발 중에는 GitHub Actions를 기본 CI로 사용하지 않는다.

- restore / build / test / static validation은 로컬 우선
- 일반 commit/push마다 workflow를 실행하지 않음
- major checkpoint는 원격 push/ref update 1회 제한
- remote artifact upload는 일반 개발 체크포인트에서 사용하지 않음
- GitHub Actions가 필요해지는 시점은 release/signing/package 등 원격 자동화의 이점이 명확할 때로 제한
- 도입 시에도 `workflow_dispatch`, release/tag 등 좁은 trigger 우선

공개 release 단계에서 필요할 수 있는 자동화:

- version tag
- signed app/package
- approved signed driver package
- SHA-256 checksum
- release notes

branch 정책은 공개 beta 시점에 다시 확정한다. 프로토타입/초기 migration 단계에서는 과도한 branch protection과 CI 소비를 만들지 않는다.

---

## 5. 버전 제안

### v0.12

동작 비교용으로 고정된 초기 PowerShell PoC baseline

### v0.13.1

- `archive/v0.13.1/` historical baseline
- DWM Peek 격리
- 표준 caption button 직접 처리
- custom chrome / shell용 virtual-desktop absolute SendInput fallback
- cursor hotspot 보정

### v0.14

- Portal input을 기본 경로로 사용
- 1 | 2 | 3 모니터 topology와 직접 이동 유지
- #2 진입 시 #3의 대응 좌표에서 Windows native input 사용
- 수동 capture/release hotkey 제거
- source 경계 제약은 portal 내부에서만 임시 사용하고 #2 경계 이동 시 자동 해제
- 작업표시줄/hover/title-bar/custom chrome을 앱별 메시지 에뮬레이션 대신 native input으로 처리
- MagSetInputTransform/UIAccess는 일반 배포 기본 의존성으로 채택하지 않음
- captured/legacy 경로는 회귀 비교용 fallback으로 유지
- v0.13.1 실행본은 archive/v0.13.1에 보존

### v0.14.1

- Portal 입력 구조 유지
- full-frame GDI 렌더와 software cursor 렌더 분리
- cursor 전용 click-through overlay 실험은 Windows 커서 소실/컴파일 회귀로 제거
- 안정적인 frame-synced cursor composition을 기본 경로로 유지
- 전체 렌더 Adaptive / 30 / 60 / 실험적 120 FPS 지원
- 1 ms timer resolution 요청으로 prototype frame pacing 개선
- diagnostic overlay에 render cost / cursor path 표시
- WinForms startup loader + GUI error dialog

### v0.20

- .NET app skeleton
- QueryDisplayConfig 기반 탐지
- GDI backend 이식
- 기존 입력 기능 이식

### v0.30

- D3D11 / Windows Graphics Capture backend
- cursor engine
- reconnect / recovery

### v0.40

- virtual driver manager
- configuration UI
- tray
- startup

### v0.50-alpha

- installer
- signed app
- limited external test

### v0.70-beta

- signed driver
- fresh-install flow
- multi-GPU QA
- soak test

### v1.0

- 일반 사용자 설치 가능
- 자동 source/destination 탐지
- driver lifecycle 지원
- 안정적 GPU rendering
- 문서/진단/제거 경로 완성

---

## 6. 공개 전에 반드시 해결해야 하는 P0

1. 정식 virtual display driver 전략 확정
2. driver licensing / redistribution 확인
3. driver signing 확보
4. resolution-only display selection 제거
5. GPU-native rendering 또는 충분히 검증된 fallback 확보
6. display reconnect / sleep resume 복구
7. installer + uninstall
8. fresh PC install QA
9. 장시간 soak test
10. 위험한 registry / EDID 수정에 의존하지 않는 설치 흐름

---

## 7. 공개 전 P1

- tray UX
- startup
- custom cursor
- Unity / Electron compatibility table
- diagnostics export
- automatic update
- localization
- Windows 10 지원 여부

---

## 8. 공개 전 P2

- 다양한 source resolution
- aspect ratio presets
- sharpen / scaling filter 선택
- keyboard remapping
- multiple mirror profiles
- per-app compatibility rules

---

## 9. 당장 다음 작업 순서

현재 기준에서 가장 효율적인 순서:

1. ✅ v0.14.1 runnable fallback 보존 + v0.20 .NET solution 생성
2. ✅ **read-only System Preflight + QueryDisplayConfig 기반 DisplayDiscovery** — 현재 개발 PC 실동작 검증 완료
3. 🟡 **DriverManager + elevated compatibility helper** — 개발용 ge9 compatibility lifecycle 지원; 실제 다중 monitor privileged apply 검증 대기
4. ✅ **public virtual display driver 전략 확정** — Microsoft official IddSample → VQM-owned production IDD, MS-PL source lineage, HLK/Hardware Dev Center production signing target
5. ✅ **공개용 driver install/upgrade/repair/uninstall 계약 작성**
6. 🟡 **v0.14.1 GDI renderer를 정식 app 내부 backend로 이식** — Core.Rendering 구현 완료, native session 연결 대기
7. 🔄 **v0.14.1 Portal/native input forwarding을 모듈화해서 이식**
8. 설정 저장 + source/destination 선택 UI + 검증된 driver install integration
9. D3D11 / Windows Graphics Capture backend + reconnect/recovery
10. tray / startup / diagnostics + installer/uninstall/rollback
11. fresh-PC external alpha

핵심 원칙은 **현재 동작하는 v0.14.1 코어를 유지한 채, 일반 사용자가 설치하고 올바른 디스플레이를 선택할 수 있게 만드는 외곽 계층부터 추가하는 것**이다. 공개화 migration이 기능 parity에 도달하기 전까지 기존 PowerShell 실행 경로를 제거하지 않는다.
