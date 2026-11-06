# 공개 준비 이슈 작업

orchestrator: Codex

| 작업 | owner | model | effort | depends_on | parallel_group | files | verification | status |
|---|---|---|---|---|---|---|---|---|
| #10 보안 감사 | Codex | gpt-6.1-sol | high | 계획 | audit-demo | docs/launch-security-review.md, artifacts/검사 | 19커밋·105배포파일·개인 경로 확인, 공개범위승인 별도 | completed |
| #14 데모 제작 | Codex | gpt-6.1-sol | high | 계획 | audit-demo | tools/KeyMan.Demo, tools/Build-Demo.*, docs/assets/demo, docs/demo.md | 26초GIF·MP4 재생성·실제목록응답·프레임·전체디코딩 | completed |
| #11/#13/#15 문서·검증 근거 | Codex | gpt-6.1-sol | high | 기존 릴리스 | docs | README, docs/verification.md, docs/installer.md, docs/alpha-release-notes.md | 릴리스·로컬링크83개·독립문서검토 | completed |
| #12 About·Topics | Codex | gpt-6.1-sol | medium | 이슈 확인 | metadata | GitHub 설정 | 설명·Topics 6개·MIT API readback | completed |
| #16 Threads 최종 준비 | Codex | gpt-6.1-sol | high | audit-demo, docs, metadata | integration | docs/threads-launch.md | 원고·미디어·피드백양식 완료, 공개승인과외부검증 대기 | blocked |
| 통합·PR·병합·이슈 반영 | Codex | gpt-6.1-sol | high | 모든 독립 작업 | integration | docs/launch-issues-*, GitHub | 시크릿·링크·CI·병합 결과 | in_progress |

Git 변경과 원격 설정은 동일 상태를 공유하므로 루트에서 순차 수행한다. Public 전환 및 익명 접근 확인은 최종 승인에 의존한다.
