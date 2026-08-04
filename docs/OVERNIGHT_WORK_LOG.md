# 한키 Windows 0.2.0-dev 작업 기록

## 2026-07-26 격리 Release 로그아웃·정리 완료

- 사용자 Chrome에서 완료한 Google 로그인 세션을 격리 Release 계정 탭에서 확인하고 로그아웃 실행: **PASS**. 표시명/이메일/fallback 아바타는 확인했으나 개인정보는 기록하지 않았다.
- 로그아웃 후 격리 `auth.session` 삭제: **PASS**. DPAPI 세션은 평문 JSON으로 해석되지 않았고 민감 로그는 0건이었다.
- 격리 `hanki.db`(24576 bytes)와 기존 단축키 목록 유지: **PASS**.
- 격리 Release 정상 종료 → 동일 격리 데이터로 재실행: **PASS**. 세션 파일 없음, 브라우저 자동 실행 없음, 43289 LISTEN 없음.
- 트레이/창 복원 및 빠른 재실행 단일 인스턴스 회귀: **PASS**. 격리 프로세스는 1개로 유지되었다.
- 실제 앱 취소 버튼과 실제 43289 포트 충돌 UI는 Computer Use가 물리 Escape로 안전 중단되어 **자동화 불가**. 기존 자동 listener 테스트(취소/timeout/충돌 포함)는 123/123 **PASS**.
- 격리 Release와 Portable 프로세스는 `CloseMainWindow()`로 정상 종료했다. 설치된 0.1.1 PID 10080과 실행 경로는 시작 전후 동일하게 보존했다.
- 최종 Debug/Release build·test, diff check, 0.1.1 SHA-256, package/secret 검사는 모두 **PASS**. ZIP/설치본 서명은 NotSigned로 확인.
- commit/push/tag/Release/Vercel 배포/Supabase migration은 실행하지 않았다.

## 2026-07-26 03:47:15 +09:00 — 작업 시작

- 완료한 작업: 요청 범위와 금지 사항 확인, 장시간 작업 계획 수립
- 변경한 파일: `docs/OVERNIGHT_WORK_LOG.md` 생성
- 실행한 명령: 첨부 요청 확인, 작업 시각 확인
- 테스트 결과: 아직 실행 전
- 발견한 문제: 없음
- 보류한 문제: 실제 Google 로그인, production 배포·migration 등 사용자 또는 외부 설정이 필요한 항목
- 다음 작업: 두 저장소 안전 스냅샷과 백업, 기존 0.1.1 배포 파일 보존값 확인

## 2026-07-26 03:50 +09:00 — 안전 스냅샷 및 백업

- 완료한 작업: 두 저장소의 브랜치·상태·diff·untracked·마지막 커밋 확인, Windows 버전과 89개 테스트 확인, 웹 script와 환경변수 이름만 확인, 미커밋 파일 별도 백업
- 변경한 파일: 백업 디렉터리의 상태·patch·untracked 목록·배포 해시 파일 7개와 untracked 사본
- 실행한 명령: `git branch --show-current`, `git status --short`, `git diff --stat`, `git diff --binary`, `git ls-files --others --exclude-standard`, `git log -1`, 배포 파일 SHA-256 계산
- 테스트 결과: 아직 실행 전
- 발견한 문제: Windows 저장소는 0.2.0-dev 인증·브랜딩 작업과 89개 테스트가 이미 미커밋 상태이고, 웹 저장소도 한키 기능 변경과 migration 제안이 미커밋 상태
- 보류한 문제: 없음
- 백업 경로: `C:\Users\MSI\Desktop\개발\_hanki_work_backup\20260726-034715`
- 0.1.1 기준 SHA-256: ZIP `334530975AD50268765FD5641F053104AABE9E074A55B7FBCEA95F7ABFC61681`, 설치본 `4DBA2D30E174FCAD3C646EEB7A77E8C70959EA2D695FFCFBA3B2B3D0786FAFE1`, 체크섬 파일 `38C89D87F220B43034E3DAEB6D6C940872871324918414CD629C23716BFBEEB1`
- 다음 작업: 기존 로고·OAuth·계정 UI·테스트·패키징 구조 독립 검수

