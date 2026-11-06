# KeyMan WPF 디자인 비교

현재 앱의 기능과 같은 가짜 메타데이터를 사용한 WPF 시안이다. 사용자가 1안 Clear Desk를 선택했으며 운영 적용 결과는 [작업 기록](../../wpf-polish-tasks.md)에 남긴다. 세 시안 모두 이름·서비스·환경·만료·메모를 확인하고 검색·선택·추가·삭제하는 흐름을 비교한다.

| 시안 | 방향 | 기본 화면 | 작은 창 | 추가 창 |
|---|---|---|---|---|
| 1. Clear Desk (권장) | 밝고 단정한 목록, 기존 동선 유지 | [보기](previews/concept-01.png) | [보기](previews/concept-01-compact.png) | [보기](previews/concept-01-add.png) |
| 2. Night Console | 어두운 바탕, 목록과 선택 상세를 분리 | [보기](previews/concept-02.png) | [보기](previews/concept-02-compact.png) | [보기](previews/concept-02-add.png) |
| 3. Quiet Ledger | 글자와 여백을 강조한 넓은 목록 | [보기](previews/concept-03.png) | [보기](previews/concept-03-compact.png) | [보기](previews/concept-03-add.png) |

## 직접 실행

저장소 루트에서 Windows와 .NET 10 SDK로 실행한다. 마지막 인수를 `concept-02`, `concept-03`으로 바꾸면 다른 시안이 열린다.

```powershell
dotnet run --project .\docs\ux-concepts\wpf-polish\KeyMan.DesignPreview.csproj -- concept-01
```

디자인 비교 전용이다. 실제 키를 입력하지 않는다. 데이터는 메모리에만 있으며 창을 닫으면 사라진다. 실제 Windows 자격 증명·보관함 파일에 접근하지 않는다. 복사는 완료 문구만 보여 주며 클립보드를 변경하지 않는다. 삭제는 가짜 항목을 즉시 지운다. 저장은 메타데이터만 목록에 넣으며 입력한 비밀값을 저장하지 않는다.

렌더링은 프리뷰 프로젝트 자체에서 수행한다. 기본 창 1040×680, 작은 창 820×520, 추가 창 500×660의 콘텐츠를 PNG로 기록한다. 운영 시의 기본 Windows 창 프레임은 이미지에서 제외했다.

```powershell
dotnet run --project .\docs\ux-concepts\wpf-polish\KeyMan.DesignPreview.csproj -c Release -- --render .\docs\ux-concepts\wpf-polish\previews
```

선택한 방향을 운영 프로젝트에 적용할 때는 현재 명령·데이터 바인딩·비밀값 처리와 기존 회귀 테스트를 유지한다. CLI·MCP·설치 흐름을 바꾸지 않는다.
