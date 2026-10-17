# VirtualQHDMirror v0.22 — Virtual Display Onboarding & Setup Plan

## Goal

v0.22는 v0.21.2의 안정된 미러링/입력 경험 위에 **가상 디스플레이 준비 과정**을 추가한다.

목표 UX:

1. VQM 실행
2. 현재 물리/가상 모니터 상태 자동 확인
3. 필요한 가상 디스플레이가 없거나 준비되지 않았으면 사용자에게 안내
4. 가능한 경우 VQM이 설치/복구/설정을 진행
5. 2560×1440 가상 화면 준비 완료 후 다시 topology 확인
6. 기존 v0.21.2 미러링 경로로 연결

## Stage A — Driver onboarding / guided setup

**바로 구현 가능한 단계.**

대상:

- 호환 가상 디스플레이 드라이버 설치 여부 자동 판정
- 드라이버 없음 / 비활성 / QHD 모드 없음 / 가상 모니터 수 부족 상태 구분
- 첫 실행 UI에서 현재 상태와 필요한 조치 표시
- `Install / Repair / Recheck` 형태의 명확한 사용자 흐름 제공
- 설치가 외부 패키지/수동 단계인 경우에도 정확한 설치 경로로 유도
- 설치/복구 후 자동 재탐지
- 기존 DriverManager의 monitor-count/config/restart 기능과 연결
- 사용자가 DISPLAY 번호, 레지스트리, CRU를 직접 다루지 않도록 함

완료 조건:

- 드라이버가 이미 있으면 별도 개입 없이 통과
- 드라이버가 없으면 왜 필요한지와 다음 행동을 VQM 안에서 알 수 있음
- 설치 후 VQM이 다시 검사하여 QHD virtual source 준비 여부를 판정
- 실패 시 원인과 복구 경로를 사용자에게 표시

## Stage B — VQM-managed automatic driver setup

**기술적으로 가능하지만 공개 자동 설치는 production driver package/signing 준비가 선행되어야 한다.**

대상:

- VQM 소유 IDD 패키지 설치
- UAC 승인 후 자동 설치
- 업그레이드 / repair / uninstall
- 필요한 가상 모니터 수 생성
- 2560×1440 @ 60 Hz 프로필 확인
- 필요 시 장치 restart/re-enumeration
- topology 안정화까지 기다린 뒤 자동 재탐지
- 실패 시 rollback 또는 안전한 복구 안내

공개 배포 전 조건:

- VQM-owned IDD source/package
- production INF/device identity
- Microsoft production signing
- install/upgrade/repair/uninstall 검증
- clean Windows 11 QA
- test-signing / Secure Boot 해제 / 수동 인증서 설치에 의존하지 않을 것

현재 ge9/IddSampleDriver 호환 경로는 개발/전환용으로만 유지하며, 공개 자동 설치 대상으로 승격하지 않는다.

## Implementation order

1. Driver state를 사용자용 상태 모델로 정리
2. 첫 실행/설정 UI에 Virtual Display Setup 영역 추가
3. Stage A guided install / repair / recheck 연결
4. 설치 후 topology convergence/re-scan 구현
5. 현재 compatibility driver에서 설정 보정 흐름 검증
6. VQM-owned signed driver가 준비되면 Stage B 자동 설치로 교체/확장
7. 이후 Auto start mirroring 및 tray UX와 연결

## Non-goals

v0.22에서 다음은 우선 범위에서 제외한다.

- 물리 모니터 EDID 자동 수정
- CRU 자동 설치/조작
- GraphicsDrivers 강제 삭제
- test-signing 활성화 안내
- Secure Boot 비활성화 요구
- Device Manager 수동 조작을 정상 UX로 채택

## Relationship to v0.21.2

v0.21.2는 그대로 안정 기준선으로 유지한다.

v0.22에서는 렌더링, Portal 입력, logical cursor topology, native drag bridge를 다시 설계하지 않는다. 가상 디스플레이의 **발견 → 준비 → 설치/복구 → 재검사** 계층만 확장한다.

## Next resume point

다음 작업은 **Stage A — Driver onboarding / guided setup**부터 시작한다.

첫 구현 목표:

- 현재 DriverManager 결과를 `Ready / Missing / Disabled / NeedsQhd / NeedsRestart / Error` 수준의 사용자용 상태로 정규화
- WPF UI에 Virtual Display Setup 카드 추가
- 상태별 안내와 `Recheck` / 가능한 경우 `Repair` 동작 연결

GitHub Actions/hosted CI는 사용하지 않는다. Windows/driver 검증은 로컬에서 진행한다.
