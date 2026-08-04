# 한키 Windows 0.2.0-rc.1 출시 후보 노트

## 포함된 검증 기능

- 한키 공식 브랜드·아이콘과 Windows 설치/트레이 적용
- Windows 앱 내 선택적 Google 로그인 진입
- Authorization Code + PKCE와 `127.0.0.1:43289/auth/callback` loopback
- DPAPI 보호 인증 세션과 로그아웃 시 세션 삭제
- 단일 인스턴스·트레이 숨김·창 복원 개선
- 계정 UI와 로그인 설정 유효성 검사
- 기존 단축어·사용 횟수·설정 보존을 확인한 업그레이드
- RC Portable/설치본 secret·token·사용자 데이터 제외 패키징

## 제한

- 단축어와 설정은 현재 로컬 저장이며 클라우드 동기화는 구현되지 않았다.
- 코드 서명이 적용되지 않아 SmartScreen에서 알 수 없는 게시자 안내가 표시될 수 있다.
- 정식 공개 전까지 다운로드 URL은 비활성화한다.