## 2026-07-26 03:55 +09:00 — 기준선 테스트와 1차 독립 검수

- 완료한 작업: 기존 로고·앱 아이콘 적용·OAuth/PKCE·loopback·DPAPI·계정 UI·웹 변경 구조 검토, Windows와 웹 기준선 테스트 실행
- 변경한 파일: 작업 로그만 갱신
- 실행한 명령: `dotnet test HankiWindows.sln -c Debug`, `npm test`, 관련 소스·XAML·installer·SVG 검토
- 테스트 결과: Windows 89/89 통과, 웹 18/18 통과
- 발견한 문제: loopback이 HTTP method를 검증하지 않음, OAuth error의 state 검증 순서 미흡, redirect 설정이 고정 포트·경로를 강제하지 않음, 브라우저 실행/세션 저장 실패 자원 정리 부족, 계정 ViewModel의 UI continuation이 background thread로 이동할 수 있음, 세션 복원 표시 상태 없음, 설정 개인정보 문구가 인증 네트워크 통신과 불일치, installer가 아직 0.1.1 출력 경로를 가리킴
- 보류한 문제: 실제 Google 계정 로그인은 외부 설정과 사용자 조작 필요
- 다음 작업: 로고 contact sheet·ICO 검증 후 OAuth·UI·installer 보강과 신규 테스트 추가

## 2026-07-26 03:51 +09:00 — 로고 자산 생성·육안 검증

- 완료한 작업: 단일 SVG 원본에서 앱·트레이·설치 프로그램용 PNG/ICO 자산을 재생성하고, 밝은/어두운 배경 및 16–1024px 크기를 한 장에서 확인할 수 있는 contact sheet 생성
- 변경한 파일: `tools/render-logo-assets.mjs`, `assets/branding/hanki-logo-small.svg`, `assets/branding/hanki-logo*.png`, `assets/branding/hanki-logo.ico`, `assets/branding/hanki-installer-*.png`
- 실행한 명령: `node tools/render-logo-assets.mjs`, `python tools/pack-ico.py`, Pillow 기반 이미지 크기·ICO 프레임 검사
- 테스트 결과: ICO에 16/20/24/32/40/48/64/128/256px 프레임 포함, preview 1024×1024, contact sheet 1800×1420, 설치 이미지 164×314 및 55×55 확인
- 발견한 문제: 기존 소형 SVG에서는 16–32px에서 화살표 대비가 약함
- 해결: 소형 변형의 화살표에 어두운 keyline을 추가해 밝고 어두운 배경 모두에서 식별 가능하게 조정
- 보류한 문제: 실제 Windows 작업 표시줄·알림 영역·설치 마법사 렌더링은 패키징 후 최종 확인 필요
- 다음 작업: OAuth 콜백·설정·세션 저장 경계 조건 보강과 회귀 테스트 추가

## 2026-07-26 04:05 +09:00 — OAuth·세션 보안 및 계정 UI 보강

