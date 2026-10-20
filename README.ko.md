# VirtualQHDMirror

**언어:** [English](README.md) · **한국어** · [日本語](README.ja.md)

VirtualQHDMirror는 Windows의 **가상 QHD(2560×1440) 바탕화면을 실제 FHD(1920×1080) 모니터에 미러링**하고 마우스 입력을 가상 화면으로 전달하는 프로그램입니다.

## 다운로드 · [v0.22 출시노트](RELEASE_NOTES_v0.22.ko.md)

**지원 환경:** Windows 11 x64

[GitHub Releases](https://github.com/Arktis-P/VirtualQHDMirror/releases)에서 **Setup.exe(설치형)**와 **ZIP(포터블)**으로 배포합니다. 설치형은 현재 사용자 계정에 앱을 설치하며, 가상 모니터 드라이버는 설치하지 않습니다. ZIP 압축을 푼 뒤 `VirtualQHDMirror.exe`를 실행하고 Session과 ElevatedHelper EXE를 같은 폴더에 유지하세요.

.NET 8 런타임이 포함되어 있어 별도 런타임 설치가 필요하지 않습니다. **앱과 설치 프로그램은 코드 서명되지 않아** Windows에서 게시자 경고가 표시될 수 있습니다.

**가상 모니터 드라이버는 포함하지 않습니다.** **Get driver** 버튼은 외부 [ge9/IddSampleDriver 공식 Releases](https://github.com/ge9/IddSampleDriver/releases)를 엽니다. 설치 방법·서명·보안 사항은 해당 개발자 문서를 확인하세요. VQM은 드라이버 자동 설치, 인증서 추가, Secure Boot 해제 또는 테스트 서명 모드 활성화를 하지 않습니다.

## 빠른 시작

1. 앱을 실행하고 **Virtual Display Setup**에서 가상 모니터 상태를 확인합니다.
2. 가상 QHD가 없으면 **Get driver**로 외부 배포 페이지를 열어 설치 안내를 확인합니다.
3. 호환 드라이버에서 **2560×1440** 가상 모니터를 설정합니다. ge9 설정은 해당 프로젝트 문서를 참고합니다.
4. VQM의 **Recheck**를 눌러 가상 QHD 소스와 실제 FHD 대상이 인식되는지 확인합니다.
5. 소스와 대상을 선택하고 **Start Mirror**를 누릅니다. 드라이버 설정 변경 전에는 미러링을 종료합니다.

## 주요 기능

- QueryDisplayConfig 기반 물리·가상 모니터 자동 탐색
- Virtual Display Setup, Get driver, Recheck
- D3D11 Desktop Duplication 미러링, 대상 모니터 VSync 기반 출력
- Portal 기반 마우스 입력, 논리 커서 이동, 화면 간 드래그
- Windows 로그인 시 자동 시작 옵션
- GDI 렌더링 및 v0.14.1 레거시 폴백

## 소스에서 빌드

Windows에 **.NET 8 SDK**를 설치하고 저장소 루트에서 실행합니다.

```powershell
.\build-v022.cmd
.\run-v022.cmd
```

실행 파일은 `artifacts/release/v0.22/win-x64/`에 생성됩니다. `VirtualQHDMirror.exe`, `VirtualQHDMirror.Session.exe`, `VirtualQHDMirror.ElevatedHelper.exe`를 같은 폴더에 유지하세요. 포터블 ZIP과 SHA-256 체크섬도 생성됩니다.

**Inno Setup 6**을 설치했다면 `build-v022-installer.cmd`로 앱 설치 EXE를 만들 수 있습니다. `artifacts/`는 Git에서 제외됩니다. 기존 `build-release.cmd`와 `run.cmd`는 v0.21.2 폴백용입니다.

## 제한 사항과 참고 문서

현재 호환 가상 QHD 드라이버가 별도로 필요하며, 한 번에 가상 소스 하나와 실제 대상 하나를 선택해 미러링합니다. 일부 드라이버 설정에는 관리자 승인이 필요할 수 있습니다. 저장소의 VQM 자체 IDD는 **미서명 실험용 소스**로 배포 앱에는 포함하지 않습니다.

[영어 출시노트](RELEASE_NOTES_v0.22.md) · [한국어 출시노트](RELEASE_NOTES_v0.22.ko.md) · [일본어 출시노트](RELEASE_NOTES_v0.22.ja.md)

[패키징 안내](docs/V022_RELEASE_PACKAGING.md) · [외부 드라이버 정책](docs/V022_STAGE_B_EXTERNAL_DRIVER_PIVOT.md) · [타사 라이선스 공지](THIRD_PARTY_NOTICES.md)

이전 버전 폴백은 `archive/`에 보관합니다. 명시적 승인 없이 GitHub Actions를 사용하지 않습니다.
