# 한키 브랜드 자산

공식 원본은 `assets\branding\hanki-logo.svg`이며 `viewBox="0 0 1024 1024"`의
글꼴 비의존 벡터다. 파랑에서 청록으로 이어지는 둥근 사각형, 굵은 H, 우상향 화살표,
작은 반짝임을 핵심 식별 요소로 유지한다.

## 색과 여백

- 배경: 선명한 blue/cyan gradient
- H: 흰색에 가까운 밝은 cyan
- 화살표: 흰색, 소형 변형에서는 어두운 keyline 사용
- 외부 캔버스: 투명
- 검은 외곽 여백, 제품명 텍스트, 과도한 그림자 추가 금지
- 도형은 아이콘 가장자리와 충분한 내부 여백을 유지한다.

## 파생 자산

- 일반 PNG: 16, 20, 24, 32, 40, 48, 64, 128, 256, 512, 1024px
- Windows ICO: 16/20/24/32/40/48/64/128/256px
- 소형 원본: `hanki-logo-small.svg` (48px 이하에서 H·화살표 대비 강화)
- 설치 마법사: `hanki-installer-wizard.png`, `hanki-installer-small.png`
- 육안 검토: `hanki-logo-contact-sheet.png`

`node tools\render-logo-assets.mjs`와 `python tools\pack-ico.py`로 재생성한다.
SVG를 바꾼 뒤에는 contact sheet의 밝은/어두운 배경과 16–32px를 반드시 확인한다.

## 적용 위치

- Windows: PE `ApplicationIcon`, 트레이 ICO, WPF 창 PNG resource, 메인 헤더·계정,
  Inno Setup·제거 프로그램·시작 메뉴·선택적 바탕 화면 바로가기
- 웹: `public/images/hanki` 아래 256/512 PNG, 홈 제품 카드와 `/hanki`
- Yulbyte 전역 로고와 favicon은 변경하지 않는다.

## 0.2.0-rc.1 확인

RC Portable·설치본에는 공식 한키 로고가 PE 아이콘, WPF 리소스, 트레이와 Inno Setup
아이콘으로 반영된다. 설치본·시작 메뉴·제거 프로그램의 육안 확인은 수동 점검표에서
진행한다 — **자동화 불가**.
