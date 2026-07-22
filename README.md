# KeyMan

개발 과정에서 발급받는 API 키와 토큰을 Windows 로컬에서 관리하는 데스크톱 앱입니다. 민감값은 Windows 자격 증명 관리자에 저장하고, 검색에 필요한 비민감 메타데이터만 로컬 JSON에 보관합니다.

## 기능

- 이름, 서비스, 환경, 만료일, 메모와 함께 키·토큰 저장
- 이름·서비스·환경 검색
- 민감값을 화면에 노출하지 않고 클립보드로 복사
- 복사 후 30초가 지나면 클립보드 자동 삭제
- 만료 임박 및 만료 상태 표시
- 확인 후 키·토큰 영구 삭제

## 실행

요구 사항은 Windows와 .NET 9 SDK입니다.

```powershell
dotnet run --project .\src\KeyMan\KeyMan.csproj
```

CLI는 다음 경로에 빌드됩니다.

```powershell
.\src\KeyMan.Cli\bin\Release\net9.0-windows\keyman.exe --help
```

PATH에 포함된 `%USERPROFILE%\.dotnet\tools`에 단일 실행 파일로 설치하려면 다음과 같이 게시합니다.

```powershell
dotnet publish .\src\KeyMan.Cli\KeyMan.Cli.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o .\artifacts\cli
Copy-Item .\artifacts\cli\keyman.exe "$env:USERPROFILE\.dotnet\tools\keyman.exe"
```

릴리스 빌드는 다음과 같이 생성합니다.

```powershell
dotnet build .\KeyMan.sln -c Release
```

## CLI

```powershell
keyman.exe register "Sample" "<TOKEN>" --service SampleService
keyman.exe get "Sample"
keyman.exe copy "Sample"
keyman.exe list --json
keyman.exe remove "Sample" --yes
```

`get`은 값만 표준 출력에 반환합니다. AI 자동화에서는 이를 화면에 출력하지 말고 환경 변수로 직접 받아 사용하세요. 전체 옵션과 안전한 사용 패턴은 [CLI 문서](./docs/cli.md)를 참고합니다.

## 데이터 위치

- 민감값: 현재 Windows 사용자의 자격 증명 관리자
- 메타데이터: `%LOCALAPPDATA%\KeyMan\vault.json`

`vault.json`에는 실제 키나 토큰이 저장되지 않습니다. 다른 Windows 사용자나 다른 PC로 메타데이터 파일만 복사해도 민감값은 함께 이전되지 않습니다.

## `.env` 가져오기

지정한 경로 아래의 `.env`를 재귀적으로 찾아 가져올 수 있습니다. 값은 콘솔에 출력하지 않으며, 같은 서비스·환경·변수명 조합은 다시 추가하지 않습니다.

```powershell
& .\tools\Import-Env.ps1 -RootPath 'D:\workspace'
```

빈 값, 명백한 플레이스홀더, Windows 자격 증명 관리자의 크기 제한을 넘는 값은 건너뜁니다. `node_modules`, `bin`, `obj`, `dist`, `build`, `.git` 아래의 파일은 검색하지 않습니다.
