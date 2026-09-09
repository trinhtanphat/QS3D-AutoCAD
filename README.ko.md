# QS3D AutoCAD

**언어:** [English](README.md) · [Tiếng Việt](README.vi.md) · [简体中文](README.zh-CN.md) · [한국어](README.ko.md) · [Русский](README.ru.md) · [Français](README.fr.md)

QS3D AutoCAD는 QS3D의 구조 모델링 및 수량 산출 워크플로를 Autodesk AutoCAD에서 실행하기 위한 호스트 플러그인입니다.

## 지원하는 AutoCAD 세대

- AutoCAD 2021–2024: Autodesk 소유의 `AutoCAD.NET` 24.0.0을 기준으로 컴파일되고 `R24.0-R24.3` bundle 계열에서 로드되는 단일 legacy .NET Framework 4.8 호스트 payload
- AutoCAD 2025: Autodesk 소유의 `AutoCAD.NET` 25.0.1을 기준으로 컴파일되는 .NET 8 호스트 payload
- AutoCAD 2026: 동일한 .NET 8 대상 payload 계열 사용; AutoCAD 2026.1.2+에서 호스트가 .NET 10으로 전환되므로 native acceptance가 실제 관측된 호스트 CLR을 기록
- AutoCAD 2027: Autodesk 소유의 `AutoCAD.NET` 26.0.0을 기준으로 컴파일되는 .NET 10 호스트 payload
- Autodesk 종속 코드는 호스트 중립적인 QS3D Core와 분리
- 배포는 AutoCAD `.bundle` 사용
- release pipeline은 portable bundle zip과 self-contained `QS3D-AutoCAD-<version>-Setup.exe`를 모두 생성

Legacy payload는 AutoCAD 2021 managed SDK를 기준으로 한 번만 빌드하며 AutoCAD 2021, 2022, 2023, 2024에서 재사용합니다. Autodesk의 managed 호환성 매트릭스는 이전 R24.x managed SDK를 더 최신 R24.x 호스트에서 사용하는 것을 명시적으로 지원하므로 QS3D는 동등한 net48 바이너리 네 개를 중복 생성하지 않습니다. Legacy payload는 최신 .NET 8/.NET 10 payload와 독립적으로 빌드/패키징되며, 2022–2024 검색 지원을 추가해도 2025–2027 바이너리의 대상 런타임이 낮아지지 않습니다.

AutoCAD 2026에는 추가 native-runtime 경계가 있습니다. 배포되는 QS3D 2026 payload는 계속 .NET 8을 대상으로 하지만, 실제 AutoCAD 2026 호스트는 업데이트 수준에 따라 CLR major 8 또는 10을 보고할 수 있습니다. Hosted CI는 source/packaging 호환성만 검증하며, 실제 AutoCAD 2026 설치에서 정확한 candidate를 로드하고 관측된 CLR/native check를 기록하는 과정을 대체할 수 없습니다.

## 구현된 모델링 워크플로

`QS3D`를 실행하면 플러그인이 lazy-load되고 dock 가능한 QS3D workspace가 열립니다. 현재 호스트에는 다음 기능이 구현되어 있습니다.

