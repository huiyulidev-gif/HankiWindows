# 한키 (Hanki) 0.2.0

한키는 Windows 어디서나 짧은 단축어를 긴 문장으로 바꾸는 로컬 우선 텍스트 확장 앱입니다.
예를 들어 메모장이나 지원되는 입력창에서 `;문의`를 입력하고 Space를 누르면 다음 문장으로 바뀝니다.

> 안녕하세요. 문의해 주셔서 감사합니다.

0.2.0은 선택적 Google 계정 연결을 지원하며 로그인할 때 Google 계정 선택 화면을 엽니다. 로그인하지 않아도 모든 로컬
단축어 기능을 사용할 수 있으며, 단축어 클라우드 동기화·원격 분석·광고는 없습니다.
네트워크 통신은 사용자가 로그인을 선택한 경우에만 인증을 위해 발생합니다.

## 설치

0.2.0 설치본은 GitHub Release `v0.2.0`에서 제공합니다.
0.1.1 후보는 검증 보존을 위해 `dist/HankiSetup-0.1.1.exe`에 그대로 유지합니다.
두 개발 후보 모두 아직 GitHub Release에는 게시하지 않았습니다.
현재 공개 버전은 [HankiSetup-0.1.0.exe](https://github.com/huiyulidev-gif/HankiWindows/releases/download/v0.1.0/HankiSetup-0.1.0.exe)입니다.

사용자 계정 범위에 설치되므로 관리자 권한은 필요하지 않습니다.
바탕 화면 바로가기는 설치 중 선택할 수 있습니다.
설치·동일 버전 덮어쓰기·제거·재설치와 LocalAppData DB 보존을 Windows에서 실제 검증했습니다.

Windows용 코드 서명 인증서가 아직 없으므로 Microsoft Defender SmartScreen이 경고를 표시할 수 있습니다.
한키는 경고를 우회하거나 보안 기능을 끄도록 안내하지 않습니다. 출처를 신뢰할 수 있을 때만 실행하세요.

## Portable 버전

0.2.0 Portable은 GitHub Release `v0.2.0`에서 제공합니다.
보존된 0.1.1 후보는 `dist/Hanki-0.1.1-win-x64.zip`이며, 현재 공개 Portable 버전은
[Hanki-0.1.0-win-x64.zip](https://github.com/huiyulidev-gif/HankiWindows/releases/download/v0.1.0/Hanki-0.1.0-win-x64.zip)입니다.

1. ZIP을 원하는 폴더에 압축 해제합니다.
2. `Hanki.exe`를 실행합니다.
3. 첫 실행 안내를 확인합니다.

.NET 8 런타임을 포함한 x64 self-contained 배포본이므로 별도 .NET 설치가 필요하지 않습니다.
실행 파일 위치를 옮긴 뒤 Windows 자동 실행을 사용하는 경우 설정에서 자동 실행을 껐다가 다시 켜 경로를 갱신하세요.

- [최신 Release 페이지](https://github.com/huiyulidev-gif/HankiWindows/releases/latest)
- [SHA-256 체크섬](https://github.com/huiyulidev-gif/HankiWindows/releases/download/v0.1.0/SHA256SUMS.txt)

## 단축어 사용

1. 한키에서 **단축어 추가**를 누릅니다.
2. 단축어와 변환 문장을 입력하고 저장합니다.
3. 지원되는 다른 프로그램의 일반 텍스트 입력창에서 단축어를 입력합니다.
4. 바로 뒤에서 Space를 누릅니다.

단축어는 대소문자를 구분합니다. 앞뒤 공백은 저장할 때 제거됩니다.
동일한 단축어는 중복 저장할 수 없습니다.

## 시스템 트레이

창을 닫으면 기본적으로 시스템 트레이로 이동합니다. 트레이 아이콘을 클릭하거나 트레이 메뉴의 **한키 열기**를 선택하면 창을 다시 엽니다.
창을 닫은 뒤 시작 메뉴, 바탕 화면 바로가기 또는 `Hanki.exe`로 한키를 다시 실행해도 중복 프로세스를 만들지 않고 기존 창을 복원합니다.
트레이 메뉴에서 변환 켜기/끄기, 새 단축어 추가, 설정, 완전히 종료를 사용할 수 있습니다.

키보드 감지와 관련 리소스를 해제하려면 트레이 메뉴의 **완전히 종료**를 선택하세요.

## 설정

- **Windows 시작 시 자동 실행**: 사용자가 켠 경우에만 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`에 등록합니다.
- **Space로 단축어 변환**: 0.2.0에서 안정 지원하는 종료 키입니다.
- **Enter/Tab**: 향후 실험 기능을 위한 설정 구조만 있으며 현재 실제 변환은 지원하지 않습니다.
- **제외 프로그램**: 프로세스 이름을 한 줄에 하나씩 입력합니다.

기본 제외 목록은 `cmd.exe`, `powershell.exe`, `WindowsTerminal.exe`, `mstsc.exe`입니다.
메모장과 브라우저는 기본 제외하지 않습니다.

## 데이터와 JSON 백업

로컬 데이터 위치:

`%LocalAppData%\Yulbyte\Hanki\hanki.db`

개인정보를 포함하지 않는 오류 로그 위치:

`%LocalAppData%\Yulbyte\Hanki\Logs`

설정 화면에서 JSON 가져오기/내보내기를 사용할 수 있습니다. 백업에는 스키마 버전, 단축어, 한키 설정만 들어갑니다.
JSON 백업에는 토큰, 비밀번호, 이메일, 기기 식별정보가 포함되지 않습니다. 로그인 토큰은
별도 `auth.session`에 Windows DPAPI CurrentUser 범위로 암호화 저장됩니다.
중복 단축어는 건너뛰기, 덮어쓰기, 이름 바꾸기 중 하나를 선택할 수 있습니다.

## 삭제

- 설치 버전: Windows의 **설치된 앱**에서 한키를 제거합니다. 제거 과정에서 로컬 데이터도 지울지 묻습니다.
- 명령줄 무인 제거: 안전을 위해 로컬 데이터를 묻지 않고 보존합니다.
- Portable 버전: 한키를 완전히 종료하고 압축을 푼 폴더를 삭제합니다.
- 로컬 데이터까지 직접 지우려면 한키를 종료한 뒤 `%LocalAppData%\Yulbyte\Hanki`를 삭제합니다.

## 개인정보 보호

한키는 Space 키만 전역 감지하고, Space 입력 직후 포커스된 일반 텍스트 요소의 커서 앞 짧은 범위만 메모리에서 일시적으로 확인합니다.
전체 키 입력을 저장하지 않고, 입력 문장이나 변환 문장, 클립보드를 로그에 남기지 않습니다.
`IsPassword=true`인 비밀번호 입력 요소와 판별할 수 없는 입력 영역에서는 변환을 건너뜁니다.

자세한 내용은 [개인정보 안내](docs/PRIVACY.md)를 확인하세요.

## 알려진 제한

관리자 권한 프로그램, 보안 데스크톱, 일부 게임, 일부 커스텀 에디터, UI Automation을 제공하지 않는 입력창에서는 변환하지 않을 수 있습니다.
현재 안정 지원 종료 키는 Space뿐입니다.

자세한 내용과 앱별 수동 검증 상태는 [알려진 제한](docs/KNOWN_LIMITATIONS.md)과
[테스트 보고서](docs/TEST_REPORT.md)를 확인하세요.

Chrome/Edge용 로컬 입력 페이지는 [브라우저 입력 테스트](docs/BROWSER_INPUT_TEST.html)입니다.
Chrome, Edge, Discord, KakaoTalk은 최종 검증에서 안전한 전용 제어 수단 또는 확실한 자기 채팅 식별이 없어 실제 입력 테스트를 하지 않았습니다.
일반 WPF 입력창과 다중행 입력창은 정상 변환됐고, WPF PasswordBox는 가짜 테스트 문자열을 변환하지 않았습니다.
Windows 자동 실행의 HKCU 등록·중복 없음·해제는 실제 확인했지만, PC 재부팅/로그인 검증은 하지 않았습니다.

## 0.1.1 변경 내역

- Fixed: 두 번째 실행 시 기존 창이 열리지 않던 문제
- Improved: 세션별 단일 인스턴스 활성화 신호 처리
- Improved: 숨김·최소화·트레이 상태의 창 복원과 전면 활성화

지원: huiyuli.dev@gmail.com
