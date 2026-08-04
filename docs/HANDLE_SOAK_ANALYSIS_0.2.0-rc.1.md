# 한키 Windows 0.2.0-rc.1 handle/메모리 격리 관찰

측정 파일: `docs\artifacts\performance\handle-soak-0.2.0-rc.1.csv`

- 격리 DB와 `HANKI_DATA_DIRECTORY`를 사용했으며 실제 사용자 DB와 분리했다.
- `is_enabled=false` 기준으로 실제 입력 치환은 수행하지 않았다.
- 설치된 RC 프로세스의 실행 경로와 PID를 확인한 뒤 1분 간격으로 측정했다.
- 진단 종료 인자 `--diagnostic-exit-after-ms=3600000`을 사용했다.

## 결과

- 상태: **PASS**
- 프로세스 시작: 2026-08-04 23:44:47 KST
- 자동 종료 확인: 2026-08-05 00:45:45 KST 확인 시 대상 PID와 monitor PID 모두 종료
- 측정 샘플: 59개 (첫 샘플부터 마지막 샘플까지 58.2분, 프로세스 실행은 1시간 인자 완료 후 종료)
- working set: 141.06–161.63 MB (평균 142.62 MB)
- private memory: 71.11–89.05 MB (평균 72.27 MB)
- handles: 724–950 (평균 857.7)
- threads: 16–22
- GDI objects: 36–36
- USER objects: 45–47
- CPU 누적 최대: 3.062초
- 샘플 누락·중복 프로세스·응답 없음: 없음
- 종료 후 Hanki 프로세스: 0개, 포트 43289: 해제
- 격리 DB SHA-256: 백업과 일치, integrity `ok`

수치의 변동은 관찰 중 자연스러운 범위였고 단조 증가하는 handle/GDI/USER 또는 private memory 패턴은 확인되지 않았다. 실제 앱 입력과 다른 앱 상호작용은 안전한 자동화 범위를 벗어나므로 별도 수동 점검 대상으로 남긴다.
