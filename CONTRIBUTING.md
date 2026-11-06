# 기여 안내

KeyMan은 Windows 로컬 API 키 보관함의 공개 알파를 준비하고 있습니다. 버그 재현, 문서 수정, 실제 사용 흐름에 대한 피드백을 환영합니다. KeyMan의 라이선스는 [MIT](LICENSE)입니다. 외부 구성요소의 기존 라이선스·저작권 고지를 보존해 주세요.

## 이슈 작성

사용한 버전 또는 커밋, Windows 버전과 아키텍처, GUI·CLI·MCP 중 실행 경로, 기대 결과와 실제 결과를 적어 주세요. 같은 문제가 반복되는 최소 단계를 가짜 데이터로 작성하면 확인에 도움이 됩니다.

실제 키·토큰·`.env`·개인 `vault.json`·클립보드 내용은 첨부하지 않습니다. 로그와 스크린샷의 이름·서비스·경로도 검토하세요. 보안 문제는 [보안 정책](SECURITY.md)의 비공개 제보 안내를 따릅니다.

## 개발 환경

Windows와 .NET 10 SDK가 필요합니다. 설치 파일 제작에는 Inno Setup도 필요합니다.

```powershell
dotnet restore .\KeyMan.sln
dotnet build .\KeyMan.sln -c Release --no-restore
& .\tools\Test-KeyMan.ps1 -NoBuild
```

검증 스크립트는 저장소·MCP·회귀·클립보드·GUI 테스트를 함께 실행합니다. `-NoBuild`를 생략하면 빌드도 수행합니다. 테스트는 가짜 credential을 사용합니다. 격리용 메타데이터 경로를 지정해도 Windows 자격 증명 관리자가 별도의 보관함으로 격리되는 것은 아닙니다. 개인 보관함을 정리하거나 덮어쓰는 테스트를 추가하지 마세요.

클립보드 수명 검사는 메모리상의 가짜 클립보드로 수행합니다. 네이티브 검사는 별도의 Windows window station 생성에 성공한 경우에만 실행하며, 권한이 없으면 `SKIP`으로 보고합니다. 사용자 클립보드로 대체 실행하지 않습니다. 이 경우 배포 전 별도 테스트 환경에서 네이티브 복사·타이머·종료 정리를 확인해야 합니다.

CI와 릴리스 워크플로는 `KEYMAN_REQUIRE_NATIVE_CLIPBOARD_TESTS=1`로 실행합니다. 이 모드에서는 격리 공간을 만들 수 없으면 실패하여, 네이티브 검증을 생략한 채 발행하지 않습니다.

## 변경 제안

기능이나 저장 형식을 크게 바꾸기 전에 이슈에서 목적과 호환성을 설명해 주세요. 작은 범위의 feature 브랜치에서 기존 코드 스타일을 따르고, 변경에 맞는 검증 결과와 남은 한계를 PR에 적습니다. 커밋 메시지는 `fix:`, `feat:`, `docs:` 같은 Conventional Commits 형식을 사용합니다.

이 저장소의 작업 지침은 [AGENTS.md](AGENTS.md)에 있습니다. 코드·동작 변경은 `docs/<slug>-analysis.md`, `docs/<slug>-design.md`, `docs/<slug>-tasks.md`에 범위·설계·검증 근거를 남깁니다. 생성된 `artifacts`, 빌드 출력, 사용자 데이터, 비밀값은 커밋하지 않습니다.
