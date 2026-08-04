# 한키 Windows 0.2.0 출시 준비도

## 현재 판정: `RELEASE_READY`

RC2의 수동 검증과 정식 0.2.0의 자동·설치 검증이 모두 PASS했습니다.

### PASS

- Google 계정 선택 화면과 `다른 계정 사용` 확인
- 메모장·Chrome·Discord·KakaoTalk 단축어 치환 및 비밀번호 보호
- 제외 앱·사이트, 자동 시작, 설치/시작 메뉴/작업표시줄/Alt+Tab/트레이/DPI
- `prompt=select_account`, PKCE/state/redirect/listener/DPAPI
- Debug/Release 127/127, warning/error 0, format/diff
- 정식 ZIP/설치본 생성 및 SHA-256
- RC2→0.2.0 업그레이드 exit code 0
- 사용자 DB integrity·논리 데이터 보존·인증 세션 보존
- Portable/설치본 단일 인스턴스 및 최종 포트 정리
- 사이트 테스트 22/22 및 공개 metadata 준비

### 사용자 승인 필요

- Authenticode 서명 및 SmartScreen의 `알 수 없는 게시자` 정책

### 다음 공개 단계

1. HankiWindows 커밋/일반 push
2. `v0.2.0` annotated tag 및 push
3. GitHub Release와 세 자산 업로드
4. 자산 재다운로드 SHA-256 검증
5. 실제 URL을 Yulbyte release metadata에 반영하고 `published=true`
6. yulbyte-site 커밋/push 및 Vercel production 배포
