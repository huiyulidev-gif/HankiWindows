# 한키 Windows 0.2.0-rc.2 출시 준비도

## 판정: `RELEASE_READY`

자동 빌드·테스트·패키지·격리 smoke·실제 RC 업그레이드·데이터 보존 검증과 사용자의 수동 UI 검증이 모두 PASS입니다. 정식 0.2.0 산출물 공개 단계로 진행할 수 있습니다.

### PASS

- `prompt=select_account`가 Google authorization URL에 정확히 한 번 포함됩니다.
- `prompt=consent`, `login_hint`는 포함되지 않습니다.
- redirect URI, state, PKCE challenge, callback listener, DPAPI 로직을 변경하지 않았습니다.
- Debug/Release build 및 test 각 127/127, format, diff check PASS
- RC2 Portable/installer가 새 경로에 생성되었고 RC1은 변경되지 않았습니다.
- 설치 경로와 AppId를 유지한 RC1→RC2 업그레이드 exit code 0
- 사용자 DB integrity·SHA-256·fingerprint·단축어·사용 횟수·설정 보존
- 최종 Hanki 프로세스 0개, `127.0.0.1:43289` listener 0개
- 사이트 내부 metadata만 RC2로 갱신, `published=false`, 다운로드 URL 없음
- Yulbyte site lint/typecheck/test/build/link check PASS; format은 script 부재로 미실행

### 수동 확인 PASS

- Chrome 계정 선택 목록 및 `다른 계정 사용` 표시
- 기존 Chrome 계정 자동 확정 방지
- 메모장·Chrome·Discord·KakaoTalk 단축어 치환
- 비밀번호 입력창 보호, 제외 앱·사이트, 자동 시작
- 설치/시작 메뉴/작업표시줄/Alt+Tab/트레이 아이콘
- 계정 탭과 버튼, DPI에서 잘림 없음
- 취소/종료 후 listener와 포트 정리

### 사용자 승인 필요

- Authenticode 서명 및 SmartScreen 배포 정책 (`NotSigned` 상태)
- 공개 다운로드/정식 release 전환
