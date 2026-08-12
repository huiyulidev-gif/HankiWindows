# 한키 0.2.1 정식 릴리스 검증

검증일: 2026-08-12 / Windows x64 / .NET SDK 8.0.420

## 자동·패키지 검증

- Debug build: 경고 0, 오류 0
- Debug tests: 169 통과, 0 실패
- Release build: 경고 0, 오류 0
- Release tests: 169 통과, 0 실패
- `dotnet format --verify-no-changes`: PASS
- 알려진 NuGet 취약 패키지: 0건
- Portable 첫 실행·창 표시·두 번째 실행 단일 인스턴스·정상 종료: PASS
- 설치된 0.2.0 → 0.2.1 업그레이드·실행·제거·0.2.0 복구: PASS
- 사용자 데이터 fingerprint 보존: PASS
- PDB·DB·환경파일·세션·로그·서버 secret·개발 절대 경로: 0건

## 정식 배포물

- `HankiSetup-0.2.1.exe`: `CEB0AA3CB4BAD082D33A2D47F0D2EE34E87F6D1DE9A14CC8F91015671EDEB23E`
- `Hanki-0.2.1-win-x64.zip`: `298070C9FBC538BD8958D71BD4A1887A2D483D8781E0F6B4F8F9E056A7D33238`

## 현장 검증 경계

메모장·문제 프로그램·실제 PC방 보안 환경의 물리 입력 검증은 이번 자동화
결과에 포함하지 않는다. 일부 관리자 권한 프로그램, 커스텀 입력창, 게임
보안 모듈과 PC방 정책에서는 Windows 보안 경계에 따라 입력이 제한될 수 있다.
