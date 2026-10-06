# VirtualQHDMirror v0.13 prototype

v0.12 기준선을 보존하면서 작업표시줄과 커스텀 제목 표시줄의 실제 입력 주입 사용감을 개선한 프로토타입입니다.

## v0.13 변경점

- 작업표시줄/커스텀 제목 표시줄 fallback 클릭을 MouseUp 시점에 시작해 물리 MouseUp이 source 쪽에 고아 이벤트로 남지 않도록 변경
- 클릭 직후 source cursor를 즉시 destination으로 되돌리지 않고 native source interaction session을 유지
- 작업표시줄 미리보기, Start/Search 등 Windows shell 상호작용 중에는 source cursor를 유지
- 일반 앱이 foreground가 되거나 shell 영역을 벗어나면 현재 source 좌표를 destination 좌표로 역매핑해 자연스럽게 복귀
- native interaction session은 안전을 위해 최대 30초 후 자동 복귀
- source cursor의 실제 hotspot과 bitmap 크기를 읽어 축소 비율에 맞춰 합성
- XButton native fallback 입력도 명시적으로 처리

## 포함 기능

- 출력 DC 재사용
- adaptive 60 / 30 / 15 FPS
- 오버레이 기본 OFF
- 일반 클릭/드래그/휠/XButton
- 제목 표시줄 이동 및 더블클릭
- 작업표시줄/커스텀 타이틀바 native input session
- source cursor hotspot 보정 합성

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

## 보존 기준

`prototype/v0.12/`는 이전 동작 비교용 기준선으로 수정하지 않습니다.
