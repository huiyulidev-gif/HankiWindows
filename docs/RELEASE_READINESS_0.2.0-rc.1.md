# 한키 Windows 0.2.0-rc.1 출시 준비 평가

## 판정: `RC_READY_WITH_MANUAL_CHECKS`

자동 검증·패키징·실제 업그레이드·데이터 보존은 통과했다. 다음 수동 확인이 남아 있어 공개 배포 전 사용자 승인이 필요하다.

- Authenticode 코드 서명 및 SmartScreen 인터넷 다운로드 환경 — **사용자 승인 필요**
- 설치 화면·아이콘·DPI 100/125/150/200% — **자동화 불가**
- 메모장·Chrome·Discord·KakaoTalk 실제 입력 치환 및 비밀번호 입력창 보호 — **자동화 불가**
- 1시간 RC 격리 handle 관찰 — **PASS** (59개 샘플, 프로세스 자동 종료, 수치 상세는 `HANDLE_SOAK_ANALYSIS_0.2.0-rc.1.md`)
- 개발 의존성 `brace-expansion` High 1건 정책 결정 — **사용자 승인 필요**

## 통과한 범위

- 0.2.0-dev → 0.2.0-rc.1 공식 Inno 업그레이드와 동일 AppId/경로
- DB integrity, DB 해시, 단축어·사용 횟수·설정·제외 목록 보존
- RC Portable/설치본 버전·제품명·회사명·공개 auth config
- 단일 인스턴스, 빠른 5회 실행, 트레이 숨김 후 재실행 복원
- Windows Debug/Release 126/126, 웹 22/22, build/lint/typecheck/link/format/diff
- Defender 탐지 없음, RC 공개 다운로드 비활성화
- PathMap 적용 후 payload PDB·개발 절대경로 0 — **PASS**

공개 상태는 `published=false`, installer/portable/release URL 없음, signed=false로 유지한다.

## 계정 선택 UX 수정 상태

- OAuth URL에 `prompt=select_account` 추가 및 consent/login_hint 미포함 — **PASS**
- PKCE/state/redirect/listener 회귀 — **PASS**
- 실제 Chrome 계정 목록 표시 — **사용자 수동 확인 필요**
- 수정은 기존 RC 산출물에 반영하지 않았으며 별도 `0.2.0-rc.1-account-picker-test` 후보만 생성했다.