- 완료한 작업: PKCE loopback 설정을 `127.0.0.1:43289/auth/callback`으로 고정, service-role/secret 키 구성 차단, state 우선 검증, GET 외 요청 거부, 취소·오류 시 리스너 종료 대기, HTTPS avatar 제한, DPAPI 세션 원자 저장과 손상 파일 정리, 저장·읽기·삭제 실패 상태 복구 구현
- 변경한 파일: `src/Hanki.Core/Authentication/AuthenticationState.cs`, `AuthErrorCode.cs`, `src/Hanki.Infrastructure/Authentication/*`, `src/Hanki.App/Services/AuthenticationUiCoordinator.cs`, `src/Hanki.App/ViewModels/AccountViewModel.cs`
- 계정 UI 변경: 복원·로그인 indeterminate progress, Enter 로그인/Escape 취소, 버튼 키보드 focus 표시, 긴 이름·이메일 말줄임/tooltip, 접근성 이름/live region, 실제 네트워크 동작과 로컬 단축어 보존을 구분한 개인정보 문구, 0.2.0-dev 버전 및 새 로고 표시
- 변경한 UI 파일: `src/Hanki.App/App.xaml`, `MainWindow.xaml`, `MainWindow.xaml.cs`, `Hanki.App.csproj`, `App.xaml.cs`
- 추가·수정한 테스트: callback parser/config/service/store 테스트와 실제 `HttpListener` 통합 테스트; 정상 콜백, 잘못된 경로·POST, state 오류, 취소·timeout 후 재시도, 포트 충돌, 브라우저 실패, 저장/읽기/삭제 실패, HTTP avatar 차단
- 실행한 명령: `dotnet test HankiWindows.sln -c Debug --no-restore`, `dotnet build src/Hanki.App/Hanki.App.csproj -c Debug --no-restore`
- 테스트 결과: 117/117 통과, 앱 빌드 경고 0·오류 0
- 발견·해결한 문제: WPF/WinForms의 `KeyEventArgs` 이름 충돌을 완전 수식 이름으로 해소
- 보류한 문제: 실제 Google 계정 로그인과 설치된 앱 UI 육안 점검은 사용자 조작/승인이 필요
- 다음 작업: 개발용 설치·portable 패키지 경로 분리, 전체 기능·성능·보안·의존성 검사

## 2026-07-26 04:10 +09:00 — Windows 패키징·의존성·격리 실행 검사

- 완료한 작업: `dist-dev/0.2.0` 전용 Release single-file publish, Inno Setup 개발 스크립트·설치 마법사 이미지 적용, ZIP/설치본 생성, EXE 아이콘 추출, Defender/secret/절대경로/PDB 검사
- 변경한 파일: `installer/Hanki.Dev.iss`, `installer/BUILD_INSTALLER.md`, `src/Hanki.Infrastructure/Hanki.Infrastructure.csproj`, `src/Hanki.Infrastructure/AppPaths.cs`, `src/Hanki.App/App.xaml.cs`, 창 XAML 4개, `tests/Hanki.Core.Tests/AppPathsTests.cs`
- 발견·해결한 문제 1: `SQLitePCLRaw.lib.e_sqlite3 2.1.6` High 취약점(GHSA-2m69-gcr7-jv3q)을 기존 계열 `SQLitePCLRaw.bundle_e_sqlite3 2.1.12` 직접 고정으로 해소; 재검사 0건
- 발견·해결한 문제 2: 실제 앱 격리 실행에서 ICO `Window.Icon` 로딩이 `XamlParseException(IOException)`을 발생시킴. EXE·트레이·설치본은 ICO를 유지하고 WPF 창은 동일 공식 로고 PNG resource로 변경; 격리 실행 종료 코드 0 확인
- 실행 안정성: 실제 사용자 설치본 PID를 종료·활성화하지 않고 임시 데이터 경로와 별도 instance namespace 사용. Debug 20/20, Release 20/20 정상 종료, 비정상 exit 0, 잔류 프로세스 0. Release 빠른 10회 실행은 700ms 시점 프로세스 1개·최종 0개
- 단기 측정: Debug 시작 평균 383.44ms(352.20–455.93), working set 평균 55.10MB, handle 평균 274, thread 평균 13.05. Release 시작 평균 461.37ms(380.98–1658.66), working set 평균 53.92MB, handle 평균 273.35, thread 평균 13
- 보안 결과: Defender 활성/실시간 보호 활성, signature `1.455.343.0`, 개발 패키지 탐지 0. PDB·실제 config·`.env.local` 0, 개발 절대경로 문자열 파일 0, 의심 secret text match 0
- 보류한 문제: 코드 서명은 `NotSigned`; 실제 설치·제거·업그레이드는 동일 AppId의 사용자 설치를 바꿀 수 있어 사용자 승인 필요
- 다음 작업: 15분 유휴 측정 완료 확인, 웹 로고·인증·migration 회귀와 전체 웹 빌드

## 2026-07-26 04:17 +09:00 — Yulbyte 웹 로고·인증 회귀