- `QS3DINIT` — DWG에 저장되는 QS3D 프로젝트 초기화/이름 변경
- `QS3DLEVEL` — Level 마커
- `QS3DGRID` — Grid 축
- `QS3DCOLUMN` — 3D 구조 기둥
- `QS3DBEAM` — 평면 방향 기반 3D 보
- `QS3DSLAB` — 직사각형 3D 슬래브
- `QS3DWALL` — 평면 방향 기반 3D 벽
- `QS3DCURTAIN` — 모듈형 커튼 패널
- `QS3DSECTION` — 단면 마커
- `QS3DBOQ` — QS3D 태그가 지정된 entity에서 수량 요약 생성
- `QS3DEDIT` — QS3D 속성을 편집하고 치수 변경 시 물리 solid를 재구성
- `QS3DASSIGNLEVEL` — 구조 요소를 QS3D Level에 바인딩하고 해당 고도로 이동/재구성
- `QS3DLEVELMOVE` — Level 고도를 변경하고 해당 Level에 바인딩된 모든 구조 요소에 Z 이동을 전파
- `QS3DBINDGRID` — 구조 요소에 하나 또는 두 개의 semantic Grid 참조를 연결
- `QS3DGRIDSNAP` — Grid에 바인딩된 구조 형상과 metadata를 함께 재구성
- `QS3DREFERENCERENAME` — semantic ID와 binding을 유지하면서 Level/Grid 참조 이름 변경
- `QS3DLEVELSEQUENCE` — 고도 기준으로 Level 이름 순서 지정
- `QS3DGRIDSEQUENCE` — 공간 순서에 따라 평행 Grid family 재정렬
- `QS3DCLEARREFS` — 형상을 이동하지 않고 Level/Grid placement 참조 제거
- `QS3DGRIDARRAY` — 고정 간격의 이름 있는 평행 Grid 시리즈 생성
- `QS3DREFERENCEDELETE` — 사용하지 않는 Level/Grid 삭제; dependent가 남아 있으면 삭제 거부
- `QS3DREFERENCES` — Level/Grid 참조 및 dependent 수 표시
- `QS3DCOLUMNJIG`, `QS3DBEAMJIG`, `QS3DSLABJIG`, `QS3DWALLJIG`, `QS3DCURTAINJIG` — 치수/방향 피드백을 제공하고 commit 시에만 영속화되는 임시 live-solid 작성 preview
- `QS3DRIBBON` — AutoCAD가 로드한 `Autodesk.Windows` runtime UI type을 통해 QS3D Ribbon 조정/생성
- `QS3DREFRESH` — model browser 새로고침
- `QS3DABOUT` — host/runtime 정보

Dock 가능한 workspace에는 Tools, Project, Levels & Grids 탭이 있습니다. Project browser는 QS3D 소유 entity를 나열하고 AutoCAD pickfirst selection과 동기화하며, geometry/quantity/placement-reference 속성을 표시하고 안전한 편집을 시작할 수 있습니다. Palette control은 베트남어와 영어 사이를 전환할 수 있습니다.

생성된 geometry에는 typed QS3D XData가 포함됩니다. Project identity/name은 DWG Named Objects Dictionary에 저장되므로 QS3D 상태가 도면과 함께 이동합니다. 현재 metadata는 Level/Grid 참조에 backward-compatible `QS3D2` schema를 사용하면서 legacy `QS3D1` entity도 계속 읽습니다. Solid 속성 또는 Level placement를 변경할 때 QS3D semantic ID는 유지하고 물리 geometry만 교체하거나 이동하므로 BOQ metadata가 화면에 보이는 모델과 불일치하는 것을 방지합니다.

JIG/Grid-manager 구현은 source 수준에서 완료되어 있지만 실제 호스트 native acceptance가 여전히 필요합니다. Hosted build는 cursor preview, Ribbon visual, undo/redo, persistence가 모든 지원 AutoCAD 세대에서 올바르게 동작한다는 증거가 아닙니다.

### Ribbon 경계

Ribbon bridge는 의도적으로 `AdWindows.dll` 또는 `Autodesk.Windows`에 직접 compile하지 않습니다. Hosted CI는 이 AutoCAD native UI dependency를 대체하거나 mock할 수 없습니다. `QS3DRIBBON`은 runtime에서 이미 로드된 AutoCAD UI assembly/type을 resolve하고 Model/References/Review panel이 포함된 idempotent QS3D tab을 만들며, Ribbon API를 사용할 수 없는 경우 soft-fail하여 palette/model command는 계속 사용할 수 있게 합니다.

