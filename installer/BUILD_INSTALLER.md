# 설치 프로그램 빌드

1. Inno Setup 6을 설치합니다.

   `winget install --id JRSoftware.InnoSetup --exact`

2. `ISCC.exe` 위치를 확인합니다. 설치 방식에 따라 다음 중 하나입니다.

   `"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer/Hanki.iss`

   `"%LocalAppData%\Programs\Inno Setup 6\ISCC.exe" installer/Hanki.iss`

3. 프로젝트 루트에서 해당 명령을 실행하면 `dist/HankiSetup-0.1.0.exe`가 생성됩니다.

설치 스크립트는 사용자 단위 설치, 시작 메뉴 바로가기, 선택적 바탕 화면 바로가기,
기존 버전 업그레이드, 설치 후 실행, 대화형 제거 시 로컬 데이터 삭제 여부 확인을 지원합니다.
무인 제거에서는 사용자 응답을 추측하지 않고 로컬 데이터를 항상 보존합니다.
