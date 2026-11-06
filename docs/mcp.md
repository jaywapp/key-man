# KeyMan MCP

`key-man-mcp.exe`는 Windows에서 실행하는 로컬 stdio MCP 서버다. GUI·CLI와 같은 현재 Windows 사용자의 보관함을 사용한다. 이 문서는 공개 알파 준비 소스 기준이며, 이번 변경을 포함한 설치 파일은 아직 공개되지 않았다.

## 값이 이동하는 방식

**KeyMan MCP는 비밀값을 도구 결과에 반환하지 않는다.** 등록할 값도 MCP 인수로 받지 않고 로컬 소스에서 읽는다.

| 도구 | 동작 | 모델로 반환하는 정보 |
|---|---|---|
| `keyman_list` | 이름·서비스로 검색 | 이름·서비스·환경·만료일 |
| `keyman_copy` | 보관함의 값을 Windows 클립보드로 복사 | 성공 여부와 항목 요약 |
| `keyman_register_from_clipboard` | 현재 로컬 클립보드의 텍스트 등록 | 성공 여부와 항목 요약 |
| `keyman_register_from_environment` | 서버 프로세스의 지정 환경 변수에서 등록 | 성공 여부와 항목 요약 |
| `keyman_remove` | 비밀값과 메타데이터 영구 삭제 | 성공 여부와 항목 요약 |

등록 도구의 `force=true`는 같은 이름·서비스·환경의 값을 교체한다. 등록 도구는 교체 가능성 때문에 파괴적 변경으로 표시된다. 삭제도 파괴적 변경이다. 이 표기는 클라이언트에 전달하는 힌트이며 서버 자체의 승인 장치는 아니다. 실제 승인·자동 실행 정책은 MCP 클라이언트에서 설정해야 한다.

30초 뒤 또는 서버 정상 종료 시 자신이 복사한 클립보드가 그대로일 때 정리를 시도한다. 이후 다른 앱에서 복사한 내용은 지우지 않는다. 클립보드 사용 중 오류, 강제 종료, Windows 클립보드 기록·동기화 사본의 삭제는 보장하지 않는다. 클립보드에서 등록한 원본은 자동 삭제 대상으로 취급하지 않는다.

## 보안 경계

키를 AI 대화·MCP 인수·메타데이터에 넣지 않는다. 서버에 `get` 도구는 없지만, **같은 사용자 권한의 프로그램이나 셸을 사용할 수 있는 에이전트에서 값을 숨기는 격리 장치는 아니다.** CLI `get`, 클립보드, 환경 변수 등 다른 로컬 경로로 값에 접근할 수 있다. 필요한 도구만 허용하고 클라이언트의 권한·승인 설정을 함께 검토한다. [보안 정책](../SECURITY.md)에 한계를 정리했다.

## 설치와 실행 파일 확인

설치 파일에는 GUI·CLI·MCP가 함께 포함된다. 설치 후 새 터미널에서 다음 명령으로 파일 경로를 확인한다.

```powershell
Get-Command key-man-mcp.exe
```

명령을 찾지 못하면 기본 설치 경로 `%LOCALAPPDATA%\Programs\KeyMan\key-man-mcp.exe`를 확인하고 MCP 설정에 전체 경로를 사용한다. 소스 실행도 가능하다.

```powershell
dotnet run --project .\src\KeyMan.Mcp\KeyMan.Mcp.csproj
```

stdio 서버이므로 직접 실행한 터미널에서 대화형 메뉴가 나오지는 않는다. 연결할 클라이언트가 프로세스를 시작하도록 설정한다.

## Codex 연결

설치 후 새 터미널에서 서버를 등록한다.

```powershell
codex mcp add key-man -- key-man-mcp.exe
codex mcp list
```

이 구문은 [공식 OpenAI MCP 문서](https://learn.chatgpt.com/docs/extend/mcp?surface=cli)의 stdio 등록 방식이다. 새 세션에서 `keyman_list` 등 KeyMan 도구가 보이는지 확인한다. 파일이 존재하거나 설정 목록에 나온 것만으로 연결 성공을 판단하지 않고, 가짜 데이터로 실제 목록 호출까지 확인한다.

## Claude Code 연결

개인 보관함 연결은 사용자 범위로 등록한다.

```powershell
claude mcp add --transport stdio --scope user key-man -- key-man-mcp.exe
claude mcp list
```

등록 범위와 stdio 방식은 [공식 Claude Code MCP 문서](https://code.claude.com/docs/en/mcp)를 따른다. 직접 설정할 경우 아래 `mcpServers` 항목을 클라이언트 설정에 병합한다. 기존 서버 항목을 덮어쓰지 않는다.

```json
{
  "mcpServers": {
    "key-man": {
      "command": "key-man-mcp.exe",
      "args": []
    }
  }
}
```

클라이언트가 PATH를 상속하지 못하면 `command`에 실제 설치 파일의 전체 경로를 쓴다. 설치 전부터 실행 중이던 IDE·클라이언트는 다시 시작해야 새 사용자 PATH가 반영될 수 있다.

## 요청 예시

- “KeyMan에서 Demo 서비스의 키 목록을 보여줘.”
- “Demo 서비스, Development 환경의 Sample을 클립보드로 복사해줘.”
- “현재 클립보드 값을 이름 Sample, 서비스 Demo, 환경 Development로 등록해줘.”
- “서버 환경 변수 DEMO_TOKEN을 이름 Sample, 서비스 Demo로 등록해줘.”

환경 변수 등록은 **이미 실행 중인 MCP 서버가 가진 환경 변수**를 읽는다. 다른 터미널에서 나중에 설정한 변수는 반영되지 않는다. 실제 값은 설정 파일에 쓰지 않고 서버를 시작하는 프로세스에서 제공하며, 변경 시 서버를 재시작한다. 메타데이터 인수만 요청에 넣는다.

## 테스트용 경로

`KEYMAN_VAULT_PATH`를 별도 JSON 경로로 지정할 수 있다. 이는 GUI·CLI·MCP의 **메타데이터 경로만** 바꾼다. 실제 비밀값은 여전히 같은 Windows 사용자의 자격 증명 관리자에 저장된다. 테스트에서는 고유 이름·ID와 가짜 값만 사용하고 개인 보관함을 읽거나 지우지 않는다.

원격·클라우드 세션이 로컬 Windows 실행 파일과 사용자 보관함을 사용할 수 있다고 가정하지 않는다. 이 서버를 실행할 Windows 로컬 세션이 필요하다.
