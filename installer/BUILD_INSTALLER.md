# 설치 프로그램 빌드

1. Inno Setup 6을 설치합니다.

   `winget install --id JRSoftware.InnoSetup --exact`

2. `ISCC.exe` 위치를 확인합니다. 설치 방식에 따라 다음 중 하나입니다.

   `"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer/Hanki.iss`

   `"%LocalAppData%\Programs\Inno Setup 6\ISCC.exe" installer/Hanki.iss`

3. 프로젝트 루트에서 해당 명령을 실행하면 `dist/HankiSetup-0.1.1.exe`가 생성됩니다.

0.2.0 개발 후보는 기존 0.1.1 산출물을 보호하기 위해 별도 스크립트와 경로를 사용합니다.

```powershell
dotnet publish src/Hanki.App/Hanki.App.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:DebugType=None -p:DebugSymbols=false `
  -o dist-dev/0.2.0/Hanki-0.2.0-dev-win-x64

& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" installer/Hanki.Dev.iss
```

소스 기본 publish에는 실제 `hanki.auth.config.json`을 넣지 않습니다. 일반 사용자가
별도 파일을 복사하지 않아도 로그인 버튼을 사용할 수 있도록, 공개 Supabase URL·
publishable key·고정 loopback redirect만 담은 배포 후보 설정은 별도 install-test
패키징 단계에서 실행 파일 옆에 포함할 수 있습니다. `auth.session`, token, service_role,
client secret, 사용자 DB는 어떤 패키지에도 넣지 않습니다. 개발 후보 출력은
`dist-dev/0.2.0` 또는 별도 install-test 경로에만 생성하며 `dist`를 수정하지 않습니다.

설치 스크립트는 사용자 단위 설치, 시작 메뉴 바로가기, 선택적 바탕 화면 바로가기,
기존 버전 업그레이드, 설치 후 실행, 대화형 제거 시 로컬 데이터 삭제 여부 확인을 지원합니다.
무인 제거에서는 사용자 응답을 추측하지 않고 로컬 데이터를 항상 보존합니다.