- 완료한 작업: Windows 로고와 웹 PNG SHA-256 일치 확인, 홈 제품 카드와 `/hanki`에서 고정 public asset·`next/image`·`alt="한키 앱 로고"` 적용, fallback 파일 검사 제거
- 정확성 수정: 미적용 migration을 전제로 노출되던 단축어 관리 UI를 계정 페이지에서 비활성화하고, 웹/Windows/Chrome 단축어가 동기화되지 않는다는 문구로 통일. migration·제안 컴포넌트는 삭제하지 않고 보존
- 인증 보강: HTTPS avatar만 허용, Supabase URL은 HTTPS origin 또는 개발용 loopback HTTP만 허용, credential/query/fragment/path 거부, `sb_secret_`·service-role key 거부
- 의존성: Next 내장 runtime `postcss`를 8.5.23, `sharp`를 0.35.3으로 최소 override해 관련 High 감사 항목 해소. ESLint 개발 체인의 `brace-expansion 1.1.16` 9건은 비파괴적 1.x 패치가 없어 ESLint 10 강제 업그레이드 보류
- 실행한 명령: `npm test`, `npm run typecheck`, `npm run lint`, `npm run build`, `npm audit`, `npm outdated`, `npm run check:links`, 로컬 Next production 서버 브라우저 검사
- 테스트 결과: 20/20, TypeScript 오류 0, lint 오류 0, production build 성공(`/`, `/hanki`, `/account`, `/auth/callback` 포함), 내부 링크 9 routes/23 files 통과
- 브라우저 결과: `/hanki` 1265px·375px overflow 0, 로고 96×96 로드·왜곡 없음, 홈 모바일 로고 48×48, `/account` 모바일 overflow 0, console error/warning 0, 잘못된 “다른 기기 관리” 문구 0
- 보류한 문제: 실제 Google 웹 로그인은 실계정 미실행, production migration·배포 미실행
- 다음 작업: migration gap analysis와 릴리스 문서, 15분 측정·최종 패키지·전체 검증

## 2026-07-26 04:31 +09:00 — 최종 Windows 검증·패키징

- 완료한 작업: cloud sync gap/인증/브랜드/성능/의존성/테스트/릴리스 준비 문서 작성, clean·restore·Debug/Release 검증, 최종 source 상태로 portable·설치본 재생성
- 실행한 명령: Debug/Release `dotnet clean`, `dotnet restore`, Debug/Release `dotnet build`, Debug/Release `dotnet test --no-build`, `dotnet format --verify-no-changes`, `git diff --check`, `dotnet publish`, Inno Setup 6.7.3, `Compress-Archive`, Defender custom scan
- 테스트 결과: Windows Debug 123/123, Release 123/123, build 경고 0·오류 0, format/diff 검사 통과
- Portable 결과: 임시 데이터·별도 instance namespace에서 실제 최종 `Hanki.exe` 시작/종료, exit code 0, 잔류 진단 프로세스 0; 실행 중인 사용자 설치본은 계속 유지
- 최종 SHA-256: EXE `5F0CA4FFF78B501961355DDA5E74C0B4EE76AC61584AA7C54BCA66706BEC084B`, ZIP `4856BCA464DAF757A3FF1B87DC6656BEBB722F8BD367DC1426651BB694B8FEA2`, 설치본 `26BBE5E37B1F12A0198CE82C117D84DB38FA6E7184CEBD082C383A2E652B8692`
- 보안 결과: 최종 패키지 Defender 탐지 0, PDB/실제 config/.env/절대 개발 경로 0, EXE·설치본 `NotSigned`
- 0.1.1 보존: ZIP·설치본·체크섬 파일 SHA-256과 수정 시각 모두 작업 전 기준과 일치
- 보류한 문제: 실제 Google 로그인, 동일 AppId 설치·제거·업그레이드, SmartScreen·DPI 육안 확인은 사용자 승인/설정 필요; 15분 handle 739→861 증가는 장기 후속 관찰 필요
- 다음 작업: 최종 저장소 상태·문서 링크·금지 작업 미실행 여부 정리

## 2026-07-26 — 실제 Google 로그인 환경 설정·수동 검증 시작

