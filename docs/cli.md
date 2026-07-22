# KeyMan CLI

`keyman.exe`는 WPF 앱과 같은 저장소를 사용하는 Windows CLI다. 실제 값은 Windows 자격 증명 관리자에, 메타데이터는 `%LOCALAPPDATA%\KeyMan\vault.json`에 저장된다.

## 명령

### 등록

```powershell
keyman.exe register "Sample" "<TOKEN>"
keyman.exe register "Sample" "<TOKEN>" --service OpenAI --environment Development
```

명령 인수에 토큰을 직접 쓰면 셸 기록이나 프로세스 목록에 남을 수 있다. 직접 사용할 때는 토큰 인수를 생략해 숨김 입력을 사용한다.

```powershell
keyman.exe register "Sample" --service OpenAI
```

자동화에서는 표준 입력을 사용할 수 있다.

```powershell
Get-Content -LiteralPath '.\token.txt' -Raw | keyman.exe register "Sample" --stdin
```

지원 옵션:

- `--service <service>`: 기본값 `General`
- `--environment <environment>`: 기본값 `Development`
- `--expires <YYYY-MM-DD>`: 만료일
- `--notes <text>`: 민감하지 않은 메모
- `--force`: 동일한 이름·서비스·환경 항목 교체
- `--stdin`: 표준 입력에서 값 읽기

### 조회

```powershell
keyman.exe get "Sample"
keyman.exe get "API_KEY" --service toss --environment Development
```

`get`은 값만 표준 출력에 반환한다. AI 자동화에서는 결과를 대화나 로그에 출력하지 말고 바로 환경 변수로 받아 사용한다.

```powershell
$env:SERVICE_TOKEN = & keyman.exe get "Sample"
try {
    some-command
}
finally {
    Remove-Item Env:SERVICE_TOKEN
}
```

### 클립보드 복사

```powershell
keyman.exe copy "Sample"
```

### 목록

목록에는 실제 값이 포함되지 않는다.

```powershell
keyman.exe list
keyman.exe list OpenAI
keyman.exe list --json
```

### 삭제

```powershell
keyman.exe remove "Sample"
keyman.exe remove "Sample" --yes
```

`delete`는 `remove`의 별칭이다. 자동화에서는 확인 프롬프트를 생략하는 `--yes`를 사용한다.

## 동일 이름 처리

같은 이름이 여러 서비스나 환경에 있으면 `get`, `copy`, `remove`는 값을 반환하지 않고 후보 메타데이터만 보여준다. `--service` 또는 `--environment`로 하나를 선택해야 한다.

## 종료 코드

- `0`: 성공
- `1`: 저장소 또는 시스템 오류
- `2`: 잘못된 명령이나 옵션
- `3`: 항목 없음
- `4`: 동일 이름 후보가 여러 개임
