# KeyMan Windows 설치 파일

## 자동 빌드

`.github/workflows/build-installer.yml`은 다음 경우 Windows 설치 파일을 만든다.

- `main` 브랜치에 push
- GitHub Actions 화면에서 수동 실행

워크플로는 Windows 러너에서 다음 순서로 실행된다.

1. .NET 9 복원 및 Release 빌드
2. 저장소 스모크 테스트
3. WPF 앱과 CLI를 `win-x64` self-contained 단일 파일로 게시
4. Inno Setup으로 `KeyMan-Setup-<version>.exe` 생성
5. 임시 경로에 자동 설치해 `key-man.exe`와 사용자 PATH 등록 검증
6. 설치 파일을 `KeyMan-Windows-Installer` 아티팩트로 30일간 보관

버전은 `1.0.<GitHub Actions 실행 번호>` 형식으로 생성된다.

## 설치 결과

기본 설치 경로는 `%LOCALAPPDATA%\Programs\KeyMan`이다. 설치 화면에서 다른 경로를 선택할 수 있다.

설치 프로그램은 다음 항목을 구성한다.

- `KeyMan.Gui.exe`: WPF GUI
- `key-man.exe`: CLI 기본 명령
- `keyman.exe`: 기존 CLI 이름과의 호환 별칭
- 사용자 `PATH`: 설치 경로 추가
- 사용자 `KEYMAN_HOME`: 설치 경로
- 시작 메뉴 바로가기와 선택적 바탕 화면 바로가기

새 환경변수는 설치 완료 후 실행하는 새 터미널에서 적용된다.

## 제거

제거 프로그램은 KeyMan 설치 과정에서 PATH를 추가한 경우에만 해당 항목을 제거한다. `KEYMAN_HOME`과 설치 레지스트리 값도 함께 제거한다. Windows 자격 증명 관리자에 저장된 사용자 키와 `%LOCALAPPDATA%\KeyMan\vault.json`은 데이터 손실 방지를 위해 자동 삭제하지 않는다.

## 로컬 컴파일

먼저 GUI와 CLI를 게시한다.

```powershell
dotnet publish .\src\KeyMan\KeyMan.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o .\artifacts\publish\gui
dotnet publish .\src\KeyMan.Cli\KeyMan.Cli.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o .\artifacts\publish\cli
```

Inno Setup 설치 후 다음 명령으로 설치 파일을 컴파일한다.

```powershell
ISCC.exe /DMyAppVersion=1.0.0 .\installer\KeyMan.iss
```

결과는 기본적으로 `artifacts\installer`에 생성된다.
