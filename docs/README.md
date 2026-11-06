# KeyMan 문서

이 저장소의 `docs/`는 사용 안내와 프로젝트 작업 기록의 기준 위치입니다. 과거 분석·설계 문서는 작성 당시의 상태를 설명하므로 현재 사용법은 아래 안내를 우선합니다.

## 사용 안내

- [설치·업데이트·제거](installer.md)
- [CLI와 .env 가져오기](cli.md)
- [MCP 연결과 보안 경계](mcp.md)
- [아키텍처](architecture.md)
- [보안 정책](../SECURITY.md) · [기여 안내](../CONTRIBUTING.md)
- [Threads 게시 초안·데모·공개 전 점검](threads-launch.md)
- [공개 알파 릴리스 안내 원고](alpha-release-notes.md)

## 작업 기록

- WPF 화면 다듬기: [분석](wpf-polish-analysis.md), [설계](wpf-polish-design.md), [작업](wpf-polish-tasks.md)

- 공개 알파 준비: [분석](public-alpha-analysis.md), [설계](public-alpha-design.md), [작업 및 검증](public-alpha-tasks.md)
- 성능·안정성 작업: [분석](performance-resilience-analysis.md), [설계](performance-resilience-design.md), [작업](performance-resilience-tasks.md)
- 전체 빌드·로컬 검증: [분석](build-verification-analysis.md), [설계](build-verification-design.md), [작업 및 결과](build-verification-tasks.md)

## 문서 작성 규칙

- 요구사항이 부족하거나 모호하면 구현 전에 질문을 모아 확인하고, 답변·근거·가정을 `*-analysis.md`에 기록합니다.
- 확정 설계는 `*-design.md`, 실행 순서는 `*-tasks.md`에 기록합니다.
- `*-tasks.md`의 모든 작업에는 `orchestrator`, `owner`, `model`, `effort`, `depends_on`, `parallel_group`, `files`, `verification`, `status`를 지정합니다.
- 독립 작업은 같은 오케스트레이터의 서브에이전트로 병렬 실행하고, 공유 파일·스키마를 변경하는 작업은 순차 실행합니다.
- 웹 페이지나 주요 UX/UI 변경은 `docs/ux-concepts/<slug>/concept-01..03`의 서로 다른 콘셉트 샘플을 먼저 제시하고 사용자 선택을 받은 뒤 구현합니다.
- UX/UI 작업에는 `impeccable`과 `design-taste-frontend` 스킬을 적용합니다.
- Vercel은 웹 프리뷰·호스팅 기본값이며, 동적 데이터·인증·스토리지는 Supabase를 우선 검토합니다. 실제 배포는 명시적 요청이 있을 때만 수행합니다.
- 시크릿·토큰·키·`.env` 값은 문서·로그·커밋에 기록하지 않습니다.

프로젝트 작업 규칙의 원본은 [AGENTS.md](../AGENTS.md)입니다. 기존 작업 기록은 보존하고, 변경된 동작은 현재 사용 안내와 해당 작업 문서에 함께 반영합니다.
