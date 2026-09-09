# 전체 빌드 및 로컬 경계 검증 작업

orchestrator: Codex

| 작업 | owner | model | effort | depends_on | parallel_group | files | verification | status |
|---|---|---|---|---|---|---|---|---|
| 규칙 및 기존 상태 조사 | Codex | gpt-6-astra | high | 없음 | dotnet-build | 규칙/README | 읽기/Git 상태 | completed |
| 전체 솔루션 빌드 | Codex | gpt-6-astra | high | 조사 | dotnet-build | 기존 솔루션 | 실제 빌드 요약 | completed |
| 안전한 경계 테스트 보강 | Codex | gpt-6-astra | high | 조사 | dotnet-tests | 기존 테스트 | 로컬 테스트 실행 | completed |
| 결과 검토 및 문서 갱신 | Codex | gpt-6-astra | high | 테스트 | sequential | docs/build-verification-* | diff 검사 | completed |

상위 Codex 세션의 저장소별 병렬 위임으로 진행한다. 이 담당 그룹 내부는 테스트 소스 확인→수정→검증 의존성 때문에 순차 진행하며 독립 빌드 명령만 병렬 실행한다. 추가 하위 위임 없음.

## 실제 실행 결과 (2026-09-09)

- `dotnet build KeyMan.sln -c Release --nologo`: 종료 코드 0, 경고 0, 오류 0. 기존 패키지 NuGet 복원을 허용한 실행으로 이전 미확인 결과를 해소했다.
- `dotnet run --project tests/RegressionTests/RegressionTests.csproj --no-restore`: PASS 11 metadata regression checks (기존 8 + 신규 3)
- 추가 검사: 읽기 전용 메타데이터 쓰기 권한 오류, 실패 후 기존 데이터 보존, 빈 파일의 JsonException을 추가했다.
- `git diff --check`: 통과. 줄바꿈 변환 안내는 검사 실패가 아니다.
- 운영 코드/패키지 버전 변경, commit/push/merge/배포 없음.

환경 요건/한계: KeyMan.SmokeTests와 KeyMan.McpTests는 WindowsCredentialStore를 실제 호출하므로 빌드만 하고 실행하지 않았다. 실제 자격 증명/클립보드/GUI 통합은 미검증이다.

completed는 위 로컬 범위의 완료다. 외부 연동을 포함한 모든 기능 검증을 완료했다는 뜻이 아니다.
