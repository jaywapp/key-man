# KeyMan CLI

설치된 `key-man.exe`는 GUI·MCP와 같은 Windows 사용자 보관함을 사용한다. `keyman.exe`는 호환 별칭이며, 소스 빌드의 CLI 파일명도 `keyman.exe`다. 이 문서는 공개 알파 준비 소스 기준이다. [설치와 배포 상태](installer.md)를 먼저 확인한다.

## 등록

비밀값 인수를 생략하면 숨김 입력을 받는다.

```powershell
key-man.exe register "Sample" --service Demo --environment Development
```

자동화에서는 이미 안전하게 제공받은 값을 stdin으로 보낸다. 다음 예시는 호출한 프로세스의 임시 환경 변수를 사용한다.

```powershell
$env:DEMO_TOKEN | key-man.exe register "Sample" --service Demo --stdin
if ($LASTEXITCODE -ne 0) { throw 'KeyMan registration failed.' }
```

값을 명령 인수로 직접 전달하는 문법도 있지만 셸 기록과 프로세스 목록에 남을 수 있다. 프롬프트나 스크립트에 실제 값을 하드코딩하지 않는다. 환경 변수·stdin도 같은 사용자 권한의 다른 프로그램으로부터 격리되지 않는다.

| 옵션 | 동작 |
|---|---|
| `--service <service>` | 기본값 `General` |
| `--environment <environment>` | 기본값 `Development` |
| `--expires <YYYY-MM-DD>` | 표시용 만료일. 제공업체의 키 만료를 설정하지 않음 |
| `--notes <text>` | 평문 메타데이터에 저장하는 메모 |
| `--force` | 이름·서비스·환경이 같은 항목의 값과 메모·만료일 교체 |
| `--skip-existing` | 같은 이름·서비스·환경이 있으면 변경 없이 성공. `--force`와 함께 사용 불가 |
| `--stdin` | 표준 입력에서 값 읽기 |

`--force`는 기존 값을 덮어쓴다. 서비스·환경을 확인한 뒤 사용한다. 메모에 비밀값을 넣지 않는다.

## 목록과 선택

```powershell
key-man.exe list
key-man.exe list Demo
key-man.exe list --json
```

CLI 검색은 이름·서비스를 대상으로 한다. JSON에는 이름·서비스·환경·만료일이 포함되며 비밀값은 포함되지 않는다. 같은 이름의 후보가 여러 개이면 `get`, `copy`, `remove`는 선택을 요구한다.

```powershell
key-man.exe copy "Sample" --service Demo --environment Development
```

## 복사

`copy`는 값을 클립보드에 넣고 확인 문구만 출력한 뒤 종료한다. **CLI는 클립보드를 자동 삭제하지 않는다.** GUI·MCP는 30초 뒤 자신이 복사한 클립보드의 정리를 시도한다. 클립보드가 사용 중이면 실패할 수 있으며, Windows 클립보드 기록·동기화 사본이나 강제 종료 시 삭제도 보장하지 않는다.

## 스크립트에서 값 사용

`get`은 비밀값만 표준 출력으로 반환한다. 명령을 그대로 실행하면 값이 터미널에 보인다. 에이전트가 셸 출력 전체를 수집하는 환경에서는 호출하지 않는다. 직접 제어하는 스크립트에서는 출력하지 않고 필요한 프로세스에 전달한다.

```powershell
$token = & key-man.exe get "Sample" --service Demo --environment Development
if ($LASTEXITCODE -ne 0) { throw 'KeyMan lookup failed.' }

try {
    $env:SERVICE_TOKEN = $token
    some-command
}
finally {
    Remove-Item Env:SERVICE_TOKEN -ErrorAction SilentlyContinue
    $token = $null
}
```

`some-command`는 실제 사용할 로컬 명령으로 바꾼다. 이 패턴은 결과를 출력하지 않도록 돕지만 메모리 삭제나 자식 프로세스의 환경 변수 정리를 보장하지 않는다. 기존 `SERVICE_TOKEN`이 있다면 호출 전에 보존·복원 방식을 정한다.

## 삭제

```powershell
key-man.exe remove "Sample" --service Demo
key-man.exe remove "Sample" --service Demo --yes
```

`delete`는 `remove`의 별칭이다. `--yes`는 확인 프롬프트를 생략한다. 보관함에서 삭제해도 키 제공업체의 키가 폐기되지는 않는다.

## `.env` 가져오기

소스 저장소에서 실행한다. 지정한 폴더 아래의 `.env`를 찾아 서비스·환경·변수명 기준으로 등록하며, 비밀값은 CLI의 stdin으로 전달한다.

```powershell
& .\tools\Import-Env.ps1 -RootPath 'D:\demo-projects'
```

설치한 CLI를 PATH에서 찾는다. 소스에서 빌드한 파일을 쓰려면 `-KeyManPath`를 지정한다. `-VaultPath`는 선택한 CLI 프로세스의 메타데이터 경로만 바꾸며 Windows 자격 증명 관리자는 격리하지 않는다.

```powershell
& .\tools\Import-Env.ps1 -RootPath 'D:\demo-projects' -KeyManPath '.\src\KeyMan.Cli\bin\Release\net10.0-windows\keyman.exe'
```

실행 전 대상 경로를 좁혀 의도한 파일만 포함한다. 원본 파일은 삭제하지 않는다. 환경은 `Development`이며 이름·서비스·환경이 같은 기존 항목은 `--skip-existing`으로 건너뛴다. 빈 값, 명백한 placeholder, 저장 크기 제한 초과 값도 건너뛴다. `node_modules`, `bin`, `obj`, `dist`, `build`, `.git` 아래의 파일은 제외한다. 일반적인 한 줄 대입 형식을 처리하는 도구이며 모든 dotenv 문법의 호환 파서는 아니다.

각 항목을 개별 등록하므로 중간에 실패하면 가져오기를 중단하고 이미 성공한 항목은 보존한다. 전체 일괄 취소 기능은 없다. 유효 항목이 0개이면 보관함을 다시 쓰지 않는다. 경로와 변수명은 메타데이터·오류 메시지에 포함될 수 있으므로 공유 전에 검토한다.

## 종료 코드

| 코드 | 의미 |
|---|---|
| `0` | 성공 또는 사용자 삭제 취소 |
| `1` | 저장소·시스템 오류 또는 교체 옵션 없는 중복 등록 |
| `2` | 잘못된 명령·옵션·입력 |
| `3` | 항목 없음 |
| `4` | 같은 이름의 후보가 여러 개 있음 |
