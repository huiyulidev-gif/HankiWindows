# 성능 기준 측정 — 0.2.0-dev

측정일: 2026-07-26, Windows x64, .NET 8.0.420

실제 사용자 AppData와 실행 중인 설치본을 건드리지 않기 위해
`HANKI_DATA_DIRECTORY`로 임시 디렉터리를 지정하고 진단용 single-instance
namespace를 사용했다. 시작 시간은 프로세스 생성부터 `WaitForInputIdle`까지이며,
working set/handle/thread는 시작 직후 단기 표본이다. 임의 기준으로 PASS 판정하지 않는다.

| 구성 | 실행 | 정상 종료 | 시작 평균 (범위) | Working set 평균 (범위) | Handle 평균 | Thread 평균 |
|---|---:|---:|---:|---:|---:|---:|
| Debug | 20 | 20 | 383.44ms (352.20–455.93) | 55.10MB (53.45–62.64) | 274.00 | 13.05 |
| Release | 20 | 20 | 461.37ms (380.98–1658.66) | 53.92MB (52.84–55.16) | 273.35 | 13.00 |

- 비정상 exit code: 0
- 각 종료 후 진단 빌드 잔류 프로세스: 0
- Release 빠른 10회: 700ms 시점 프로세스 1개, 전체 종료 후 0개
- 실행 중이던 사용자 설치본은 계속 실행됐으며 종료·변경하지 않았다.

## 유휴 관찰

숨김 창/트레이 상태의 Release 격리 프로세스를 15분 동안 관찰한다.

- 5초: working set 153.63MB, private 80.89MB, CPU 2656.25ms, handle 739, thread 26
- 6.27분 중간 표본: working set 136.18MB, private 65.45MB, CPU 3203.12ms,
  handle 756, thread 23
- 15.21분: working set 136.92MB, private 65.88MB, CPU 3265.62ms,
  handle 861, thread 19, 응답 정상
- 진단 deadline 안에 정상 종료했고 잔류 진단 프로세스는 0이었다.

5초 대비 working set은 16.71MB, private memory는 15.01MB 감소했고 CPU 증가는
609.37ms였다. 메모리의 지속 증가는 관찰되지 않았다. 반면 handle은 739에서 861로
122개 증가했다. 같은 시간 thread는 26에서 19로 감소했고 오류 로그·응답 정지는
없었지만, 이 결과만으로 handle 누수가 없다고 판정하지 않는다. 장시간 실제 트레이
사용과 반복 창 열기에서 handle이 계속 선형 증가하는지 후속 관찰이 필요하다.

프로필 이미지가 있는 실계정 상태와 실제 트레이 좌클릭 전후 측정도 수동 확인 대상이다.