- 완료한 작업: 두 저장소의 현재 브랜치·상태·diff·untracked·마지막 커밋 재확인, 인증 구현·설정 예제·Git 제외 규칙·실행 중 Hanki·고정 callback 포트·0.1.1 배포 파일 기준선 확인
- 현재 상태: HankiWindows와 yulbyte-site 모두 `main`; 설치된 사용자 Hanki `PID 10080`은 종료하거나 활성화하지 않고 보존; `127.0.0.1:43289`는 초기 상태에서 비어 있음
- 웹 공개 설정: `.env.local`의 `NEXT_PUBLIC_SUPABASE_URL`과 `NEXT_PUBLIC_SUPABASE_PUBLISHABLE_KEY`가 모두 존재하며 원문은 출력하지 않음. URL은 HTTPS Supabase 프로젝트 root origin이고 query/fragment/userinfo/localhost가 없으며, 키는 길이 46의 `sb_publishable_` 형식이고 privileged key가 아님
- 네트워크 확인: 동일 공개 설정으로 Supabase Auth settings endpoint에 TLS 접속하여 HTTP 200 확인
- 설정 구조: 앱은 `AppContext.BaseDirectory\hanki.auth.config.json`에서 `supabaseUrl`, `supabasePublishableKey`, `redirectUri`를 읽고 redirect는 `http://127.0.0.1:43289/auth/callback`만 허용. 실제 설정 파일은 `.gitignore`에서 제외되고 Git index에 추적되지 않음
- 0.1.1 기준선: ZIP `334530975AD50268765FD5641F053104AABE9E074A55B7FBCEA95F7ABFC61681`, 설치본 `4DBA2D30E174FCAD3C646EEB7A77E8C70959EA2D695FFCFBA3B2B3D0786FAFE1`, 체크섬 파일 `38C89D87F220B43034E3DAEB6D6C940872871324918414CD629C23716BFBEEB1`
- 테스트 결과: 자동 테스트 재실행 전
- 발견한 문제: 소스 프로젝트의 예제 설정은 의도대로 빈 값이며, 실제 설정은 기본 빌드에 자동 포함되지 않으므로 로컬 실행과 별도 auth-test Portable의 실행 파일 옆에만 안전하게 생성해야 함
- 보류한 문제: 없음
- 다음 작업: Debug/Release 빌드·테스트 후 각 격리 출력에 실제 공개 클라이언트 설정을 원문 노출 없이 생성하고 Release UI를 실행

## 2026-07-26 0.1.1 → 0.2.0-dev 실제 설치 검증 완료

- 기존 설치·DB·시작 메뉴·제거 등록을 사전 기록하고 `_hanki_upgrade_backup\20260726-161352`로 사용자 데이터를 백업 — **PASS**
- `dist-dev\0.2.0-install-test` 후보를 별도 생성. 공개 Supabase 설정만 포함하고 token/session/service_role/client_secret/PDB/DB/개발 절대경로를 제외 — **PASS**
- 공식 Inno Setup 옵션으로 0.1.1 설치 경로를 0.2.0-dev로 업그레이드(exit code 0). 앱 실행 전 DB SHA-256 및 논리 데이터 보존 확인 — **PASS**
- 설치본 계정 탭 로그인 버튼 활성화, 기존 단축어 표시, 로고, X→트레이, 재실행 창 복원, 빠른 5회 단일 인스턴스 — **PASS**
- 공식 제거 프로그램 사용 후 설치 등록·시작 메뉴·설치 파일 제거 및 사용자 데이터 보존 — **PASS**
- 트레이 숨김 프로세스가 제거 후 남아 정확한 설치 경로·버전의 단일 PID만 마지막 수단으로 종료. 다른 프로세스는 종료하지 않음 — **PASS**
- 동일 후보 재설치 및 최종 0.2.0-dev 설치 상태 확인. Hanki 프로세스 0개, 43289 해제, DB SHA-256 원본과 일치 — **PASS**
- 실제 Google 인증 재시작은 하지 않음 — **미실행** (기존 실제 로그인 PASS 기록 유지)
- 실행 파일 서명 상태 `NotSigned`; 공개 배포 전 코드 서명·SmartScreen 정책은 **사용자 승인 필요**
- commit/push/tag/Release/Vercel 배포/Supabase migration 미실행

