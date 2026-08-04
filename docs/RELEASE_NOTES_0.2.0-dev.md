# 한키 Windows 0.2.0-dev 릴리스 노트

로컬 검증용 후보이며 공개 릴리스가 아니다.

## 추가·개선

- 공식 한키 로고를 EXE, 창, 트레이, 설치 프로그램, 바로가기, 계정 화면에 적용
- Yulbyte와 같은 Google 계정 연결(Supabase Authorization Code + PKCE)
- 이름·이메일·HTTPS 프로필 이미지 표시와 fallback
- DPAPI CurrentUser 세션 저장, 시작 시 refresh/복원, 로그아웃
- 세션 복원·로그인 진행 UI, Enter 로그인/Escape 취소, 키보드 focus와 접근성 개선
- single-instance 숨김/최소화 창 복원과 빠른 연속 실행 회귀 유지

## 보안·안정성

- loopback callback을 `127.0.0.1:43289/auth/callback`과 GET으로 제한
- state 우선 검증, 리스너 종료·포트 재사용, secret/service-role config 차단
- 원자적 세션 저장과 손상/저장/삭제 실패 복구
- SQLite native dependency 취약 버전 해소

## 현재 제한

- 단축어 클라우드 동기화 없음
- Enter/Tab 치환은 설정 모델만 있고 실제 변환은 Space만 지원
- 코드 서명 없음; SmartScreen 경고 가능
- 실제 Google 로그인과 설치/업그레이드는 사용자 확인 전 미실행
