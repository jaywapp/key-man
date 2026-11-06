# KeyMan

**API 키를 찾을 때마다 `.env`를 뒤지지 않도록.**

KeyMan은 Windows 개발자를 위한 로컬 API 키·토큰 보관함입니다. GUI에서 서비스와 환경별로 정리하고, CLI에서 스크립트에 연결하고, MCP로 AI 코딩 도구에서 목록 조회·등록·복사를 요청할 수 있습니다. 실제 값은 Windows 자격 증명 관리자에 저장합니다.

> **공개 알파 준비 중입니다.** 이 문서는 다음 알파의 소스와 사용 방법을 설명합니다. 이번 변경을 포함한 설치 파일은 아직 공개되지 않았습니다. [릴리스 목록·공개 상태 확인](https://github.com/jaywapp/key-man/releases)에서 실제 배포 여부와 해당 버전의 안내를 먼저 확인하세요.

![가짜 데이터로 렌더링한 KeyMan 앱 화면](docs/assets/keyman-demo.png)

현재 앱의 XAML을 가짜 데이터로 렌더링한 화면입니다. 실제 사용자 키는 포함하지 않습니다.

## 이런 흐름에 맞습니다

- 여러 서비스의 개발용 키를 이름·서비스·환경·만료일로 관리합니다.
- GUI에서 값을 화면에 드러내지 않고 복사합니다.
- CLI에서 값을 받아 필요한 로컬 프로세스에 전달합니다.
- Codex·Claude Code에서 키 이름으로 작업을 요청합니다. **KeyMan MCP 도구 결과에는 비밀값을 반환하지 않습니다.**

클라우드 계정 없이 현재 Windows 사용자 범위에서 동작합니다. 팀 공유, 클라우드 동기화, 키 발급·자동 회전 기능은 제공하지 않습니다.

## 설치와 첫 키 등록

배포된 알파를 사용하려면 릴리스의 `KeyMan-Setup-<version>.exe`와 SHA-256 체크섬을 확인한 뒤 설치합니다. 설치 파일은 Windows x64용이며 .NET 런타임을 포함합니다. 서명·운영체제·업데이트 안내는 [설치 문서](docs/installer.md)를 확인하세요. 공개 알파가 없는 동안에는 아래 소스 실행 방법을 사용합니다.

1. 시작 메뉴에서 KeyMan을 실행합니다.
2. 추가 화면에서 이름 `Sample`, 서비스 `Demo`, 환경 `Development`와 테스트용 값을 입력합니다.
3. 목록에서 `Demo`를 검색하고 복사합니다. 만료일을 지정하면 만료 상태도 확인할 수 있습니다.

**GUI와 MCP는 30초 뒤 또는 정상 종료 시 자신이 복사한 클립보드의 정리를 시도합니다. CLI `copy`는 자동으로 지우지 않습니다.** 이후 복사한 다른 내용은 지우지 않습니다. 클립보드가 사용 중이면 정리에 실패할 수 있으며, 기록·클라우드 동기화 사본과 강제 종료까지 삭제를 보장하지 않습니다.

## CLI

설치 후 새 터미널에서 다음 명령을 사용할 수 있습니다. 비밀값 인수를 생략하면 숨김 입력을 받습니다.

```powershell
key-man.exe register "Sample" --service Demo --environment Development
key-man.exe list Demo
key-man.exe copy "Sample" --service Demo --environment Development
```

`key-man.exe get "Sample"`은 **비밀값을 표준 출력으로 반환**합니다. 터미널·대화·로그로 출력하지 않도록 직접 제어하는 스크립트에서 사용하세요. 옵션, stdin 등록, 종료 코드는 [CLI 문서](docs/cli.md)에 있습니다.

## MCP

로컬 stdio 서버 `key-man-mcp.exe`가 GUI·CLI와 같은 보관함을 사용합니다. “Demo 서비스의 키 목록을 보여줘”, “Sample을 클립보드로 복사해줘”처럼 요청할 수 있습니다. 등록할 값은 대화에 붙여 넣지 않고 로컬 클립보드 또는 서버가 상속한 환경 변수에서 읽습니다. [Codex·Claude Code 연결 방법](docs/mcp.md)을 참고하세요.

MCP 결과에 값을 싣지 않는 것은 **AI나 같은 사용자 권한의 프로그램으로부터 비밀값을 격리한다는 뜻이 아닙니다.** 셸 접근이 가능한 에이전트는 CLI `get` 등을 실행할 수 있습니다. 클립보드·환경 변수도 같은 사용자 권한의 다른 프로그램이 읽을 수 있습니다. 권한과 도구 승인은 사용하는 AI 클라이언트에서 관리해야 합니다.

## 저장 위치와 보안

| 데이터 | 위치 |
|---|---|
| 실제 API 키·토큰 | 현재 Windows 사용자의 자격 증명 관리자, `KeyMan/<id>` |
| 이름·서비스·환경·메모·만료일 등 | `%LOCALAPPDATA%\KeyMan\vault.json` |

메타데이터는 평문입니다. 이름이나 메모에 비밀값을 넣지 마세요. JSON 파일만 복사해서는 비밀값을 백업하거나 다른 PC로 옮길 수 없습니다. 앱 제거 후에도 사용자 데이터는 보존됩니다. 삭제·제보·위협 모델은 [보안 정책](SECURITY.md)을 참고하세요.

## 소스에서 실행

Windows와 .NET 10 SDK가 필요합니다.

```powershell
dotnet build .\KeyMan.sln -c Release
dotnet run --project .\src\KeyMan\KeyMan.csproj
dotnet run --project .\src\KeyMan.Cli\KeyMan.Cli.csproj -- --help
```

기존 `.env`를 가져오려면 [CLI 문서의 가져오기 안내](docs/cli.md)를 먼저 확인하세요. 이 기능은 원본 `.env`를 삭제하지 않습니다.

## 문서와 피드백

- [설치·업데이트·제거](docs/installer.md)
- [CLI 사용법](docs/cli.md) · [MCP 연결](docs/mcp.md) · [아키텍처](docs/architecture.md)
- [기여 안내](CONTRIBUTING.md) · [보안 정책](SECURITY.md) · [문서 목록](docs/README.md)

알파 피드백에는 사용한 버전, Windows 버전, 실행 경로(GUI/CLI/MCP), 재현 과정을 적어 주세요. 실제 키·토큰·`.env`·개인 보관함 파일은 첨부하지 마세요.

KeyMan은 [MIT 라이선스](LICENSE)를 따릅니다. 설치 폴더의 `LICENSE.txt`에 원문을 포함하며, 외부 구성요소의 라이선스·고지는 `ThirdPartyNotices`에 있습니다.
