# 0.2.0 출시 후 검증 기록

이 문서는 GitHub Release 및 운영 사이트 공개 후 실제 결과를 이어 기록하는 자리입니다.

현재 상태:

- GitHub Release: PASS — [v0.2.0](https://github.com/huiyulidev-gif/HankiWindows/releases/tag/v0.2.0)
- 공개 자산 재다운로드: PASS — 설치본·Portable·SHA256 manifest 해시 일치
- Yulbyte `published=true`: PASS — `yulbyte-site` main `5d8668c`에 실제 URL·해시 반영
- Vercel production: PASS — `https://www.yulbyte.com` 운영 배포에서 0.2.0 공개 페이지 확인
- 운영 URL 다운로드 링크 확인: PASS — `/hanki` 설치형·Portable·GitHub Release 링크 HTTP 200

GitHub 공개 단계와 Vercel 운영 반영은 로컬 최종 산출물 검증, HankiWindows push, 태그 push, GitHub 자산 재다운로드 검증, 운영 URL 확인까지 PASS로 완료했습니다.

## 공개 자산 검증

- Installer: `HankiSetup-0.2.0.exe`, SHA-256 `11056DF733CE961AA7633A2362FF1C4A50E1257FD64B26E42A7FC0617C74CD3E`
- Portable: `Hanki-0.2.0-win-x64.zip`, SHA-256 `07F4C99DE9BA846C5870FA3C586F5CCEA287275403CEB6E3293F61D909436917`
- 다운로드 파일을 임시 폴더에 다시 받아 크기·해시·제품 버전·압축 해제를 확인했으며 Defender 탐지 없음.
- GitHub Release assets는 3개(설치본, Portable, SHA256SUMS)이며 draft/prerelease가 아님.

## 사이트 공개 검증

- `yulbyte-site` 커밋 `5d8668c`를 `main`에 일반 push 완료.
- 소스의 `/hanki`는 버전 `0.2.0`, 설치형·Portable·GitHub Release 링크와 SHA-256을 표시하도록 갱신됨.
- 로컬 `lint`, `typecheck`, `npm test` 22/22, `build`, 내부 link check 모두 PASS.
- `https://www.yulbyte.com`, `/hanki`, `/updates`, `/guide`는 HTTP 200 응답. `/hanki` HTML에서 0.2.0, 설치형·Portable·GitHub Release 버튼과 실제 v0.2.0 URL을 확인함.
- GitHub Release 설치형·Portable URL은 각각 HTTP 200이며 SHA-256은 재다운로드 검증값과 일치함.
