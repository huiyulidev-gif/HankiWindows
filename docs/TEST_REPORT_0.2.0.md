# 한키 Windows 0.2.0 테스트 보고서

## 자동·수동 검증

| 항목 | 상태 | 결과 |
|---|---|---|
| RC2 수동 검증 | PASS | 계정 선택, 단축어 치환, 입력 보호, 제외 규칙, 자동 시작, 아이콘, 트레이, DPI |
| Debug build/test | PASS | 경고 0, 오류 0, 127/127 |
| Release build/test | PASS | 경고 0, 오류 0, 127/127 |
| Format/diff check | PASS | `dotnet format --verify-no-changes`, `git diff --check` |
| 정식 Portable smoke | PASS | 0.2.0 metadata, 격리 DB, exit 0, 5회 단일 인스턴스 |
| RC2 → 0.2.0 업그레이드 | PASS | 공식 Inno 옵션, exit code 0, 동일 경로/AppId |
| 사용자 데이터 보존 | PASS | integrity ok, 단축어 7개, 설정 9개, 사용 횟수 15(수동 검증 후 기준) |
| 설치된 0.2.0 회귀 | PASS | 실행, 계정 탭/로고, 5회 단일 인스턴스, 종료/포트 정리 |
| Yulbyte site lint/typecheck/test/build/link | PASS | 22/22 및 각 명령 성공 |
| Authenticode | 사용자 승인 필요 | `NotSigned` |
| 공개 Release/사이트 공개 | 미실행 | GitHub Release와 Vercel은 커밋 후 진행 |

## 정식 산출물

- `dist\0.2.0\Hanki-0.2.0-win-x64.zip`
  - SHA-256 `07F4C99DE9BA846C5870FA3C586F5CCEA287275403CEB6E3293F61D909436917`
  - 66,986,153 bytes
- `dist\0.2.0\HankiSetup-0.2.0.exe`
  - SHA-256 `11056DF733CE961AA7633A2362FF1C4A50E1257FD64B26E42A7FC0617C74CD3E`
  - 50,262,657 bytes
- Payload EXE SHA-256 `65B50C2791F55DD83B9815EFA27C1CEFFA61646D217B17225693ED58F7B9D91E`
- FileVersion `0.2.0.0`, ProductVersion `0.2.0`, 제품 `한키`, 회사 `Yulbyte`

## 데이터와 보안

- 설치 후 SQLite integrity: `ok`
- 설치 후 DB SHA-256: `AF5F0716BA130A7F98E041C89B76ACD07FE8500807A74F603C9EF93FB1B4A392`
- 인증 세션 보존: PASS (값은 출력하지 않음)
- 정식 payload 파일: EXE와 공개 auth config 2개
- session/DB/PDB/.env.local/secret/token/개발 절대경로: 포함되지 않음
- Defender 사용자 지정 검사: PASS, 탐지 없음

## 공개 작업 상태

아직 commit, push, tag, GitHub Release, Vercel production 배포, 공개 다운로드 활성화는 실행하지 않았습니다. 이 문서는 정식 패키지와 설치 검증 완료 시점의 기록입니다.
