# 공개 알파 작업

orchestrator: Codex

사용자가 승인한 검토 제안을 구현한다. 담당별 파일 소유권을 분리해 병렬 진행하며 통합 검증은 모든 변경 후 순차 수행한다.

| 작업 | owner | model | effort | depends_on | parallel_group | files | verification | status |
|---|---|---|---|---|---|---|---|---|
| 계획·작업 브랜치 | Codex | gpt-6.1-sol | high | 없음 | plan | docs/public-alpha-* | 기준 브랜치·승인 범위 | completed |
| 데이터 보존·importer·MCP 표기 | Codex | gpt-6.1-sol | high | plan | implementation | MetadataStore, MainViewModel, CLI Program, KeyManTools, tools/Import-Env, 기존 테스트 소스 | 동시 변경·오류·importer 회귀 테스트 | completed |
| 런타임·CI·설치 패키징 | Codex | gpt-6.1-sol | high | plan | implementation | csproj, sln, global.json, .github/workflows, installer, tools/빌드검증 | .NET 10 빌드·설치 파일 | completed |
| README·사용 안내·Threads 초안 | Codex | gpt-6.1-sol | high | plan | implementation | README, SECURITY, CONTRIBUTING, docs/사용 문서 | 실제 구현과 정합성·링크 | completed |
| 클립보드 수명·데모·통합 | Codex | gpt-6.1-sol | high | plan | implementation | UiServices, WindowsClipboard, 새 클립보드 테스트, docs/assets | 가짜 데이터 테스트·화면 검증, native 미검증 별도 기록 | completed |
| 전체 검증·검토·결과 기록 | Codex | gpt-6.1-sol | high | implementation | integration | docs/public-alpha-* | 실행 가능한 로컬 검사·설치·diff·시크릿 검사, 제한 사항 기록 | completed |
| 네이티브 클립보드·원격 CI | Codex | gpt-6.1-sol | high | 사용자 요청에 따른 push 이후 | release | .github/workflows, ClipboardTests | 격리 station의 실제 Win32 복사·삭제 | pending |
| MIT 반영·패키징 검증 | Codex | gpt-6.1-sol | medium | 사용자 MIT 승인 완료 | license | LICENSE, README, CONTRIBUTING, docs, installer/KeyMan.iss, tools/Test-Installer.ps1 | 설치된 원문 해시 일치·격리 설치/제거 | completed |

MIT 후속 작업은 원문 추가 → 문서·패키지 반영 → 재패키징·설치 검증으로 순차 진행한다. 기존 문서와 패키징 상태를 공유하고 앞 단계에 의존하므로 루트 Codex가 직접 수행한다.

## 외부 게시 전 남는 작업

사용자가 커밋/push/병합/Release 업데이트/README 갱신을 승인했다. Codex가 순차적으로 커밋 → push/PR → CI → 병합 → 수동 알파 발행 → 다운로드 검증 → 문서 PR을 처리한다. 같은 Git 상태와 이전 단계 결과에 의존하므로 원격 변경은 루트 세션에서 순차 수행한다. 저장소 공개 전환은 별도 범위로 확인하고 Threads 원고는 게시하지 않는다.

| 후속 작업 | owner | model | effort | depends_on | parallel_group | files | verification | status |
|---|---|---|---|---|---|---|---|---|
| 커밋·push·PR·원격 CI | Codex | gpt-6.1-sol | high | 사용자 배포 승인 | release | 전체 변경 | 시크릿 검사·CI 통과 | in_progress |
| 병합·알파 발행 | Codex | gpt-6.1-sol | high | CI | release | main, Release 자산 | 설치·체크섬·태그 | pending |
| 배포 상태 문서 정리 | Codex | gpt-6.1-sol | high | Release 발행 | release | README, docs/installer, SECURITY 등 | 실제 링크·문구·문서 PR | pending |

## 검증 결과 (2026-11-06)

