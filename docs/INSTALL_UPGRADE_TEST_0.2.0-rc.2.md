# 0.2.0-rc.2 설치·업그레이드 검증

## 사전 상태 및 백업

- 백업: `C:\Users\MSI\Desktop\개발\_hanki_rc2_backup\20260805-072349`
- 사용자 DB와 `auth.session`, 로그를 별도 폴더에 복사하고 SHA-256 및 복구 manifest를 기록했습니다.
- 설치 전 RC1 경로는 `C:\Users\MSI\AppData\Local\Programs\Yulbyte\Hanki`였고, DB는 24,576 bytes였습니다.

## 설치 실행

공식 Inno Setup 실행 파일에 `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-`만 전달했습니다.

- 설치 exit code: PASS (0)
- 설치 경로 유지: PASS
- AppId 유지: PASS
- ProductVersion: PASS (`0.2.0-rc.2`)
- FileVersion: PASS (`0.2.0.0`)
- 제품명/회사명: PASS (`한키` / `Yulbyte`)
- 제거 등록: PASS (`0.2.0-rc.2`)
- 시작 메뉴 링크: PASS (`한키\한키.lnk`)
- 공개 auth config 포함: PASS
- Authenticode: 사용자 승인 필요 (`NotSigned`)

## 데이터 비교

설치 직후 앱 실행 전에 DB를 비교했고, 이후 정상 실행·종료 후에도 동일했습니다.

- SQLite integrity: PASS (`ok`)
- DB SHA-256: PASS (사전/사후 동일)
- 단축어 7개: PASS
- 사용 횟수 총합 14: PASS
- 설정 9개 및 기능/자동 시작/트레이 플래그: PASS
- 제외 프로세스 4개, 제외 사이트 0개: PASS
- 논리 fingerprint: PASS (사전/사후 동일)
- 기존 auth.session 보존: PASS

## 설치된 RC2 회귀

- 앱 실행 및 버전 메타데이터: PASS
- 빠른 5회 실행: PASS (동일 실행 경로 프로세스 1개)
- 정상 CloseMainWindow 후 트레이 잔류 시 대상 경로·버전을 재확인하고 해당 RC2 PID만 정리: PASS
- 최종 프로세스 0개, 포트 43289 0개: PASS
- 실제 계정 선택 UI: 사용자 수동 확인 필요

## 롤백

critical failure가 발생하지 않아 rollback은 실행하지 않았습니다. 백업은 별도 보관되어 복구 가능합니다.
