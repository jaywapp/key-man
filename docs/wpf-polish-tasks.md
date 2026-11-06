# WPF 화면 다듬기 작업

orchestrator: Codex

| 작업 | owner | model | effort | depends_on | parallel_group | files | verification | status |
|---|---|---|---|---|---|---|---|---|
| 조사·분석·비교 설계 | Codex | gpt-6.1-sol | high | 없음 | plan | docs/wpf-polish-* | 실제 XAML·기존 작업 확인 | completed |
| 시안 1·공통 프리뷰 | Codex | gpt-6.1-sol | high | plan | concepts | docs/ux-concepts/wpf-polish/프리뷰 프로젝트, concept-01 | WPF 빌드·렌더 | completed |
| 시안 2 | Codex | gpt-6.1-sol | high | plan | concepts | docs/ux-concepts/wpf-polish/concept-02 | WPF 렌더·공통 콘텐츠 | completed |
| 시안 3 | Codex | gpt-6.1-sol | high | plan | concepts | docs/ux-concepts/wpf-polish/concept-03 | WPF 렌더·공통 콘텐츠 | completed |
| 통합 비교·선택 | Codex | gpt-6.1-sol | high | concepts | review | 미리보기, README | 사용자 "1안" 선택 | completed |
| 운영 XAML 적용 | Codex | gpt-6.1-sol | high | 사용자 시안 1 승인 | implementation | src/KeyMan/*.xaml | 기존 명령·필드·공통 스타일 | completed |
| 화면 회귀·렌더링 | Codex | gpt-6.1-sol | high | 사용자 시안 1 승인 | implementation | tests/KeyMan.UiTests, docs/wpf-polish-* | 실제 창 바인딩·입력·여러 크기 | completed |
| 통합 검토·패키징 | Codex | gpt-6.1-sol | high | implementation | verification | docs/assets, artifacts/installer, 결과 문서 | 빌드·GUI·설치 검증 | completed |

## 시안 검증 결과

- 프리뷰 프로젝트 Release 빌드: 경고 0·오류 0.
- WPF 실제 렌더: 기본 1040×680, 작은 창 820×520, 추가 창 500×660, 긴 문자열 820×520. 총 12개 이미지 생성·검토.
- 검색 결과 1개·0개·복원, 선택, 복사 상태 문구, 가짜 항목 삭제·추가 검증 통과. 실제 자격 증명·보관함·클립보드 호출 없음.
- 버튼 hover/pressed 투명도로 인한 대비 저하를 배경색 전환으로 수정. 긴 필드 원문은 툴팁 또는 선택 상세에서 확인한다.
- DataGrid 열 크기의 지연 계산이 이미지에 반영되도록 렌더링 전에 ApplicationIdle까지 처리했다. 최종 작은 창 이미지에서 모든 열 표시를 확인했다.
- 기본·선택·입력 글자 대비와 명시적 키보드 포커스 스타일을 소스 검토했다. 실제 사용자 키보드·스크린리더·고대비 모드·여러 DPI 설정의 검증은 운영 적용 후 수행할 항목이며 완료로 간주하지 않는다.
- 추가 창은 폼만 스크롤하고 저장/취소는 고정한다. 작은 메인 창에서는 목록·상세 영역 스크롤을 허용한다.
- `git diff --check` 통과. 운영 src/KeyMan XAML 및 설치 패키지는 이번 시안 단계에서 변경하지 않았다.
- [시안 비교와 실행 방법](ux-concepts/wpf-polish/README.md). 사용자 선택: 1안 Clear Desk.

## 운영 적용 결과

- App.xaml에 Clear Desk의 색상·글자·버튼·입력·표 스타일을 통합하고 메인/추가 창에 적용했다. 기존 C# 명령·저장·비밀번호 처리 코드는 변경하지 않았다.
- 이름과 메모를 한 셀에 묶고 긴 문자열 툴팁, 선택 상태, 검색 결과 없음/빈 보관함 안내, 접근성 이름과 포커스 표시를 적용했다.
- 추가 창의 폼은 스크롤하고 버튼은 하단에 고정한다. 시각 검토에서 오류 안내가 폼 스크롤에 숨는 문제를 발견해 버튼 위의 고정 영역으로 이동했다.
- `dotnet build KeyMan.sln -c Release`: 경고 0·오류 0. `KeyMan.UiTests --screenshots`: 기존 상태 검사와 실제 XAML 바인딩·PasswordChanged·필수값 검증·오류 문구의 화면 내 배치 검사 총 32개 통과.
- 운영 화면 8종을 렌더링했다: [메인](assets/wpf-clear-desk/main.png), [작은 창](assets/wpf-clear-desk/main-compact.png), [검색 결과 없음](assets/wpf-clear-desk/search-empty.png), [빈 보관함](assets/wpf-clear-desk/vault-empty.png), [긴 텍스트](assets/wpf-clear-desk/long-content.png), [추가](assets/wpf-clear-desk/add.png), [작은 추가 창](assets/wpf-clear-desk/add-compact.png), [필수 입력 오류](assets/wpf-clear-desk/add-error.png). README 이미지를 실제 적용 화면으로 갱신했다.
- 모든 화면은 임시 메타데이터와 고정 가짜 값으로 검증했다. 사용자 보관함·실제 키·클립보드를 조회하지 않았다.
- Clear Desk를 포함한 `KeyMan-Setup-1.1.0-alpha.1.exe` 재생성 완료. 체크섬·MIT 원문 일치·설치된 CLI 가짜 값 등록/조회/삭제·MCP 초기화·사용자 환경 불변·제거 검사 통과. 최종 해시는 [공개 알파 작업 기록](public-alpha-tasks.md)에 기록했다.
- `git diff --check` 통과. 커밋·push·원격 릴리스는 수행하지 않았다.
- 키보드 포커스·스크린리더 이름은 스타일과 계약을 확인했지만 전체 실제 키보드 조작·스크린리더·고대비·다중 모니터 DPI 전환은 검증하지 않았다. 자동 고대비 팔레트 전환은 구현하지 않았다.