## 2026-08-04 자율 유지보수 1차 기록

- 백업: `_hanki_finalization_backup\20260804-184846`에 두 프로젝트 Git 상태/diff/untracked 목록과 사용자 DB 사본을 보관했다. 기존 사용자 데이터와 0.1.1 배포 파일은 변경하지 않았다.
- 자동 검증: Yulbyte `npm test` 20/20, `lint`, `typecheck`, `build`, 내부 링크 9 routes/23 files 통과. Hanki Release test 123/123, Debug test 125/125 통과.
- 보안 보강: 웹과 Windows의 Supabase 설정 검증에서 malformed JWT와 `authenticated`/`supabase_admin` 역할을 publishable key로 허용하지 않도록 테스트와 구현을 보강했다.
- 웹사이트: `/hanki`를 Windows 0.2.0-dev 로컬 검증 상태 중심으로 갱신하고, 공개 다운로드 링크를 만들지 않은 채 코드 서명 검토 상태를 명시했다. Chrome 화면은 참고 자료로 구분했다.
- 문서: 설치 인증 설정 문서가 소스 기본 publish와 install-test 공개 설정 후보를 구분하도록 갱신했다.
- 다음 작업: 로컬 production `/hanki` 시각·반응형·접근성 확인, Windows isolated diagnostic smoke/handle 측정, 최종 보안·diff·문서 검사.

## 2026-08-04 자율 유지보수 2차 기록

- `/hanki` production 로컬 확인: Windows 0.2.0-dev 배지·공개 다운로드 미노출·Chrome 참고 화면 구분, demo 변환 동작, 375px에서 scrollWidth 360px — **PASS**
- 격리 진단 smoke: `dist-dev\soak\20260804-2`, 별도 DB SHA-256 유지, `auth.config.loaded`만 기록, auth.session 미생성, 30초 진단 종료 후 Hanki 잔류 프로세스 0 — **PASS**
- 진단 시작 표본: working set 약 158.7MB, private 약 85.7MB, handle 821. 장시간 45분 soak는 아직 수행하지 않음 — **미실행**
- 이전 15분 관찰에서 handle 739→861 증가가 기록되어 있어 공개 전 사용자 설정/장시간 관찰 blocker로 유지한다.
- 보안 보강을 포함한 별도 Portable 후보 `dist-dev\0.2.0-autonomous-20260804` 생성. EXE SHA-256 `362F41FEA00157A7D2CD2BB07423D3C6DFEB3A2FBBE519EA563AA50ED2504737`, ZIP SHA-256 `9289071673A37BD5FF41A266281478486A4114E059431ADD7E1DB1DE7697316D`, static scan PASS.

## 2026-08-04 자율 유지보수 3차 기록

- 최종 웹 검증 재실행: `npm test` **21/21 PASS**, lint/typecheck/build/link check **PASS**.
- 최종 Windows 검증 재실행: Debug/Release build 경고 0·오류 0, Debug/Release test **125/125 PASS**, `dotnet format --verify-no-changes` 및 `git diff --check` **PASS**.
- 별도 자율 Portable 후보를 격리 데이터 디렉터리로 실행: 공개 auth config 로딩, 30초 진단 정상 종료, 후보 프로세스 0, `auth.session` 미생성, 포트 43289 미청취, DB SHA-256 보존 **PASS**.
- `npm audit --omit=dev`: 취약점 0건 **PASS**. 개발 의존성 audit은 `brace-expansion` High 1건으로 **사용자 설정 필요**.
- 별도 45분 handle/메모리 soak는 계속 실행 중이며 최종 종료 전 상태는 **미실행**으로 보류한다. 설치된 사용자 앱과 DB는 격리하여 변경하지 않았다.

## 2026-08-04 자율 유지보수 4차 기록

