# 사용자가 확인할 다음 단계

## 이번 격리 검증 결과 (2026-07-26)

- Supabase Redirect URL 등록: **PASS** (사용자 설정 완료)
- 실제 Google 로그인 성공·계정 정보 표시·로그인 진행 종료: **PASS** (개인정보 미출력)
- 로그아웃·격리 `auth.session` 삭제·로컬 단축키/DB 유지: **PASS**
- 완전 종료 후 재실행 시 로그아웃 유지·브라우저 미실행·43289 미점유: **PASS**
- 재실행 직후 로그인 버튼 UI 직접 확인: **자동화 불가** (Computer Use 안전 중단)
- 실제 로그인 취소 버튼: **자동화 불가** (Computer Use 안전 중단)
- 실제 포트 충돌 UI: **자동화 불가** (자동 listener 테스트는 PASS)
- 격리 Release/Portable 종료 및 설치된 0.1.1 보존: **PASS**

## 남은 사용자 작업

- 설치본 실제 설치·업그레이드 및 UAC/SmartScreen 동작: **사용자 승인 필요**
- 코드 서명/배포 정책 결정: **사용자 설정 필요**
- 100/125/150/200% DPI 및 장시간 handle 관찰: **사용자 설정 필요**
- 클라우드 데이터 삭제 정책과 npm audit High 항목 결정: **사용자 설정 필요**
- 최종 commit·push·tag·GitHub Release·배포 여부: **사용자 승인 필요** (이번 작업에서는 실행하지 않음)

- [ ] `assets\branding\hanki-logo-contact-sheet.png`에서 로고 디자인과 16–32px 확인
- [ ] Supabase Redirect URLs에 `http://127.0.0.1:43289/auth/callback` 등록
- [ ] 실제 Google 로그인 후 이름·이메일·프로필 이미지/fallback 확인
- [ ] 앱 완전 종료·재시작 후 로그인 유지 확인
- [ ] 로그아웃·재시작 후 로그아웃 상태와 `auth.session` 삭제 확인
- [ ] 트레이로 숨긴 상태에서 로그인 완료와 창 복원 확인
- [ ] Portable ZIP을 별도 폴더에서 실행
- [ ] 설치본 실행과 0.1.1 → 0.2.0-dev 데이터·설정 보존 확인
- [ ] 시작 메뉴/선택적 바탕 화면/제거 프로그램 아이콘 확인
- [ ] Windows SmartScreen 경고와 코드 서명 정책 결정
- [ ] 100%, 125%, 150%, 200% DPI와 긴 이메일 레이아웃 확인
- [ ] 최종 commit·push·tag·GitHub Release·웹 배포 여부 결정

## 0.2.0-dev 실제 설치 검증 결과

- 백업 경로: `C:\Users\MSI\Desktop\개발\_hanki_upgrade_backup\20260726-161352` — **PASS**
- install-test 후보 생성·정적 검사·공개 인증 설정 포함 — **PASS**
- 0.1.1 → 0.2.0-dev 업그레이드 — **PASS**
- 제거 후 사용자 DB·단축어·사용 횟수·설정 보존 — **PASS**
- 동일 0.2.0-dev 재설치 및 로그인 버튼 활성화 — **PASS**
- 최종 설치 버전 `0.2.0-dev`, 프로세스 0개, 포트 43289 해제 — **PASS**
- 코드 서명/SmartScreen 배포 정책 — **사용자 승인 필요**
- 공개 배포 전 실제 Google 인증·다른 PC 설치 — **미실행**

이번 검증에서는 commit, push, tag, GitHub Release, Vercel 배포, Supabase migration을 수행하지 않았다.

## 2026-08-04 자율 유지보수 결과

