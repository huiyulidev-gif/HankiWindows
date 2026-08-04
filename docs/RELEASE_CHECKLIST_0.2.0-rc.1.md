# 0.2.0-rc.1 출시 체크리스트

- [x] 전용 백업·manifest·DB SHA-256
- [x] 0.2.0-rc.1 버전 동결
- [x] Debug/Release build 및 126/126 test
- [x] RC Portable ZIP·Inno 설치본·SHA256SUMS
- [x] 공개 auth config만 포함
- [x] RC 정적 secret/PDB/DB/경로 검사
- [x] PathMap 적용 재생성 및 개발 절대경로 재검사
- [x] Portable baseline smoke
- [x] 실제 0.2.0-dev → RC 업그레이드
- [x] 사용자 DB·논리 기준값 보존
- [x] 단일 인스턴스·빠른 5회·트레이 복원 프로세스 검증
- [x] Defender 검사
- [x] Yulbyte `/hanki` RC 비공개 상태
- [x] 1시간 격리 soak — **PASS** (59개 샘플, 자동 종료·정리 확인)
- [ ] 수동 아이콘·DPI·입력 치환 — **자동화 불가**
- [ ] 코드 서명·SmartScreen 정책 — **사용자 승인 필요**
- [ ] Git commit/push/tag/Release/Vercel/Supabase migration — **미실행**