- 격리 idle diagnostic에서 45개 샘플(22:23:59~23:08:00, 약 45분)을 수집했다. working set 132.41~161.65MB, private memory 70.69~88.38MB, handles 746~1086 — **PASS (관찰 완료)**.
- 진단 인자 상한이 기존 20분이라 45분 자동 종료가 예약되지 않았음을 확인했다. 사용자 앱이 아닌 정확한 격리 PID 18280만 마지막 수단으로 종료했다 — **PASS**.
- 재발 방지를 위해 diagnostic exit 상한을 1시간으로 확장하고 Debug/Release build 및 test 각 125/125를 재실행 — **PASS**.
- 핸들 최고값이 초기 대비 상승했으므로 실제 입력 훅을 포함한 사용자 시나리오 장시간 검증은 **사용자 설정 필요**로 남긴다.
- 상한 보강 후 Debug 빌드 격리 진단 30초 smoke: exit code 0, 잔류 PID 0, 포트 43289 해제, auth.session 미생성, DB 해시 보존 — **PASS**.
- 상한 보강을 포함한 별도 Release 후보 `dist-dev\0.2.0-autonomous-20260804-r2` 생성·정적 검사 및 30초 smoke — **PASS**. EXE SHA-256 `9580F657116B69016BA78DEF53A8C8FD83B4C46FB4BFD870A402624E73619983`, ZIP SHA-256 `EE37B19264177C3F984BA40CB49A30621C88130F21C1FA0F7BE2BA41EFA5BFCB`.

## 2026-08-04 0.2.0-rc.1 전환 기록

- 새 RC 백업 `C:\Users\MSI\Desktop\개발\_hanki_rc_backup\20260804-233255` 생성. 사용자 DB와 관련 파일의 SHA-256·복구 manifest 보관 — **PASS**
- 소스 버전·WPF 표시·RC Inno 스크립트·README를 0.2.0-rc.1로 동결. 기존 dev/0.1.1 산출물은 덮어쓰지 않음 — **PASS**
- RC ZIP/설치본 및 `SHA256SUMS-0.2.0-rc.1.txt` 생성. 공개 auth config만 포함 — **PASS**
- 실제 0.2.0-dev → RC 업그레이드 후 DB integrity·논리 기준값·시작 메뉴·제거 등록 보존 — **PASS**
- RC 단일 인스턴스·트레이 복원·빠른 5회 실행 — **PASS**. 계정 탭 육안/Google 버튼 클릭은 **자동화 불가**.
- 1시간 RC 격리 관찰 완료: 59개 1분 샘플, working set 141.06–161.63MB, private 71.11–89.05MB, handles 724–950. 진단 인자 완료 후 대상·monitor 프로세스와 포트 43289가 정리됨 — **PASS**.
- PathMap 적용으로 RC payload의 PDB/개발 절대경로를 제거한 뒤 새 ZIP smoke를 재실행했다. 격리 DB 해시 일치·30초 자동 종료·프로세스 0·포트 해제 — **PASS**.
- Inno Setup 6.7.3으로 최종 설치본을 재컴파일하고 공식 옵션 설치를 재실행했다. exit code 0, FileVersion/ProductVersion·DB·auth.session·포트·프로세스 상태를 재확인 — **PASS**.

## 2026-08-05 Google 계정 선택 UX 수정

- 원인 확인: Supabase Google authorize URL에 account-picker prompt가 없어서 기본 Chrome 세션이 재사용될 수 있었다.
- `prompt=select_account`를 URI 인코딩 query parameter로 추가하고 `prompt=consent`/`login_hint`는 추가하지 않음 — **PASS**.
- URL builder 테스트에 provider, redirect, PKCE, state, 인코딩, 중복 query 및 금지 parameter 회귀 검증 추가 — **PASS**.
- Debug/Release build 및 test 각 127/127, format/diff check — **PASS**.
- 별도 `dist-rc\0.2.0-rc.1-account-picker-test` Portable 후보와 격리 smoke 생성 — **PASS**.
- Google 계정 선택 UI는 인증 대화상자 자동 조작 금지 원칙에 따라 실행하지 않음 — **사용자 수동 확인 필요**.
- 기존 `dist-rc\0.2.0-rc.1` 산출물·설치본·사용자 세션은 변경하지 않음 — **PASS**.
# 2026-08-05 0.2.0-rc.2 후보 제작 및 업그레이드

