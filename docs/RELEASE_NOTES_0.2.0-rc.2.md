# 한키 Windows 0.2.0-rc.2 출시 후보 변경 내역

- Google OAuth 계정 선택을 위해 `prompt=select_account` 추가
- 권한 동의 반복을 유발하는 `prompt=consent` 미사용
- `login_hint` 미사용
- PKCE, state, redirect URI, loopback listener, DPAPI 세션 저장 유지
- URL query parsing 및 중복/인코딩/회귀 테스트 보강
- RC1 산출물은 별도 경로와 SHA-256으로 보존

공개 배포 링크와 서명은 아직 활성화하지 않았습니다.
