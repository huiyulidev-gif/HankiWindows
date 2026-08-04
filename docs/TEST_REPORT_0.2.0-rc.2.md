# 한키 Windows 0.2.0-rc.2 테스트 보고서

## 판정 요약

| 항목 | 상태 | 결과 |
|---|---|---|
| RC1 산출물 보존 | PASS | 작업 전 백업과 작업 후 SHA-256/크기 비교 일치 |
| 사용자 데이터 백업 | PASS | `_hanki_rc2_backup\20260805-072349`, DB·세션·로그 백업 및 복구 manifest 생성 |
| OAuth account picker URL | PASS | `prompt=select_account` 1회, consent/login_hint 없음 |
| PKCE/state/redirect 유지 | PASS | 기존 callback, state, code challenge, listener 경로 유지 |
| Debug build | PASS | 경고 0, 오류 0 |
| Debug test | PASS | 127/127 |
| Release build | PASS | 경고 0, 오류 0 |
| Release test | PASS | 127/127 |
| Format/diff check | PASS | `dotnet format --verify-no-changes`, `git diff --check` |
| RC2 패키지 | PASS | ZIP·설치본·manifest·SHA256SUMS 생성 |
| Portable 격리 smoke | PASS | 진단 종료 코드 0, DB 해시 유지, 프로세스/포트 정리 |
| 실제 설치 RC1→RC2 | PASS | Inno 공식 옵션 exit code 0, 동일 경로/AppId 유지 |
| 설치 후 사용자 데이터 | PASS | SQLite integrity ok, DB SHA-256·fingerprint·카운트 유지 |
| 설치된 RC2 5회 실행 | PASS | 빠른 5회 실행 중 동일 경로 프로세스 1개 |
| 실제 Chrome 계정 선택 화면 | 사용자 수동 확인 필요 | 인증/브라우저 계정 선택 자동화는 수행하지 않음 |
| Defender 대상 검사 | PASS | 탐지 결과 없음 |
| Authenticode | 사용자 승인 필요 | 설치본 `NotSigned` |
| Yulbyte site format script | 미실행 | `package.json`에 `format` script가 없어 실행할 수 없음 |

## RC2 산출물

- `dist-rc\0.2.0-rc.2\Hanki-0.2.0-rc.2-win-x64.zip`
  - SHA-256 `D04D86A7711468DD8B72768E7ACEFAE7BACAEC3253EDED57389919B212D8F703`
  - 66,986,209 bytes
- `dist-rc\0.2.0-rc.2\HankiSetup-0.2.0-rc.2.exe`
  - SHA-256 `AAC737A4564ACD9F8FD0B539EAD4E33F2A041925BCF02F9D9199948E773FF0FE`
  - 50,261,906 bytes
- Payload EXE SHA-256 `7C5E566F51C025FAAFF53949CE282318250FAF6A1DAE50D568897774BE2F8BBD`

정적 파일 목록은 EXE와 `hanki.auth.config.json` 두 개입니다. config에는 공개 Supabase URL·publishable key·redirect URI만 포함됩니다.

## 사용자 데이터 보존

- DB: 24,576 bytes, SHA-256 `5F9449D836409176487B909404B7B2F3896C5B3823ADF03B701647631EB0C9F1`
- SQLite integrity: `ok`
- 단축어 7개, 사용 횟수 총합 14, 설정 9개
- 제외 프로세스 4개, 제외 사이트 0개
- 논리 fingerprint `f55ba25156f68a7e663f4e0097871d4df6c940295ec86075ca0a428f79d01560`
- 기존 인증 세션은 보존되었으며 세션 값은 보고서에 출력하지 않습니다.

## 보안 검사

RC2 payload/installer에는 service_role, client_secret, JWT 값, auth.session, hanki.db, `.env.local`, PDB 파일, 개발 PC 절대경로가 포함되지 않았습니다. 단일 파일 번들에서 발견되는 `access_token`/`refresh_token` 문자열은 인증 라이브러리 필드명이며 credential 값이 아닙니다. `D:\a\...` 형태의 .NET 프레임워크 심볼 문자열은 PDB 파일이나 로컬 개발 경로가 아닙니다.

## 작업 제한

commit, push, tag, GitHub Release, Vercel 배포, Supabase migration, 공개 다운로드 활성화는 실행하지 않았습니다.