- `/hanki` Windows 0.2.0-dev 상태·공개 전 안내·Chrome 참고 화면 구분 — **PASS**
- 웹사이트 모바일 375px overflow 및 설치 전 체험 — **PASS**
- Supabase 공개 키 검증 강화와 회귀 테스트 — **PASS**
- 장시간 handle 증가 추가 관찰 — **사용자 설정 필요**
- 코드 서명·SmartScreen 배포 정책 — **사용자 승인 필요**
- 공개 다운로드 링크·Vercel 배포·Supabase migration — **미실행**

## 2026-08-04 자율 검증 후 남은 항목

- 자율 Portable 후보 smoke와 최종 Debug/Release·웹 검증 — **PASS**
- 공개 배포 전 Authenticode 코드 서명과 SmartScreen 정책 확인 — **사용자 승인 필요**
- 기존 45분 dev handle/메모리 soak 기록 — **미실행**으로 보존. RC 1시간 격리 관찰은 아래 전환 기록에서 **PASS**.
- 45분 idle diagnostic 관찰은 완료했으며 핸들 746~1086, private memory 70.69~88.38MB를 기록했다 — **PASS (관찰 완료)**
- 실제 입력 훅을 포함한 장시간 handle 검증은 **사용자 설정 필요**.
- 개발 의존성 `brace-expansion` High 1건의 업그레이드·정책 결정 — **사용자 설정 필요**. production dependency audit은 0건.
- Google 재로그인, UAC/SmartScreen UI, DPI별 수동 UI는 기존 실제 검증 기록을 보존하고 이번 자율 작업에서는 다시 실행하지 않았다.

## 2026-08-04 0.2.0-rc.1 전환

- RC 설치·업그레이드·데이터 보존·단일 인스턴스 — **PASS**
- 수동 아이콘/DPI/실제 입력 치환 — **자동화 불가**
- Authenticode 서명·SmartScreen 인터넷 환경 — **사용자 승인 필요**
- 1시간 RC 리소스 관찰 최종 판정 — **PASS** (59개 샘플, 자동 종료·정리 확인)

## 2026-08-05 Google 계정 선택 UX 수정

- `SupabaseAuthenticationService` authorization URL에 URI 인코딩된 `prompt=select_account` 추가 — **PASS**
- `prompt=consent`, `login_hint`, 중복 prompt 미포함 및 PKCE/state/redirect 유지 — **PASS**
- Debug/Release build 및 test `127/127` — **PASS**
- 별도 검증 후보 `dist-rc\0.2.0-rc.1-account-picker-test\` 생성. 기존 RC 산출물 보존 — **PASS**
- 실제 Chrome 계정 선택 화면과 `다른 계정 사용` 표시 — **사용자 수동 확인 필요**
- 실제 사용자 인증·브라우저 쿠키·설치된 RC는 변경하지 않음 — **PASS**
# 2026-08-05 RC2 다음 단계

- `0.2.0-rc.2` 후보와 설치 업그레이드 검증: PASS
- 사용자 DB·단축어·설정·기존 세션 보존: PASS
- 실제 Chrome 계정 선택 화면 육안 확인: 사용자 수동 확인 필요
  - 실행기: `HankiWindows\dist-rc\0.2.0-rc.2\Run-Account-Picker-Test.ps1`
  - 계정 선택 또는 비밀번호 입력은 진행하지 말고 화면만 확인한 뒤 취소/정상 종료
- Authenticode 서명과 SmartScreen 정책: 사용자 승인 필요
- 공개 URL/다운로드, commit, push, tag, Release, Vercel, Supabase migration: 미실행
# 2026-08-05 RC2 수동 검증 완료

- Google 계정 선택, `다른 계정 사용`, 기존 계정 자동 확정 방지: **PASS**
- 메모장·Chrome·Discord·KakaoTalk 치환, 비밀번호 보호, 제외 앱/사이트, 자동 시작: **PASS**
- 설치/시작 메뉴/작업표시줄/Alt+Tab/트레이/계정 UI/DPI: **PASS**
- RC2 출시 준비 판정: **RELEASE_READY**
- 다음 단계는 정식 0.2.0 패키지·설치 검증과 공개 출시 절차입니다.
