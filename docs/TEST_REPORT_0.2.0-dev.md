# 한키 0.2.0-dev 테스트 보고서

## 2026-07-26 격리 Release 인증 세션 검증

- 사용자 Chrome에서 완료된 Google 로그인 세션을 격리 Release에서 확인: **PASS** (표시명·이메일·fallback 아바타 표시, 개인정보 미출력)
- 로그아웃 실행 및 UI 전환: **PASS**
- 격리 `auth.session` 삭제와 DPAPI 평문 비노출: **PASS** (로그아웃 후 파일 없음; 로그아웃 전 JSON parse false)
- `hanki.db`·단축키 유지: **PASS** (DB 24576 bytes)
- 정상 종료 후 Release 재실행 시 로그아웃 상태·브라우저 미실행·포트 미점유: **PASS** (파일/프로세스 상태로 확인)
- 재실행 직후 로그인 버튼을 UI에서 직접 재확인: **자동화 불가** (Computer Use 안전 중단)
- 실제 로그인 취소 버튼 검증: **자동화 불가** (Computer Use 안전 중단; 인증을 진행하지 않음)
- 실제 43289 포트 충돌 UI 검증: **자동화 불가** (실제 UI 미실행). 기존 HttpListener/listener 자동 테스트의 충돌·취소·timeout 케이스는 **PASS**.
- 격리 Release/Portable 프로세스 정상 종료: **PASS**. 설치된 0.1.1 PID 10080 및 실행 경로는 보존.

## 최종 정적·자동 검증

- `dotnet build -c Debug/Release`: **PASS**, 각 warning 0/error 0
- `dotnet test -c Debug/Release`: **PASS**, 각 123/123
- `git diff --check`: **PASS** (line-ending 경고만 출력)
- 실제 0.1.1 ZIP/설치본/체크섬 재검증: **PASS** (해시는 로그에 기록, 개인정보·token은 미출력)
- 소스·문서·패키지 secret 검사: **PASS** (실제 publishable/service_role/client_secret/token 0; JWT 유사 문자열은 테스트 fixture만)
- ZIP/설치본 정적 검사: **PASS** (개발 절대경로·token·secret 미포함; 설치본 서명은 NotSigned)

실제 설치·UAC·SmartScreen·코드서명·DPI/장시간 handle 검증은 **사용자 승인 필요** 또는 **사용자 설정 필요**로 남겨 두었다.

측정일: 2026-07-26

## Windows 자동 검증

- 작업 전 기준: 89/89
- 현재: 123/123 (신규 34), Debug·Release 각각 실패 0
- Debug 앱 build: 경고 0, 오류 0
- Release 앱 build: 경고 0, 오류 0
- 실제 `HttpListener` 통합: 정상, wrong path, POST 405, state mismatch,
  cancel/timeout 후 재사용, 포트 충돌
- 서비스: PKCE URL, 중복 로그인 차단, browser 실패, callback 오류 분류, token 교환,
  HTTPS avatar, DPAPI save/load/delete 실패, refresh 성공/실패, 복원 상태
- 기존 단축어·single instance·window activation 테스트 유지
- 격리 앱 시작에서 발견된 WPF ICO runtime 오류를 PNG resource로 수정 후 종료 코드 0

실제 전역 키 입력 자동화는 다른 앱 입력에 영향을 줄 수 있어 실행하지 않았다. Enter/Tab
치환은 현재 설정 모델만 있고 구현되지 않은 기존 제한이다.

## 웹 자동 검증

- `npm test`: 20/20, 실패 0
- `npm run typecheck`: 오류 0
- `npm run lint`: 오류 0
- `npm run build`: 성공; `/`, `/hanki`, `/account`, `/auth/callback` 생성
- `npm run check:links`: 9 routes, 23 source files 통과
- 로컬 production browser: desktop/mobile horizontal overflow 0, 로고 200 응답 및
  96×96/48×48 표시, console error/warning 0

실제 Google 로그인, UAC 설치, 업그레이드, SmartScreen UI는 자동화하지 않았다.

## 2026-07-26 설치·업그레이드·제거·재설치 실제 검증

- 백업: `C:\Users\MSI\Desktop\개발\_hanki_upgrade_backup\20260726-161352` — **PASS**
- 별도 후보: `dist-dev\0.2.0-install-test` (ZIP `BE2CBC98AC5A13F09FF0502C5201668DF45D93BFFF487F43FA558ED763808DD7`, installer `10D8775EC1055C5707D1394D1B01822E8A8FCD0B08A71E3F07A7730039EA0FB5`) — **PASS**
- 0.1.1 사전 DB SHA-256과 업그레이드 직후·제거 후·재설치 후 DB SHA-256 일치 — **PASS**
- 업그레이드·재설치 후 논리 데이터(단축어 7개, 사용 횟수 합계 14, 설정 9개) 보존 — **PASS**
- 설치본 로그인 버튼 활성화 및 로고 표시, 실제 Google 인증 재시작 — **미실행** (기존 실제 로그인 PASS 기록은 유지)
- X→트레이, 재실행 창 복원, 최소화/빠른 5회 실행 단일 인스턴스 — **PASS**
- 공식 제거 exit code 0, 시작 메뉴·제거 등록·설치 파일 제거, 사용자 데이터 보존 — **PASS**
- 트레이에 숨은 프로세스가 uninstaller 뒤 남아 경로·버전이 일치하는 단일 PID를 마지막 수단으로 종료 — **PASS**
- 동일 0.2.0-dev 재설치 exit code 0, 최종 프로세스 0개, 43289 해제 — **PASS**
- UAC/SmartScreen 우회 없음. 로컬 실행에서 프롬프트 없음 — **PASS**; Authenticode `NotSigned`의 배포 정책 결정 — **사용자 승인 필요**

