# 의존성 감사 — 0.2.0-dev

측정일: 2026-07-26

## NuGet

`dotnet list package --vulnerable --include-transitive`에서 최초
`SQLitePCLRaw.lib.e_sqlite3 2.1.6`의 High 취약점
`GHSA-2m69-gcr7-jv3q`가 확인됐다. 메이저 프레임워크 변경 없이 기존 transitive
bundle을 `SQLitePCLRaw.bundle_e_sqlite3 2.1.12`(Apache-2.0)로 직접 고정했다.
재복원 후 네 프로젝트 모두 알려진 취약 패키지 0건이다.

Outdated 결과에는 Microsoft.Data.Sqlite 10, ProtectedData 10, MSTest 4,
coverlet 10 등 메이저 업데이트가 있었으나 0.2.0-dev에서 일괄 업그레이드하지 않았다.

## npm

최초 `npm audit`은 12 High를 보고했다. 앱 빌드/runtime 경로의 transitive 의존성을
최소 override하여 다음을 적용했다.

- `postcss 8.5.23` (MIT)
- `sharp 0.35.3` (Apache-2.0)

lint·typecheck·test·production build 재검증 후 남은 항목은 9 High다. 모두 ESLint
개발 도구 체인의 `minimatch 3.1.5 → brace-expansion 1.1.16`(ISC/MIT)에서 발생한다.
안전한 1.x 패치가 없고 npm 제안은 ESLint 10 강제 메이저 업그레이드이므로 자동
`--force`는 실행하지 않았다. 이 경로는 배포 앱 runtime bundle이 아니라 개발 lint
glob 처리 경로지만, 후속 호환성 검증과 함께 갱신해야 한다.

`npm audit --omit=dev` 결과 production 의존성 취약점은 0건이다.

`npm outdated`의 React 19 patch, Node types 26, ESLint 10, TypeScript 7도 이번
작업에서 자동 변경하지 않았다.
