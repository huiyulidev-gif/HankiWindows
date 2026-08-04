# 한키 Windows 0.2.0 최종 출시 계획

## 현재 판정

`RC_READY_WITH_MANUAL_CHECKS`

0.2.0-rc.1은 로컬 설치·업그레이드·데이터 보존·자동 테스트를 통과한 비공개 후보다. 공개 전에는 코드 서명, SmartScreen 인터넷 환경, 아이콘/DPI/실제 입력 치환을 수동 확인한다.

## 공개 전 순서

1. 수동 점검표 완료
2. Authenticode 인증서 적용 및 서명 재검증
3. 서명 후 Portable/설치본 SHA-256 재생성
4. Yulbyte release 설정에서 `published=true`와 승인된 URL만 변경
5. 사용자 승인 후에만 commit/tag/Release/Vercel 배포를 별도 실행

현재 `published=false`, 다운로드 URL 없음, Supabase migration 없음 상태를 유지한다.
# 0.2.0-rc.2 후보 상태 (2026-08-05)

- 현재 후보: `0.2.0-rc.2`
- 판정: `RC2_READY_FOR_MANUAL_ACCOUNT_PICKER_TEST`
- RC1 보존: PASS
- OAuth `prompt=select_account`: PASS
- Windows Debug/Release test: PASS (127/127)
- 실제 RC 업그레이드 및 사용자 데이터 보존: PASS
- Chrome 계정 선택 화면: 사용자 수동 확인 필요
- 서명/SmartScreen 및 공개 배포 전환: 사용자 승인 필요
- 공개 다운로드, commit, push, tag, GitHub Release, Vercel 배포, Supabase migration: 미실행

상세 절차는 `MANUAL_ACCOUNT_PICKER_TEST_0.2.0-rc.2.md`, `RELEASE_READINESS_0.2.0-rc.2.md`를 참조하세요.