Hosted compile 성공은 bridge source가 host-safe하게 유지된다는 것만 증명합니다. `ribbon_surface`와 `ribbon_visual_qa`는 계속 native acceptance gate이며, AutoCAD 2021–2024 legacy matrix는 별도로 qualification할 수 있습니다. 기본 production qualification matrix는 release policy가 의도적으로 변경되기 전까지 AutoCAD 2025, 2026, 2027입니다.

## 빌드 및 배포

GitHub `CI`는 호스트 중립 Core를 빌드하고 smoke-test하며, Autodesk 소유 package를 사용해 AutoCAD 2021–2024용 단일 legacy net48 payload, AutoCAD 2025–2026용 net8 payload, AutoCAD 2027용 net10 payload를 컴파일합니다. 또한 command/bundle architecture를 검증하고 engineering release candidate를 패키징하며 release provenance/checksum을 end-to-end로 검증합니다. Autodesk assembly는 compile-time dependency일 뿐이며 QS3D release payload에는 포함되지 않습니다.

CI는 native-acceptance tooling 자체도 검증하며, `pending` check가 포함된 synthetic evidence가 기본 modern matrix와 별도 AutoCAD 2021–2024 legacy matrix 모두에서 거부되는 것을 확인합니다. Hosted CI는 native PASS를 생성하지 않습니다.

`./scripts/package.ps1 -Version <version>`은 다음을 생성합니다.

- `artifacts/QS3D-AutoCAD-<version>.zip`
- `artifacts/QS3D-AutoCAD-<version>-Setup.exe`
- `artifacts/RELEASE-PROVENANCE.json`
- `artifacts/SHA256SUMS.txt`

`RELEASE-PROVENANCE.json`에는 정확한 source commit, version, 세 가지 runtime payload family, signing 상태, artifact 크기 및 SHA-256 hash가 기록됩니다. `./scripts/verify-artifacts.ps1 -Version <version>`은 이 contract를 독립적으로 검증합니다.

Setup executable은 bundle을 내장하고 모든 사용자가 사용하는 Autodesk `ApplicationPlugins` 디렉터리에 설치합니다. Install/upgrade는 staging되고 rollback-safe하며, AutoCAD가 실행 중이면 Setup은 install, upgrade 또는 `--uninstall`을 거부합니다.

Tag publication은 fail-closed 방식입니다. Tagged SHA는 반드시 `main`에 있어야 하고 repository variable `QS3D_NATIVE_ACCEPTED_SHA`와 정확히 일치해야 하며, 실제 Authenticode PFX/password secret이 구성되어야 합니다. Workflow는 plugin assembly와 Setup.exe를 서명하고 서명/provenance를 검증한 다음에만 GitHub prerelease를 생성합니다. 수동 packaging은 engineering validation에는 적합하지만 provenance가 `signed=false`라고 보고하는 경우 signed production release로 표현해서는 안 됩니다.

현재 플러그인은 telemetry를 전송하지 않으며 production licensing call도 수행하지 않습니다. 현재 privacy 정책은 `docs/PRIVACY.md`, release/signing gate는 `docs/RELEASE-SECURITY.md`를 참조하십시오.

Source build가 green이라고 해서 native runtime qualification이 완료된 것은 아닙니다. 생성된 정확한 bundle은 여전히 실제 AutoCAD에서 acceptance testing을 거쳐야 합니다. 기본 정식 release matrix는 AutoCAD 2025/2026/2027입니다. AutoCAD 2021/2022/2023/2024는 별도의 legacy evidence matrix를 사용하며, 각 테스트 host는 native-qualified라고 부르기 전에 실제 host evidence를 가져야 합니다. AutoCAD 2026 evidence는 QS3D 로드 후 실제 관측된 CLR도 기록해야 하므로 AutoCAD 2026.1.2+의 .NET 10 host 전환이 숨겨지지 않습니다. 정확한 evidence workflow는 `docs/NATIVE-ACCEPTANCE.md`를 참조하십시오.

Architecture, build 및 native acceptance gate는 `docs/IMPLEMENTATION-PLAN.md`와 `docs/BUILD.md`를 참조하십시오.
