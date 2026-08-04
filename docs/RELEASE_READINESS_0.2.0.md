# 한키 Windows 0.2.0 출시 준비도

## 현재 판정: `RELEASE_READY_WITH_VERCEL_BLOCKER`

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
- 사이트 테스트 22/22 및 공개 metadata 반영
- GitHub Release `v0.2.0` 공개, 자산 재다운로드·해시 검증
- HankiWindows commit/push/tag 및 yulbyte-site commit/push 완료

### 사용자 승인 필요

- Authenticode 서명 및 SmartScreen의 `알 수 없는 게시자` 정책
- Vercel 로그인/production 배포 설정 및 운영 HTML 갱신

### 완료한 공개 단계

1. HankiWindows 커밋/일반 push, `v0.2.0` annotated tag 및 push
2. GitHub Release와 세 자산 업로드 및 재다운로드 SHA-256 검증
3. 실제 URL을 Yulbyte release metadata에 반영하고 `published=true`
4. yulbyte-site 커밋/push

### 남은 단계

1. Vercel 프로젝트에 로그인하고 production 배포를 실행하거나 Git integration을 연결합니다.
2. `https://www.yulbyte.com/hanki`에서 0.2.0 설치형·Portable 링크와 해시를 확인합니다.
