# 한키 Windows 개인정보 구현 메모

## 0.2.0-rc.1 동결 기준

- 단축어·사용 횟수·설정은 `%LocalAppData%\Yulbyte\Hanki\hanki.db`에만 저장한다.
- Google 인증 세션은 별도 `auth.session`에 DPAPI로 보호해 저장하며, 로그아웃 시 삭제한다.
- RC Portable/설치본에는 `hanki.db`, `auth.session`, 로그, 실제 사용자 설정을 포함하지 않는다.
- 배포 설정 파일에는 Supabase 공개 URL·publishable key·고정 loopback redirect만 포함한다.
- service_role, secret key, Google Client Secret, Access/Refresh Token은 사용하거나 패키징하지 않는다.
- RC 정적 검사와 실제 설치 후 DB 해시·SQLite integrity·논리 기준값을 비교했다 — **PASS**.

실제 이름, 이메일, 단축어 원문, token, callback query는 테스트 로그와 보고서에 기록하지 않는다.