기존 Debug/Release build warning/error 0, test 각 123/123, `git diff --check`, secret/token 정적 검사는 이전 완료 결과를 재사용한다. 이번 작업에서 commit/push/tag/Release/Vercel 배포/Supabase migration은 수행하지 않았다.

## 2026-08-04 자율 유지보수 검증

- Yulbyte 웹사이트 `npm test`: 21/21 — **PASS**
- Yulbyte `lint`, `typecheck`, `build`, 내부 링크 검사: **PASS**
- Hanki Debug test: 125/125 — **PASS** (공개 키 검증 회귀 포함)
- 웹/Windows Supabase 설정 검증: malformed JWT와 `authenticated`/`supabase_admin` 역할 거부 — **PASS**
- `/hanki` 로컬 production 페이지: Windows `0.2.0-dev` 상태 표시, 공개 다운로드 링크 없음 — **PASS**
- `/hanki` 모바일 375px overflow: 0 — **PASS**
- 설치된 사용자 Hanki 프로세스와 사용자 DB: 변경하지 않음 — **PASS**
- 격리 진단 프로세스: `HANKI_DATA_DIRECTORY`와 별도 instance namespace 사용, 사용자 DB와 분리 — **PASS**
- 별도 자율 유지보수 Portable 후보 생성: EXE `362F41FEA00157A7D2CD2BB07423D3C6DFEB3A2FBBE519EA563AA50ED2504737`, ZIP `9289071673A37BD5FF41A266281478486A4114E059431ADD7E1DB1DE7697316D` — **PASS**
- 장시간 handle 관찰: 이전 15분 표본에서 handle 증가가 있어 추가 장시간 관찰 대상. 이번 진단은 종료 후 잔류 여부를 확인하는 중 — **미실행**

## 2026-08-04 자율 검증 최종 추가

- 자율 Portable 후보 격리 실행: config 로딩, 30초 정상 종료, 후보 프로세스 0, 포트 43289 해제, 세션 파일 미생성, 사용자 DB 해시 보존 — **PASS**
- 후보 EXE/ZIP SHA-256: `362F41FEA00157A7D2CD2BB07423D3C6DFEB3A2FBBE519EA563AA50ED2504737` / `9289071673A37BD5FF41A266281478486A4114E059431ADD7E1DB1DE7697316D`
- Production dependency audit: 0건 — **PASS**. Development audit: `brace-expansion` High 1건 — **사용자 설정 필요**.
- 45분 soak 최종 결과는 아직 수집 중 — **미실행**. 실제 Google 재로그인·서명·공개 배포는 수행하지 않았다.

### 45분 격리 관찰 최종 결과

- 45개 샘플(약 45분) 수집 완료: working set 132.41~161.65MB, private memory 70.69~88.38MB, handles 746~1086 — **PASS (관찰 완료)**.
- diagnostic 인자 상한 20분으로 인해 자동 종료가 예약되지 않아 정확한 격리 PID만 정리했다. 사용자 설치 앱·DB에는 영향 없음 — **PASS**.
- diagnostic exit 상한을 1시간으로 보강한 뒤 Debug/Release build 및 test 각 125/125 재실행 — **PASS**.
- 실제 입력 훅을 포함한 장시간 soak는 별도 사용자 시나리오가 필요 — **사용자 설정 필요**.
- 상한 보강 후 Debug 진단 30초 smoke 정상 종료·잔류 PID 0·세션 미생성·DB 해시 보존 — **PASS**.
- 상한 보강 Release 후보 `dist-dev\0.2.0-autonomous-20260804-r2` 정적 검사 및 30초 smoke — **PASS**. EXE `9580F657116B69016BA78DEF53A8C8FD83B4C46FB4BFD870A402624E73619983`, ZIP `EE37B19264177C3F984BA40CB49A30621C88130F21C1FA0F7BE2BA41EFA5BFCB`.

이번 자율 작업에서도 commit, push, tag, Release, Vercel 배포, Supabase migration, 0.1.1 배포 파일 덮어쓰기는 수행하지 않았다.

## 2026-08-04 0.2.0-rc.1 전환 기록

- RC 전용 백업 `_hanki_rc_backup\20260804-233255` 생성 및 DB 무결성·해시 기준 보관 — **PASS**
- 0.2.0-rc.1 버전 동결, RC Portable/설치본 생성, Defender 검사 — **PASS**
- 실제 0.2.0-dev → 0.2.0-rc.1 업그레이드 exit code 0, DB·논리 기준값 보존 — **PASS**
- RC 설치 후 단일 인스턴스·빠른 5회 실행·트레이 숨김/재실행 복원 — **PASS**
- 빈 데이터 첫 실행의 UI 자동 종료 — **자동화 불가**
- 1시간 RC soak — **PASS** (59개 샘플, 대상 프로세스 자동 종료·포트 해제; 상세 `HANDLE_SOAK_ANALYSIS_0.2.0-rc.1.md`)