- 환경: Windows x64, OS build 26200, SDK 10.0.400. 소스 테스트는 설치된 .NET 10.0.11에서, 배포물은 self-contained .NET 10.0.14에서 확인했다.
- `tools/Test-KeyMan.ps1`: Release 빌드 경고 0·오류 0. 메타데이터 39개, 클립보드 수명 14개, GUI 상태 13개 검사 통과. 저장소·MCP 스모크 통과.
- importer: 0·1·여러 항목, 한글 UTF-8 stdin, 중복 보존, 손상 메타데이터 보존, 독립 CLI 6프로세스 동시 등록 통과. 메타데이터 테스트도 독립 4프로세스를 검증했다.
- MCP: 성공 응답의 비밀값 미포함, 강제 교체·삭제의 저장 실패 시 기존 자격 증명 복원, 파괴적 도구 표기 검증 통과.
- GUI: 초기 로드 실패 후 쓰기 차단, 외부 등록·삭제 뒤 GUI 저장 시 변경 보존, 검색 상태 검증 통과.
- 교차 리뷰에서 발견한 clipboard write/sequence 획득 사이 경쟁 조건을 Win32 배타 구간으로 수정했다. 종료 정리 실패 시 GUI는 고정 문구로 안내한다.
- 네이티브 클립보드 검사는 격리 window station 생성의 Windows 접근 거부로 생략했다. 실제 사용자 클립보드로 대체하지 않았다. CI/릴리스는 `KEYMAN_REQUIRE_NATIVE_CLIPBOARD_TESTS=1`로 이 검사를 필수화했으며 원격 실행은 아직 하지 않았다.
- `docs/assets/keyman-demo.png`: 사용자 선택 1안 Clear Desk가 적용된 실제 운영 XAML을 가짜 메타데이터 3개로 렌더링(1040×680), 시각 검토 완료. 화면 녹화가 아니며 개인 키·경로를 포함하지 않는다.
- WPF 후속 개선: 솔루션 빌드 경고 0·오류 0, 실제 창 바인딩·입력·유효성 안내 포함 GUI 검사 32개 통과. [적용 결과](wpf-polish-tasks.md).
- `tools/Build-Installer.ps1 -Version 1.1.0-alpha.1`: 최종 설치 파일 및 SHA-256 생성. .NET Core/Desktop runtime 10.0.14 확인, runtime 의존성/pack 33개의 원문 고지와 버전·출처 목록 포함.
- `tools/Test-Installer.ps1`: 체크섬, 격리 설치, 설치된 CLI의 가짜 값 등록·목록·조회·삭제, 설치된 MCP 초기화/도구 목록, 사용자 PATH/KEYMAN_HOME 불변, 제거 검증 통과. 실제 등록 옵션은 원격의 깨끗한 사용자에서 검증하도록 구성했으며 로컬에서는 실행하지 않았다.
- MIT 승인 후 루트 `LICENSE`, README·기여 안내·릴리스 노트를 일치시켰다. 설치 폴더에 `LICENSE.txt`를 포함한다. 이후 Clear Desk WPF 변경까지 재패키징했으며 저장소 원문과 설치된 원문의 SHA-256 일치, CLI·MCP·환경 불변·제거 검증을 다시 통과했다.
- 설치 파일: `artifacts/installer/KeyMan-Setup-1.1.0-alpha.1.exe`, 176,443,565 bytes, Authenticode `NotSigned`.
- SHA-256: `2cc843f44e7c35643de2f1ae195a2b0aec9ad9c1808412b26dd4df215b2e48cc`.
- NuGet 직접·전이 패키지 취약점 감사: 검출 없음. Gitleaks 8.30.1: 전체 Git 이력 11커밋과 변경·신규 공개 대상 파일에서 검출 없음. 알려지지 않은 취약점이나 모든 개인정보 부재를 보장하는 감사는 아니다.
- 문서 로컬 링크 오류 0, Threads 본문 307자·댓글 288자, `git diff --check` 통과.
- 사용자 실제 vault·키 조회/변경, 커밋/push/PR, 저장소 공개 전환, 릴리스/Threads 발행은 하지 않았다.

## 남은 공개 조건

MIT 라이선스는 사용자 승인으로 확정했다. 원격 필수 CI(네이티브 클립보드 포함), 보안 제보 채널 활성화, 공개 저장소/다운로드의 비로그인 접근 확인이 필요하다. 코드 서명은 구성되지 않았으며 알파 안내에 명시했다. 클린 VM·ARM64·모든 Windows 버전 및 전체 사용자 수동 흐름은 검증하지 않았다.
