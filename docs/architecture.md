# KeyMan 아키텍처

KeyMan은 단일 Windows 사용자의 로컬 API 키를 GUI·CLI·stdio MCP에서 함께 관리한다. 서비스·환경별로 찾는 메타데이터와 실제 비밀값을 분리한다. 현재 공개 알파 준비 소스는 .NET 10을 기준으로 한다.

## 저장 구조와 경계

| 계층 | 책임 |
|---|---|
| Windows 자격 증명 관리자 | `KeyMan/<id>` 대상 이름으로 실제 값 저장 |
| `MetadataStore` | `%LOCALAPPDATA%\KeyMan\vault.json`의 메타데이터 읽기·변경·원자적 교체 |
| `WindowsCredentialStore` | Windows Credential API 호출과 크기 제한 처리 |
| GUI·CLI·MCP | 같은 Core 저장소를 사용하는 사용자별 진입점 |

메타데이터에는 ID, 이름, 서비스, 환경, 메모, 만료일, 생성 시각이 포함된다. JSON은 암호화되지 않는다. MCP 목록·성공 결과는 이름·서비스·환경·만료일 등 요약만 반환하고 비밀값을 포함하지 않는다. CLI `get`은 사용자가 명시적으로 요청하면 값을 표준 출력으로 반환한다.

같은 사용자 권한의 프로그램이나 셸 접근이 가능한 에이전트를 차단하는 보안 경계는 제공하지 않는다. 메타데이터 파일 위치를 바꾸더라도 Windows 자격 증명 관리자 자체가 격리되는 것은 아니다. 상세 위협 모델은 [보안 정책](../SECURITY.md)에 있다.

## 변경 흐름

데이터 변경은 같은 메타데이터 경로에 대한 프로세스 간 잠금 안에서 최신 목록을 읽고 적용한다. 오래된 GUI 목록 전체를 저장하지 않으므로 다른 프로세스가 추가한 항목을 덮어쓰는 것을 방지한다. 비밀값 저장·삭제와 실패 시 복원도 변경 구간 안에서 처리한다. JSON은 고유 임시 파일을 작성한 뒤 교체한다.

Windows 자격 증명 관리자와 JSON은 하나의 데이터베이스 트랜잭션이 아니다. 저장 오류에 대한 복원을 수행하지만 프로세스 강제 종료나 시스템 장애까지 완전한 원자성을 보장하지 않는다. 오류가 발생한 보관함 파일을 빈 목록으로 덮어쓰지 않으며, GUI 초기 로드가 실패하면 쓰기 작업을 차단한다.

`.env` importer는 자격 증명·JSON 저장 로직을 별도로 구현하지 않고 CLI 등록 명령을 사용한다. 값은 프로세스 인수 대신 stdin으로 전달한다. 원본 `.env`는 그대로 남는다.

## 프로젝트 구성

- `KeyMan.Core`: 모델, 메타데이터 저장소, Windows 자격 증명 저장소
- `KeyMan`: WPF MVVM GUI. View는 XAML과 UI 경계, ViewModel은 목록·입력·검색·명령 상태를 담당한다.
- `KeyMan.Cli`: `register`, `get`, `copy`, `list`, `remove` 명령
- `KeyMan.Mcp`: stdio MCP 도구와 Windows 클립보드 연동
- `tests/KeyMan.SmokeTests`, `tests/KeyMan.McpTests`, `tests/RegressionTests`: 저장소·프로토콜·회귀 검증
- `installer/KeyMan.iss`: GUI·CLI·MCP 설치와 사용자 PATH·`KEYMAN_HOME` 관리
- `.github/workflows`: PR·main 검증과 명시적 수동 알파 릴리스

## 클립보드 수명

GUI·MCP는 복사 후 30초 또는 정상 종료 시, 현재 클립보드가 자신이 복사한 내용일 때만 정리를 시도한다. 사용자가 이후 복사한 다른 내용은 지우지 않는다. 다른 프로그램이 클립보드를 사용 중이면 정리에 실패할 수 있다. CLI `copy`는 즉시 종료하며 자동 삭제하지 않는다. 클립보드 기록·동기화·다른 앱의 사본·강제 종료 시 삭제는 보장하지 않는다.

팀 공유, 클라우드 동기화, 제공업체 키 발급·자동 회전, 비밀값의 자동 백업은 현재 범위 밖이다.