- RC2 전용 timestamp 백업 `_hanki_rc2_backup\20260805-072349` 생성: PASS. DB, auth.session, 로그, Git 상태, RC1 metadata를 보관했습니다.
- `prompt=select_account` OAuth 수정과 URL 회귀 테스트: PASS. prompt 중복·consent·login_hint·redirect/PKCE/state를 검증했습니다.
- Debug/Release build 및 test: PASS (각 127/127, 경고·오류 0). format/diff check: PASS.
- RC2 ZIP/installer/manifest/SHA256SUMS/release notes 생성: PASS. RC1 경로는 덮어쓰지 않았습니다.
- 격리 Portable diagnostic smoke: PASS (exit 0, DB 해시 유지, auth.session 미생성, 프로세스/43289 정리).
- 공식 Inno 옵션으로 RC1→RC2 업그레이드: PASS (exit 0, 동일 AppId/경로, ProductVersion rc.2, FileVersion 0.2.0.0).
- 설치 후 DB integrity·SHA-256·논리 fingerprint·단축어/사용 횟수/설정 보존: PASS.
- 설치된 RC2 빠른 5회 실행 단일 인스턴스: PASS. 최종 Hanki 프로세스 0개, 43289 미청취.
- Chrome 계정 선택 육안 확인: 사용자 수동 확인 필요. 인증 UI 자동 조작은 하지 않았습니다.
- Authenticode: 사용자 승인 필요 (`NotSigned`). Defender 대상 검사: PASS, 탐지 없음.
- 사이트 내부 RC2 metadata만 변경하고 `published=false`·URL 없음 유지: PASS. 공개 배포는 미실행.
- 사이트 lint/typecheck/test/build/link check: PASS (22/22). format script는 없어 미실행으로 기록했습니다.
- 2026-08-05 사용자 수동 검증 결과: Google 계정 선택/다른 계정 사용, 기존 Chrome 계정 자동 확정 방지, 주요 앱 치환, 비밀번호 보호, 제외 앱·사이트, 자동 시작, 설치·시작 메뉴·작업표시줄·Alt+Tab·트레이·계정 UI·DPI를 모두 **PASS**로 확인했습니다. RC2 판정을 `RELEASE_READY`로 변경했습니다.

## 2026-08-05 정식 0.2.0 공개 및 사이트 반영

- HankiWindows `release: prepare Hanki 0.2.0` commit `f9e15f9`, 일반 push, annotated tag `v0.2.0` push: **PASS**.
- GitHub Release `한키 0.2.0` 공개 및 설치본·Portable·SHA256SUMS 업로드: **PASS**. Release는 latest이며 draft/prerelease가 아닙니다.
- 공개 자산 임시 재다운로드, ZIP 압축 해제, 제품 버전·해시·Authenticode 확인: **PASS**. 실제 해시는 `docs/POST_RELEASE_VERIFICATION_0.2.0.md`에 기록했습니다.
- yulbyte-site에 `published=true`, 실제 GitHub Release URL·SHA-256·설치형/Portable 버튼 반영. 사이트 commit `5d8668c`를 `main`에 push: **PASS**.
- yulbyte-site lint/typecheck/test 22/22/build/internal link check: **PASS**.
- `www.yulbyte.com`, `/hanki`, `/updates`, `/guide` HTTP 200: **PASS**. `/hanki`에서 0.2.0·설치형·Portable·GitHub Release 링크를 확인했습니다.
- Vercel production 자동 배포 반영과 운영 링크/해시 확인: **PASS**. 사이트 링크와 GitHub 자산 모두 HTTP 200입니다.
- Supabase migration, 추가 사용자 데이터 삭제, code signing은 실행하지 않음. Hanki 설치본은 최종 프로세스 0개·43289 미청취 상태로 정리했습니다.
