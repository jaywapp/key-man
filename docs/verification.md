# 검증 결과와 지원 범위

마지막 확인: **2026-11-06**. 배포 대상은 `v1.1.0-alpha.1`, 소스 커밋 `e809f8d`다. 이후 README 문서 변경을 포함한 main `5639d41`도 [Windows CI](https://github.com/jaywapp/key-man/actions/runs/42143897637)를 통과했다.

## 검증 환경

| 구분 | 환경 | 확인한 범위 |
|---|---|---|
| 릴리스 CI | Windows Server 2025 x64, OS build 26100, SDK 10.0.403 | 전체 자동 검사, 패키징, 사용자 PATH·KEYMAN_HOME 등록, 설치·실행·제거 |
| 배포 런타임 | .NET Core/Desktop 10.0.14 포함 | SDK를 요구하지 않는 self-contained GUI·CLI·MCP |
| 로컬 | Windows x64 build 26200, SDK 10.0.400 | WPF 렌더링, 가짜 데이터 시나리오, 다운로드한 설치 파일의 격리 설치·제거 |

[릴리스 워크플로와 로그](https://github.com/jaywapp/key-man/actions/runs/42142621668), [배포된 파일](https://github.com/jaywapp/key-man/releases/tag/v1.1.0-alpha.1).

## 실행 결과

| 대상 | 결과와 범위 |
|---|---|
| Release 빌드 | 경고 0·오류 0 |
| 메타데이터 | 39개: 손상 파일, 최신 상태 병합, 실패 복구, 프로세스 간 동시 변경 |
| 클립보드 수명 | 메모리 fixture 14개, 격리된 Win32 window station 9개 |
| GUI | 32개: 상태·검색·선택, 실제 XAML 바인딩, 비밀 입력 변경, 필수값 오류의 가시성 |
| importer | 0/1/여러 항목, UTF-8 stdin, 중복·손상 보존, CLI 동시 실행 |
| CLI·MCP | 고유 가짜 credential 등록·조회·삭제, MCP 응답에 비밀값 미포함, 실패 복구 |
| 설치·제거 | 체크섬·MIT 원문 일치, 설치된 CLI·MCP 실행, 사용자 환경 등록·제거, 데이터 보존 설계 |
| 시각 검토 | 실제 운영 XAML의 기본·작은 창·빈 상태·긴 텍스트·입력 오류 렌더링 |

GUI 검사는 자동 바인딩 검사와 렌더링 검토다. 모든 사용자 수동 조작을 검증했다는 뜻은 아니다. 로컬에서 권한 때문에 생략된 네이티브 클립보드 검사는 원격 CI에서 필수로 통과했으며, 실제 사용자 클립보드로 대체하지 않았다.

## 다시 실행하기

Windows와 .NET 10 SDK에서:

```powershell
& .\tools\Test-KeyMan.ps1
& .\tools\Build-Installer.ps1 -Version '1.1.0-alpha.1'
& .\tools\Test-Installer.ps1 -InstallerPath '.\artifacts\installer\KeyMan-Setup-1.1.0-alpha.1.exe'
```

패키징에는 Inno Setup이 필요하다. `-RegisterEnvironment` 설치 검사는 개인 PC 대신 깨끗한 CI 러너에서 수행한다. 다운로드한 실제 파일의 SHA-256은 `a473da6d53e0f01962770a451daf18103b170135c32a1680e55a95996f5998c6`이며 자산 크기는 176,443,024 bytes다. 코드 서명은 `NotSigned`다.

CI와 동일하게 네이티브 클립보드 검사를 필수화하려면 private window station 생성 권한이 있는 격리 환경에서 `KEYMAN_REQUIRE_NATIVE_CLIPBOARD_TESTS=1`을 설정한다. 개인 PC에서 권한 부족으로 생략되면 성공한 네이티브 검사로 간주하지 않는다.

## 알려진 제한과 미검증 범위

- Windows x64 알파다. ARM64 네이티브 빌드, 모든 Windows 버전, 별도 클린 VM 전체 조합은 검증하지 않았다.
- 전체 키보드·스크린리더·고대비·다중 모니터 DPI 전환 수동 검증은 완료하지 않았다.
- GUI·MCP 클립보드 정리는 다른 프로그램의 점유나 강제 종료로 실패할 수 있다. 기록·동기화 사본은 삭제하지 않는다. CLI `copy`는 자동 삭제하지 않는다.
- CLI `get`은 비밀값을 표준 출력으로 반환한다. 메타데이터는 평문이며 같은 사용자 권한의 프로그램으로부터 값을 격리하지 않는다.
- 클라우드 동기화·팀 공유·자동 키 회전·자동 업데이트·완전한 백업/복원은 지원하지 않는다.
- GitHub Actions의 현재 상태 배지는 비공개 저장소에서 외부 접근을 보장하지 않으므로 공개 전환 전에는 README에 넣지 않는다. 실제 성공한 실행 링크를 사용한다.

보안 경계는 [SECURITY.md](../SECURITY.md), 설치와 업데이트는 [설치 안내](installer.md)를 따른다.
