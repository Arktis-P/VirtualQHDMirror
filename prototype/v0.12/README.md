# VirtualQHDMirror v0.12 prototype

v0.11 기반 최적화를 유지하면서 Unity/Electron 계열의 커스텀 타이틀바 fallback과 source cursor 합성을 추가한 프로토타입 기준선입니다.

## 포함 기능

- 출력 DC 재사용
- adaptive 60 / 30 / 15 FPS
- 오버레이 기본 OFF
- 작업표시줄 클릭
- 일반 클릭/드래그/휠/XButton
- 제목 표시줄 이동 및 더블클릭
- 일부 커스텀 타이틀바 실제 입력 주입 fallback
- source cursor 합성

## 실행

- `run.cmd`: adaptive
- `run-30fps.cmd`: 30 FPS 고정
- `run-60fps.cmd`: 60 FPS 고정

가상 2560×1440 디스플레이와 물리 1920×1080 디스플레이가 이미 준비되어 있어야 합니다.

## 단축키

- Ctrl+Alt+F12: 종료
- Ctrl+Alt+F11: 입력 전달 ON/OFF
- Ctrl+Alt+F10: 진단 오버레이 ON/OFF
- Ctrl+Alt+F9: 렌더 모드 순환

이 디렉터리는 정식 제품 코드로 전환하기 전의 동작 기준선으로 보존합니다.
