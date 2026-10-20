# VirtualQHDMirror v0.22

**언어:** [English](RELEASE_NOTES_v0.22.md) · **한국어** · [日本語](RELEASE_NOTES_v0.22.ja.md)

Windows 11 x64 — 외부 가상 모니터 드라이버 설정 안내가 추가됐습니다.

## 변경 사항

- **Virtual Display Setup:** 미설치/준비 완료/QHD 모드/재시작 상태 안내
- **Get driver:** [ge9/IddSampleDriver 공식 Releases](https://github.com/ge9/IddSampleDriver/releases)로 이동하며 타사 드라이버 경고 표시
- **Recheck:** 드라이버 설치 후 디스플레이 상태 재탐지
- v0.21.2 D3D11 + Portal 미러링, 마우스·드래그, Windows 로그인 자동 실행 유지

## 다운로드

- `VirtualQHDMirror-v0.22-win-x64-Setup.exe`: 사용자 단위 앱 설치 파일
- `VirtualQHDMirror-v0.22-win-x64.zip`: 포터블 버전
- 각 파일의 SHA-256 체크섬

**가상 모니터 드라이버는 동봉하거나 자동 설치하지 않습니다.** ge9는 타사 소프트웨어입니다. 앱/설치 프로그램은 미서명으로 Windows에서 게시자 경고가 나올 수 있습니다. Secure Boot와 인증서 설정은 변경하지 않습니다.

## 검증

개발 PC에서 앱 빌드/실행, Get driver, Recheck/Ready, 기존 미러링 PASS. 가상 모니터가 없는 별도 PC에서 빌드/실행 및 Missing 감지 PASS. 새 Setup.exe 설치/제거 검증은 공개 전 필요합니다.

[한국어 README](README.ko.md)
