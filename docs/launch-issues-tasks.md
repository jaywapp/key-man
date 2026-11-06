# 공개 준비 이슈 작업

orchestrator: Codex

| 작업 | owner | model | effort | depends_on | parallel_group | files | verification | status |
|---|---|---|---|---|---|---|---|---|
| #10 보안 감사 | Codex | gpt-6.1-sol | high | 계획 | audit-demo | docs/launch-security-review.md, artifacts/검사 | 19커밋·105배포파일·개인 경로 확인, 후속 공개범위승인 완료 | completed |
| #14 데모 제작 | Codex | gpt-6.1-sol | high | 계획 | audit-demo | tools/KeyMan.Demo, tools/Build-Demo.*, docs/assets/demo, docs/demo.md | 26초GIF·MP4 재생성·실제목록응답·프레임·전체디코딩 | completed |
| #11/#13/#15 문서·검증 근거 | Codex | gpt-6.1-sol | high | 기존 릴리스 | docs | README, docs/verification.md, docs/installer.md, docs/alpha-release-notes.md | 릴리스·로컬링크83개·독립문서검토 | completed |
| #12 About·Topics | Codex | gpt-6.1-sol | medium | 이슈 확인 | metadata | GitHub 설정 | 설명·Topics 6개·MIT API readback | completed |
| #16 공개 전환·외부 접근 | Codex | gpt-6.1-sol | high | 사용자 공개승인 | integration | GitHub 설정, README, SECURITY, docs | Public·제보 활성화·익명 문서/미디어/설치 파일·해시 | completed |
| #16 Threads 앱 미리보기 | Codex | gpt-6.1-sol | high | 공개 전환 | integration | docs/threads-launch.md | 공유 메타데이터 확인, 이미지 HTTP429·Work 브라우저 비활성 | blocked |
| 통합·PR·병합·이슈 반영 | Codex | gpt-6.1-sol | high | 모든 독립 작업 | integration | docs/launch-issues-*, GitHub | PR17·CI 통과 및 #10~15 종료, 공개 상태 문서 후속 PR | completed |

Git 변경과 원격 설정은 동일 상태를 공유하므로 루트에서 순차 수행했다. 2026-11-06 사용자의 기존 이력 포함 공개 승인 후 Public·제보 활성화·익명 접근 확인을 완료했다. 실제 Threads 게시 승인은 포함하지 않는다.
