# 0.2.0 설치·업그레이드 검증

## 백업

- `C:\Users\MSI\Desktop\개발\_hanki_020_release_backup\20260805-075912`
- 사용자 DB·auth.session·로그·Git 상태·RC/기존 배포 해시를 보관했습니다.
- 복구 가능 여부: PASS

## RC2 → 0.2.0

- Inno Setup 6.7.3 공식 옵션 사용: PASS
- 설치 exit code: PASS (0)
- 설치 경로/AppId: PASS (기존 경로와 동일)
- ProductVersion: PASS (`0.2.0`)
- FileVersion: PASS (`0.2.0.0`)
- 제품/회사: PASS (`한키` / `Yulbyte`)
- 시작 메뉴 및 제거 등록: PASS
- Authenticode: 사용자 승인 필요 (`NotSigned`)

## 사용자 데이터

수동 검증으로 사용 횟수가 14에서 15로 증가했으므로 설치 직전 유효 기준은 15로 기록했습니다.

- SQLite integrity: PASS (`ok`)
- 단축어: 7개, PASS
- 사용 횟수 총합: 15, PASS
- 설정: 9개, PASS
- 제외 프로세스: 4개, 제외 사이트: 0개, PASS
- 자동 시작/트레이/기능 플래그: PASS
- auth.session: PASS, 보존
- 설치 후 DB 해시: `AF5F0716BA130A7F98E041C89B76ACD07FE8500807A74F603C9EF93FB1B4A392`

## 회귀 및 정리

- 정식 앱 실행/계정 탭/로고: PASS
- 빠른 5회 실행 단일 인스턴스: PASS
- 정상 종료 후 프로세스 0개: PASS
- `127.0.0.1:43289` listener 0개: PASS
- rollback: 미실행 (critical failure 없음)
