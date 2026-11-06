# KeyMan 설치·업데이트·제거

## 배포 상태

현재는 공개 알파 준비 단계다. 다음 알파 후보 버전은 `1.1.0-alpha.1`이며 아직 발행되지 않았다. [릴리스 목록·공개 상태 확인](https://github.com/jaywapp/key-man/releases)에서 실제 배포 여부를 확인한다. 기존 릴리스가 보이더라도 이번 소스의 수정 사항이 포함되었다고 가정하지 않는다.

## 요구 환경과 파일 확인

- 설치 패키지 대상은 Windows x64다. ARM64 네이티브 패키지는 제공하지 않는다.
- GUI·CLI·MCP 실행 파일은 .NET 10.0.14 런타임을 포함하므로 별도 SDK 없이 실행하도록 패키징한다. 배포 런타임 패치는 `Build-Installer.ps1`에서 관리한다.
- 모든 Windows 버전·클린 VM에서의 동작을 확인한 상태는 아니다. 실제 검증 환경과 알려진 문제는 해당 릴리스 안내를 따른다.
- 현재 코드 서명 절차는 구성되어 있지 않다. 배포된 파일의 실제 서명 상태를 확인하고, Windows 경고를 우회하도록 보안 기능을 해제하지 않는다.

알파가 발행되면 릴리스에서 설치 파일과 같은 이름의 `.exe.sha256` 파일을 함께 받는다. 예를 들어 다음 명령으로 받은 파일의 해시를 계산하고 체크섬 파일의 값과 비교한다.

```powershell
Get-FileHash -LiteralPath '.\KeyMan-Setup-1.1.0-alpha.1.exe' -Algorithm SHA256
Get-Content -LiteralPath '.\KeyMan-Setup-1.1.0-alpha.1.exe.sha256'
Get-AuthenticodeSignature -LiteralPath '.\KeyMan-Setup-1.1.0-alpha.1.exe'
```

예시 버전은 준비 중인 후보이며 다운로드 가능하다는 뜻이 아니다. 해시 일치는 파일 일치 확인이며 코드 서명이나 배포자 신뢰를 대신하지 않는다.

## 설치 결과

기본 경로는 `%LOCALAPPDATA%\Programs\KeyMan`이다. 설치 과정에서 경로를 바꿀 수 있다.

| 항목 | 역할 |
|---|---|
| `KeyMan.Gui.exe` | WPF GUI |
| `key-man.exe` | CLI 기본 명령 |
| `keyman.exe` | CLI 호환 별칭 |
| `key-man-mcp.exe` | 로컬 stdio MCP 서버 |
| `LICENSE.txt` | KeyMan의 MIT 라이선스 원문 |
| `ThirdPartyNotices` | 포함된 runtime 의존성의 원문 라이선스·고지·버전 목록 |
| 사용자 `PATH` | 기본 선택된 설치 옵션으로 설치 경로 추가 |
| 사용자 `KEYMAN_HOME` | 설치 경로 기록 |
| 시작 메뉴·선택적 바탕 화면 바로가기 | GUI 실행 |

PATH 추가 옵션을 해제해도 `KEYMAN_HOME`은 등록된다. PATH를 추가하지 않았다면 실행 파일의 전체 경로를 사용한다. 새 사용자 PATH는 설치 후 새로 실행한 터미널과 클라이언트에서 적용된다.

```powershell
key-man.exe --help
key-man.exe list
Get-Command key-man-mcp.exe
```

## 업데이트와 제거

자동 업데이트 기능은 없다. GUI와 MCP 클라이언트를 정상 종료한 뒤 새 설치 파일을 같은 경로에 설치한다. 기존 메타데이터와 Windows 자격 증명을 사용한다. 이전 버전으로의 다운그레이드나 모든 버전 간 호환성을 보장하지 않으므로 릴리스별 안내를 확인한다.

제거는 Windows의 설치된 앱 또는 설치 경로의 제거 프로그램에서 실행한다. 설치 프로그램이 추가한 사용자 PATH 항목, `KEYMAN_HOME`, 설치 레지스트리 값을 제거한다. **사용자 키와 `%LOCALAPPDATA%\KeyMan\vault.json`은 자동 삭제하지 않는다.** 제거는 제공업체의 키 폐기도 수행하지 않는다.

사용자 데이터까지 정리하려면 앱 제거 전에 GUI·CLI에서 항목을 삭제한다. JSON만 삭제하면 실제 자격 증명은 남고 GUI에서 찾을 수 없게 될 수 있다. 메타데이터 사본만으로는 비밀값을 복구·이전할 수 없다.

## 소스 검증과 로컬 패키징

Windows, .NET 10 SDK, Inno Setup이 필요하다. `global.json`은 SDK `10.0.400`을 기준으로 같은 기능 밴드의 최신 패치를 허용한다. CI의 Inno Setup 버전은 `6.7.4`다. [기여 안내](../CONTRIBUTING.md)의 빌드·테스트를 먼저 실행한다.

```powershell
& .\tools\Test-KeyMan.ps1
& .\tools\Build-Installer.ps1 -Version '1.1.0-alpha.1'
& .\tools\Test-Installer.ps1 -InstallerPath '.\artifacts\installer\KeyMan-Setup-1.1.0-alpha.1.exe'
```

`Build-Installer.ps1`은 GUI·CLI·MCP를 self-contained 단일 파일로 게시하고 Inno Setup의 `ISCC.exe`로 설치 파일을 컴파일한다. `ISCC.exe`를 찾지 못하면 `-IsccPath`로 전체 경로를 지정한다. `1.1.0-alpha.1` 같은 명시적 알파 버전만 받으며 Windows 파일 버전 형식은 스크립트에서 분리해 전달한다.

산출물은 `artifacts\installer\KeyMan-Setup-1.1.0-alpha.1.exe`와 `.exe.sha256`이다. 게시 디렉터리는 `artifacts\publish\<version>\` 아래에 나뉜다. 기본 설치 검증은 별도 임시 경로와 레지스트리·환경 등록을 생략하는 모드로 실행한다. 실제 사용자 PATH 등록 검증인 `-RegisterEnvironment`는 격리된 CI 러너에서 사용하며 개인 Windows 환경에서는 기본 모드를 사용한다.

## CI와 릴리스

`verify-windows.yml`은 PR·main에서 빌드와 전체 테스트를 실행한다. main push만으로 릴리스를 발행하지 않는다. `build-installer.yml`은 관리자가 main에서 버전을 명시해 수동 실행하는 별도 절차다. 설치 검증 후 GitHub prerelease와 SHA-256 체크섬을 발행하며 최신 안정판으로 지정하지 않는다. 임시 Actions 아티팩트는 3일 보관하고 Release 업로드 후 선택적으로 업로드한다. 사용자가 받는 GitHub Release 자산과 임시 아티팩트는 별개다.

실제 릴리스 발행, 저장소 공개 전환, 서명·SmartScreen 평판, 클린 VM 검증은 로컬 준비 완료와 별도로 확인해야 한다.
