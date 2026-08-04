# 한키 Windows Google 로그인 설정 (0.2.0-dev)

한키는 Google 비밀번호를 받지 않습니다. 시스템 기본 브라우저에서 Supabase Auth의
Authorization Code + PKCE(S256) 흐름을 사용하며, 데스크톱 앱에는 공개 가능한
Supabase project URL과 publishable/anon key만 둡니다.

## Supabase 설정

1. Supabase Dashboard에서 Yulbyte 프로젝트를 연다.
2. **Authentication → URL Configuration → Redirect URLs**에 아래 주소를 정확히 추가한다.

   ```text
   http://127.0.0.1:43289/auth/callback
   ```

3. **Authentication → Providers → Google**이 기존 웹 로그인에 사용하는 Google
   provider로 활성화돼 있는지 확인한다. Windows 앱 때문에 Google Cloud Console에
   loopback URL을 추가할 필요는 없다. Google은 Supabase callback으로 돌아오고,
   Supabase가 위 loopback URL로 다시 보낸다.

## 로컬 설정 파일

`src\Hanki.App\hanki.auth.config.json.example`을 실행 파일 옆의
`hanki.auth.config.json`으로 복사한 뒤 다음 공개 값만 채운다.

```json
{
  "supabaseUrl": "https://PROJECT.supabase.co",
  "supabasePublishableKey": "PUBLIC_PUBLISHABLE_OR_ANON_KEY",
  "redirectUri": "http://127.0.0.1:43289/auth/callback"
}
```

- `sb_secret_`, `service_role`, service-role JWT, Google Client Secret은 앱에 넣지 않는다.
- 소스 트리의 실제 설정 파일은 `.gitignore` 대상이다. 배포 후보에는 공개 Supabase
  URL·publishable key·고정 redirect만 담은 설정을 실행 파일 옆에 포함할 수 있지만,
  `auth.session`, token, service_role, client secret, 사용자 DB는 넣지 않는다.
- URL은 HTTPS origin만 허용하고 redirect URI는 위 고정값만 허용한다.

## 로컬 확인 순서

1. Supabase redirect 등록을 저장한다.
2. install-test 후보에는 공개 설정이 이미 포함되어 있는지 정적 검사한다. 포함되지
   않은 로컬 개발 빌드에서만 실행 파일 옆에 실제 config를 만든다.
3. 한키를 실행하고 계정 탭에서 **Google로 로그인**을 누른다.
4. 기본 브라우저에서 계정을 선택하고 앱에 이름·이메일·프로필 이미지가 표시되는지 본다.
5. 완전히 종료 후 재실행해 세션 복원을 확인한다.
6. 로그아웃 후 `%LocalAppData%\Yulbyte\Hanki\auth.session`이 삭제되는지 확인한다.

자동 테스트는 실제 Google 계정이나 비밀번호를 사용하지 않는다. 전체 수동 항목은
`docs\AUTH_MANUAL_TEST_CHECKLIST.md`를 따른다.

## 0.2.0-rc.1 패키징 확인

RC에는 공개 Supabase URL·publishable key·고정 loopback redirect만 포함된
`hanki.auth.config.json`을 실행 파일 옆에 둔다. `.env.local`, `auth.session`, 사용자 DB,
token, service_role, client secret은 포함하지 않는다 — **PASS**.
