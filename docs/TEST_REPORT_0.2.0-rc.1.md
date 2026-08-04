# 한키 Windows 0.2.0-rc.1 테스트 보고서

## 요약

| 항목 | 상태 | 결과 |
|---|---|---|
| 전용 백업 | PASS | `_hanki_rc_backup\20260804-233255`, DB와 Git 상태·패키지 해시 보관 |
| 버전 동결 | PASS | 0.2.0-dev → 0.2.0-rc.1, FileVersion 0.2.0.0 |
| Debug build/test | PASS | 경고 0·오류 0, 126/126 |
| Release build/test | PASS | 경고 0·오류 0, 126/126 |
| Portable RC 정적 검사 | PASS | 공개 설정 3개 키, DB/session/PDB/secret/개발 경로 없음 |
| Portable baseline smoke | PASS | 격리 DB, 30초 진단 종료, 프로세스 0, 포트 43289 해제 |
| PathMap 재생성 Portable smoke | PASS | 새 ZIP에서 동일 격리 DB 해시, 30초 자동 종료, 프로세스 0, 포트 해제 |
| 빈 데이터 첫 실행 자동화 | 자동화 불가 | 최초 실행 UI가 자동 진단 종료를 막아 추가 입력 없이 중단 |
| 실제 0.2.0-dev → RC 업그레이드 | PASS | Inno 공식 옵션, exit code 0, 기존 경로·AppId·데이터 보존 |
| 설치 후 단일 인스턴스 | PASS | 빠른 5회 실행에서 프로세스 1개 |
| 트레이 숨김·재실행 복원 | PASS | CloseMainWindow 후 트레이 상태 추정 및 재실행 창 복원 |
| 계정 탭·로그인 버튼 육안 확인 | 자동화 불가 | 다른 앱 입력에 영향 없는 안전한 UI 자동화 세션 없음 |
| 실제 Google 재인증 | 미실행 | 기존 0.2.0-dev 실제 인증 PASS를 재사용, 비밀번호/MFA 재입력 금지 |
| 1시간 격리 관찰 | PASS | 59개 1분 샘플, 프로세스 1시간 인자 완료 후 자동 종료, working set 141.06–161.63MB, private 71.11–89.05MB, handles 724–950 |
| Defender RC 검사 | PASS | 탐지 결과 없음 |

## RC 산출물

- `dist-rc\0.2.0-rc.1\Hanki-0.2.0-rc.1-win-x64.zip`
  - SHA-256: `26530605379D410952E87621BD78FDAD6F815D48957A8ABBFF88D4FBB1E4B577`
- `dist-rc\0.2.0-rc.1\HankiSetup-0.2.0-rc.1.exe`
  - SHA-256: `684CFBF071CAB1BC83A9A9909E511AB5216B7FBF7F8DD9B36DFE0BD057C4C5A0`
- Authenticode: `NotSigned` — **사용자 승인 필요**

## 업그레이드 후 기준값

- DB SQLite integrity: `ok` — **PASS**
- DB 물리 해시: 백업 해시와 일치 — **PASS**
- 단축어 7개, 사용 횟수 총합 14, 설정 9개 — **PASS**
- 제외 프로세스 4개, 제외 사이트 0개, auth.session 없음 — **PASS**

## 보안·취약점

- Production `npm audit --omit=dev`: 0건 — **PASS**
- Development audit: `brace-expansion` High 1건. ESLint 9의 `minimatch@3.1.5` 체인에서만 사용되며 production bundle에는 포함되지 않음 — **사용자 승인 필요**
- RC 패키지 secret/token/DB/PDB/로그/`.env.local`/개발 절대경로: 0 — **PASS** (PathMap 적용 재생성 후 재검사)

## 격리 관찰 상세

- 측정 파일: `docs\artifacts\performance\handle-soak-0.2.0-rc.1.csv`
- 격리 DB 해시와 integrity는 백업과 일치했고, 종료 후 대상 프로세스·monitor·포트 43289가 모두 정리됐다.
- 실제 사용자 DB·인증 세션·입력 치환은 격리 관찰에 사용하지 않았다.

이번 RC 작업에서 commit, push, tag, GitHub Release, Vercel 배포, Supabase migration은 실행하지 않았다.

## Google 계정 선택 UX 수정 검증

- 원인: Supabase authorize URL에 Google provider를 지정했지만 provider-specific `prompt`가 없어 기본 브라우저 세션이 재사용될 수 있었다.
- 수정: `SupabaseAuthenticationService.BuildAuthorizeUrl`에 URI 인코딩된 `prompt=select_account`를 추가했다. PKCE, state, callback listener, redirect URI, DPAPI는 변경하지 않았다.
- 자동 테스트: **PASS**, Debug/Release 각 127/127
- `prompt=consent`: 미포함 — **PASS**
- `login_hint`: 미포함 — **PASS**
- redirect URL·PKCE code challenge·state·URL 인코딩·prompt 중복 없음 — **PASS**
- Google 외 provider: 현재 코드 경로에 다른 provider가 없어 불필요한 적용 없음 — **PASS**
- 실제 Chrome 계정 선택 화면: **사용자 수동 확인 필요**. 인증 대화상자 자동 조작은 수행하지 않았다.
- 수정 검증 후보: `dist-rc\0.2.0-rc.1-account-picker-test\`, 기존 `dist-rc\0.2.0-rc.1\`은 덮어쓰지 않았다.
