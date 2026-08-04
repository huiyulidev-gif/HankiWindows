# Hanki Windows 0.1.1 → 0.2.0-dev 설치·업그레이드 검증

검증 범위: 기존 0.1.1 설치를 별도 install-test 후보로 업그레이드한 뒤 공식 제거 프로그램으로 제거하고 동일 후보를 재설치했다. 사용자 데이터와 기존 0.1.1 배포 파일은 보존했다. 실행 과정에서 commit, push, tag, Release, Vercel 배포, Supabase migration은 수행하지 않았다.

## 결과 요약

| 항목 | 상태 | 결과 |
|---|---|---|
| 기존 0.1.1 사전 상태 기록 | PASS | 설치 경로·버전·레지스트리·시작 메뉴·DB 상태를 기록했다. 단축어 본문과 개인정보는 문서에 쓰지 않았다. |
| 사용자 데이터 백업 | PASS | `C:\Users\MSI\Desktop\개발\_hanki_upgrade_backup\20260726-161352`에 DB와 관련 파일 및 SHA-256 목록을 보관했다. |
| 배포 인증 설정 | PASS | install-test payload에 공개 Supabase URL·publishable key·loopback redirect만 포함된 `hanki.auth.config.json`이 포함됐다. service_role, client secret, token, auth.session, 사용자 DB는 포함되지 않았다. |
| install-test 후보 정적 검사 | PASS | ZIP/설치본·payload에서 secret/token/PDB/DB/개발 절대경로가 검출되지 않았다. |
| 0.1.1 정상 종료 사전 조건 | 미실행 | 업그레이드 시작 시 설치된 0.1.1 프로세스가 이미 0개였다. |
| 0.1.1 → 0.2.0-dev 업그레이드 | PASS | 공식 Inno Setup 옵션(`/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-`)으로 exit code 0. 설치 경로와 AppId가 유지됐다. |
| 업그레이드 직후 데이터 보존 | PASS | 앱 실행 전에 DB SHA-256이 백업과 일치했고, 논리 데이터도 단축어 7개·사용 횟수 합계 14·설정 9개로 유지됐다. |
| 설치본 UI·단일 인스턴스 | PASS | 계정 탭 로그인 버튼 활성화, 로고 표시, 기존 단축어 표시, X→트레이, 재실행 창 복원, 빠른 5회 실행 시 프로세스 1개를 확인했다. 포트 43289는 열리지 않았다. |
| 공식 제거 | PASS | 공식 uninstaller exit code 0. 설치 등록·시작 메뉴·설치 파일이 제거됐고 사용자 DB는 보존됐다. 트레이에 숨은 프로세스는 창 복원이 되지 않아 경로·버전이 일치하는 단일 PID만 마지막 수단으로 종료했다. |
| 동일 0.2.0-dev 재설치 | PASS | 같은 후보로 재설치 exit code 0. 버전·아이콘·시작 메뉴·제거 등록·인증 설정·사용자 데이터가 복원됐다. |
| 최종 상태 | PASS | 0.2.0-dev 설치, Hanki 프로세스 0개, 43289 FREE, DB SHA-256 일치, auth.session 없음. 원래 트레이 설정을 복원했다. |
| rollback | 미실행 | critical failure가 발생하지 않았다. |

## 산출물 및 식별값

- install-test 후보: `C:\Users\MSI\Desktop\개발\HankiWindows\dist-dev\0.2.0-install-test`
- 후보 ZIP SHA-256: `BE2CBC98AC5A13F09FF0502C5201668DF45D93BFFF487F43FA558ED763808DD7`
- 후보 설치본 SHA-256: `10D8775EC1055C5707D1394D1B01822E8A8FCD0B08A71E3F07A7730039EA0FB5`
- 설치된 Hanki.exe: FileVersion `0.2.0.0`, ProductVersion `0.2.0-dev`, Product `한키`, Company `Yulbyte`
- 설치된 Hanki.exe SHA-256: `5F0CA4FFF78B501961355DDA5E74C0B4EE76AC61584AA7C54BCA66706BEC084B`
- 백업 DB SHA-256 및 최종 DB SHA-256: `5F9449D836409176487B909404B7B2F3896C5B3823ADF03B701647631EB0C9F1`
- 보존한 0.1.1 ZIP SHA-256: `334530975AD50268765FD5641F053104AABE9E074A55B7FBCEA95F7ABFC61681`
- 보존한 0.1.1 설치본 SHA-256: `4DBA2D30E174FCAD3C646EEB7A77E8C70959EA2D695FFCFBA3B2B3D0786FAFE1`
- 보존한 0.1.1 SHA 목록 SHA-256: `38C89D87F220B43034E3DAEB6D6C940872871324918414CD629C23716BFBEEB1`

## 제한 및 미실행 항목

- 이번 업그레이드 세션에서 실제 Google 인증 완료를 다시 시작하지 않았다. 계정 탭의 로그인 버튼 활성화만 확인했으며, 기존 실제 Google 로그인 PASS 기록은 유지한다.
- 설치본·설치 프로그램의 Authenticode 상태는 `NotSigned`다. 배포 전 코드 서명 및 SmartScreen 정책 결정은 사용자 승인 필요다.
- UAC/SmartScreen은 우회하지 않았고 로컬 설치 명령에서 추가 프롬프트가 나타나지 않았다. 다른 PC·인터넷 다운로드 시나리오는 미실행이다.
- 문서·로그에는 이름, 이메일, token, key, callback query, 단축어 본문을 기록하지 않았다.

## 2026-08-04 별도 자율 유지보수 후보

- 후보 경로: `C:\Users\MSI\Desktop\개발\HankiWindows\dist-dev\0.2.0-autonomous-20260804`
- Portable 실행 파일 SHA-256: `362F41FEA00157A7D2CD2BB07423D3C6DFEB3A2FBBE519EA563AA50ED2504737`
- Portable ZIP SHA-256: `9289071673A37BD5FF41A266281478486A4114E059431ADD7E1DB1DE7697316D`
- FileVersion `0.2.0.0`, ProductVersion `0.2.0-dev`, 공개 설정 키 3개만 포함, PDB/DB/auth.session 0개 — **PASS**
- 실제 사용자 설치본 교체·설치·로그인 — **미실행** (기존 설치본과 사용자 데이터 보호)

### 자율 Portable smoke 추가

- 후보를 별도 데이터 디렉터리로 30초 실행하여 auth config 로딩 및 정상 진단 종료를 확인했다 — **PASS**
- 후보 프로세스 0, 포트 43289 해제, `auth.session` 미생성, 사용자 DB SHA-256 보존 — **PASS**
- 실제 설치된 0.2.0-dev와 기존 0.1.1 패키지는 변경하지 않았다. 서명 없는 후보의 공개 배포는 **사용자 승인 필요**.

### 진단 상한 보강 Release 후보

- 별도 후보: `dist-dev\0.2.0-autonomous-20260804-r2`.
- EXE SHA-256 `9580F657116B69016BA78DEF53A8C8FD83B4C46FB4BFD870A402624E73619983`, ZIP SHA-256 `EE37B19264177C3F984BA40CB49A30621C88130F21C1FA0F7BE2BA41EFA5BFCB`.
- FileVersion `0.2.0.0`, ProductVersion `0.2.0-dev`, 공개 설정 3개 키만 포함, PDB/DB/auth.session/개발 절대경로 없음, 30초 smoke 정상 종료 — **PASS**.
