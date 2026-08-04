# 0.2.0-dev 출시 준비 평가

## 2026-07-26 격리 수동 검증 반영

- 실제 Google 로그인(사용자 Chrome 완료 세션), 계정 표시, 진행 UI 종료: **PASS**
- 로그아웃 UI 전환, 격리 DPAPI `auth.session` 삭제, 로컬 DB/단축키 유지: **PASS**
- 정상 종료 후 Release 재실행, 로그아웃 유지, 브라우저 미실행, 포트 해제: **PASS** (파일·프로세스 상태 확인)
- 재실행 계정 탭 로그인 버튼 직접 확인: **자동화 불가** (Computer Use 안전 중단)
- 실제 로그인 취소 버튼·실제 포트 충돌 UI: **자동화 불가**. 자동 listener 테스트는 **PASS**.
- 격리 Release/Portable 프로세스 정상 종료 및 설치된 0.1.1 보존: **PASS**
- Debug/Release build·test 0 warnings/errors, 각 123/123: **PASS**
- Portable ZIP 및 설치본 정적 검사: **PASS**. 설치본 SHA-256은 별도 기록되며 제품/회사/버전 메타데이터 확인. 코드서명 상태는 `NotSigned`.

### 현재 blocker 상태

1. 실제 취소/포트 충돌 UI는 **자동화 불가** (안전한 Computer Use 세션 재개 필요)
2. 설치본 실제 설치·업그레이드/UAC/SmartScreen은 **사용자 승인 필요**
3. 코드서명·SmartScreen 배포 정책은 **사용자 설정 필요**
4. 100/125/150/200% DPI 및 장시간 handle 검증은 **사용자 설정 필요**
5. 클라우드 데이터 삭제 정책 및 npm audit High 항목 결정은 **사용자 설정 필요**

이번 작업에서는 commit, push, tag, GitHub Release, Vercel 배포, Supabase migration을 실행하지 않았다.

## 평가: 조건부 출시 가능

코드·자동 테스트·로컬 빌드·패키징 후보는 준비됐지만 공개 출시 전 수동 blocker가 남아 있다.

## 준비된 항목

- Windows 123/123, Debug/Release build 경고·오류 0
- 웹 20/20, lint/typecheck/build/link check 통과
- 공식 로고와 별도 `dist-dev` ZIP/설치본
- OAuth/PKCE/state/loopback/DPAPI/refresh 보강
- Defender 탐지 0, package secret/PDB/절대경로 검사 통과
- 기존 0.1.1 배포 파일 미변경

## 공개 전 blocker

1. Supabase redirect URL 등록과 실제 Google 계정 로그인·재시작·로그아웃 확인
2. 설치본의 동일 AppId 업그레이드, 시작 메뉴/제거/사용자 데이터 보존 확인
3. 코드 서명 또는 NotSigned/SmartScreen 배포 정책 결정
4. 로고와 100–200% DPI의 최종 육안 확인
5. 서비스 탈퇴·서버 계정 데이터 삭제 정책 확정
6. npm 개발 도구 체인 High 감사 9건의 ESLint 10 호환성 검토
7. 15분 유휴에서 handle이 739→861로 증가한 원인이 장기적으로 계속되는지 추가 관찰

production migration, Vercel 배포, Git commit/push/tag/Release는 수행하지 않았다.

## 설치 업그레이드 검증 후 상태

- 0.1.1 사용자 데이터 백업 및 복구 가능성 — **PASS**
- 별도 install-test 후보의 공개 인증 설정 포함·비밀정보 미포함 — **PASS**
- 실제 0.1.1 → 0.2.0-dev 업그레이드, 동일 AppId/경로, 버전·시작 메뉴·제거 등록 — **PASS**
- 업그레이드 전후 DB SHA-256·단축어·사용 횟수·설정 — **PASS**
- 공식 제거 후 사용자 데이터 보존 및 동일 버전 재설치 — **PASS**
- 최종 0.2.0-dev 설치·Hanki 프로세스 0개·43289 해제 — **PASS**
- 후보 설치본 및 설치된 실행 파일 Authenticode — **사용자 승인 필요** (`NotSigned`)
- 실제 Google 인증 재실행 — **미실행** (기존 수동 로그인 PASS 유지)

rollback은 필요하지 않았다. 공개 출시 전 코드 서명, SmartScreen 정책, 다른 PC 설치 시나리오는 사용자 승인 필요다.

## 2026-08-04 자율 유지보수 반영

- Yulbyte `/hanki`를 Windows 0.2.0-dev 로컬 검증 상태 중심으로 갱신하고, 공개 다운로드 링크를 추가하지 않음 — **PASS**
- 웹사이트 `npm test` 21/21, lint/typecheck/build/link check — **PASS**
- Windows 인증 설정에서 malformed JWT와 사용자 access-token 역할을 publishable key로 허용하지 않는 회귀 테스트 추가 — **PASS**
- 사용자 데이터와 분리한 diagnostic process 시작 — **PASS**
- 소스 보안 보강을 반영한 별도 Portable 후보 생성 및 정적 검사 — **PASS**
- 기존 15분 handle 증가 표본은 장시간 추가 관찰이 필요한 blocker로 유지 — **사용자 설정 필요**
- 코드 서명·SmartScreen 정책 — **사용자 승인 필요**

## 2026-08-04 자율 검증 추가 결과

- 별도 Portable 후보 실행/정상 진단 종료/프로세스 정리/DB 격리 — **PASS**
- 후보 정적 패키지 검사(공개 키 3개만, token/session/service_role/client_secret/PDB/개발 절대경로 없음) — **PASS**
- Yulbyte `/hanki` Windows 중심 문구·릴리스 상태 카드·공개 다운로드 보류·모바일 overflow 및 demo 동작 — **PASS**
- 전체 자동 테스트: Windows Debug/Release 각 125/125, 웹 21/21; build/lint/typecheck/link/format/diff — **PASS**
- 45분 handle soak 최종 판정: 기존 dev 기록은 **미실행**으로 보존한다. RC에서 1시간 격리 관찰은 **PASS**로 완료했다. 코드 서명/Authenticode는 **사용자 승인 필요**, 개발 의존성 High 1건은 **사용자 설정 필요**.

### 45분 격리 관찰 판정

- 45개 idle diagnostic 샘플 수집 및 메모리/핸들 범위 기록 — **PASS (관찰 완료)**
- diagnostic exit 상한 1시간 확장 및 Debug/Release 재검증 — **PASS**
- 핸들 증가 가능성이 있어 실제 입력 훅 장시간 검증 — **사용자 설정 필요**
- 상한 보강 후 Debug diagnostic smoke 정상 종료 — **PASS**
- 상한 보강 Release 후보(r2) 정적 검사·30초 smoke — **PASS**. 공개 패키지와 별도 경로이며 서명 상태는 **사용자 승인 필요**.

## 2026-08-04 0.2.0-rc.1 전환

- RC 후보 패키징·실제 업그레이드·DB 보존 — **PASS**
- RC 공개 상태는 다운로드 URL 없음·`published=false`로 유지 — **PASS**
- RC 코드 서명과 SmartScreen 정책 — **사용자 승인 필요**
- RC 수동 시각/DPI/실제 입력 치환 — **자동화 불가**
