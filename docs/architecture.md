# KeyMan 아키텍처

## 목표

개발 중 발급받는 API 키와 토큰을 서비스·환경·만료일 기준으로 찾고, 민감값을 평문 파일에 남기지 않는 Windows 로컬 도구를 제공한다.

## 보안 경계

- 실제 키와 토큰은 Windows 자격 증명 관리자에 `KeyMan/{id}` 이름으로 저장한다.
- 이름, 서비스, 환경, 만료일, 메모만 `%LOCALAPPDATA%\KeyMan\vault.json`에 저장한다.
- UI 목록과 검색 결과에는 민감값을 표시하지 않는다.
- 복사한 민감값은 30초 뒤 클립보드 내용이 그대로일 때만 자동 삭제한다.
- 애플리케이션은 키와 토큰을 로그에 기록하지 않는다.

## 구성 (MVVM)

- `KeyMan.Core`: GUI와 CLI가 공유하는 모델, 메타데이터 저장소, Windows 자격 증명 저장소
- `MainWindow`, `AddCredentialWindow`: XAML 바인딩과 UI 경계 이벤트만 담당하는 View
- `MainViewModel`: 목록, 검색, 복사, 삭제 상태와 Command
- `AddCredentialViewModel`: 입력 상태, 검증, 저장 Command
- `RelayCommand`, `AsyncRelayCommand`: View와 ViewModel을 연결하는 Command 구현
- `WpfDialogService`, `WpfClipboardService`: 대화상자와 클립보드 같은 UI 의존성 격리
- `WindowsCredentialStore`: Windows Credential Manager 네이티브 API 경계
- `MetadataStore`: 비민감 메타데이터 JSON의 원자적 저장
- `KeyMan.Cli`: `register`, `get`, `copy`, `list`, `remove` 명령 진입점
- `installer/KeyMan.iss`: GUI·CLI 설치와 사용자 PATH·`KEYMAN_HOME` 관리
- `.github/workflows/build-installer.yml`: `main` push 시 Windows 설치 파일 빌드·검증·업로드

## 현재 범위

MVP는 단일 Windows 사용자, 로컬 저장, 추가·검색·복사·삭제를 지원한다. 팀 공유, 클라우드 동기화, 키 자동 회전은 명시적으로 제외한다.
