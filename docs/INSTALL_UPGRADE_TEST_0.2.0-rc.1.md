# 한키 Windows 0.2.0-dev → 0.2.0-rc.1 설치·업그레이드 검증

## 결과

- 백업: `C:\Users\MSI\Desktop\개발\_hanki_rc_backup\20260804-233255` — **PASS**
- RC 설치본: `dist-rc\0.2.0-rc.1\HankiSetup-0.2.0-rc.1.exe` — **PASS**
- 공식 옵션 `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-`, exit code 0 — **PASS**
- 설치 경로 `%LocalAppData%\Programs\Yulbyte\Hanki`, AppId 유지, 시작 메뉴·제거 등록 — **PASS**
- FileVersion `0.2.0.0`, ProductVersion `0.2.0-rc.1`, Product `한키`, Company `Yulbyte` — **PASS**
- 설치 전후 DB integrity·해시·논리 기준값 — **PASS**
- UAC/SmartScreen 우회 없음. 로컬 서명 상태 `NotSigned` — **사용자 승인 필요**

RC 후보는 PathMap을 적용해 재생성했으며 payload 단일 실행 파일에서 PDB·개발 절대경로가 제거된 것을 재확인했다. Inno Setup 6.7.3으로 설치본을 다시 컴파일하고 공식 설치를 재실행했으며 exit code 0, DB 보존, 프로세스 0을 확인했다. 새 ZIP/설치본 해시는 `TEST_REPORT_0.2.0-rc.1.md`와 `SHA256SUMS-0.2.0-rc.1.txt`에 반영했다.

설치된 RC는 마지막에 완전 종료했으며 Hanki 프로세스 0개, 포트 43289 해제 상태다. 기존 0.1.1와 0.2.0-dev 산출물은 덮어쓰지 않았다.
