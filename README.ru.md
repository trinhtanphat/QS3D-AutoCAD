# QS3D AutoCAD

**Языки:** [English](README.md) · [Tiếng Việt](README.vi.md) · [简体中文](README.zh-CN.md) · [한국어](README.ko.md) · [Русский](README.ru.md) · [Français](README.fr.md)

QS3D AutoCAD — это хост-плагин QS3D для Autodesk AutoCAD, предназначенный для структурного моделирования и работы с объемами.

## Поддерживаемые поколения AutoCAD

- AutoCAD 2021–2024: единый legacy host payload на .NET Framework 4.8, скомпилированный против принадлежащего Autodesk `AutoCAD.NET` 24.0.0 и загружаемый из семейства bundle `R24.0-R24.3`
- AutoCAD 2025: host payload на .NET 8, скомпилированный против принадлежащего Autodesk `AutoCAD.NET` 25.0.1
- AutoCAD 2026: то же семейство payload с target .NET 8; native acceptance фиксирует реально наблюдаемый CLR хоста, поскольку AutoCAD 2026.1.2+ переводит хост на .NET 10
- AutoCAD 2027: host payload на .NET 10, скомпилированный против принадлежащего Autodesk `AutoCAD.NET` 26.0.0
- код, зависящий от Autodesk, изолирован от нейтрального к хосту QS3D Core
- развертывание выполняется через AutoCAD `.bundle`
- release pipeline создает и переносимый bundle zip, и автономный установщик `QS3D-AutoCAD-<version>-Setup.exe`

Legacy payload собирается один раз с managed SDK AutoCAD 2021 и повторно используется для AutoCAD 2021, 2022, 2023 и 2024. Матрица managed-совместимости Autodesk явно поддерживает более старые SDK R24.x на более новых хостах R24.x, поэтому QS3D не дублирует четыре эквивалентных net48 binary. Legacy payload собирается и упаковывается независимо от современных payload .NET 8/.NET 10; добавление discovery для 2022–2024 не понижает целевые версии binary 2025–2027.

Для AutoCAD 2026 действует дополнительная граница native runtime: поставляемый payload QS3D 2026 по-прежнему нацелен на .NET 8, тогда как реальный хост AutoCAD 2026 в зависимости от уровня обновления может сообщать CLR major 8 или 10. Hosted CI проверяет только совместимость исходного кода и упаковки. Он не заменяет загрузку точного candidate в реальную установку AutoCAD 2026 и фиксацию наблюдаемого CLR/native checks.

## Реализованный процесс моделирования

Выполните `QS3D`, чтобы lazy-load плагин и открыть закрепляемое рабочее пространство QS3D. Текущий хост реализует:

- `QS3DINIT` — инициализация/переименование проекта QS3D, хранящегося в DWG
- `QS3DLEVEL` — маркер Level
- `QS3DGRID` — ось Grid
- `QS3DCOLUMN` — 3D структурная колонна
- `QS3DBEAM` — 3D балка, ориентированная по плану
- `QS3DSLAB` — прямоугольная 3D плита
- `QS3DWALL` — 3D стена, ориентированная по плану
- `QS3DCURTAIN` — модульные curtain-панели
- `QS3DSECTION` — маркер сечения
- `QS3DBOQ` — сводка объемов по entity, помеченным QS3D
- `QS3DEDIT` — редактирование свойств QS3D с перестроением физических solid при изменении размеров
- `QS3DASSIGNLEVEL` — привязка структурного элемента к QS3D Level и перемещение/перестроение на эту отметку
- `QS3DLEVELMOVE` — изменение отметки Level с распространением смещения Z на все структурные элементы, привязанные к Level
- `QS3DBINDGRID` — привязка одного или двух semantic Grid references к структурному элементу
- `QS3DGRIDSNAP` — совместное перестроение геометрии и metadata структурного элемента, привязанного к Grid
- `QS3DREFERENCERENAME` — переименование ссылок Level/Grid с сохранением semantic ID и binding
- `QS3DLEVELSEQUENCE` — упорядочивание имен Level по высоте
- `QS3DGRIDSEQUENCE` — переупорядочивание семейства параллельных Grid по пространственному порядку
- `QS3DCLEARREFS` — удаление placement references Level/Grid без перемещения геометрии
- `QS3DGRIDARRAY` — создание именованной серии параллельных Grid с фиксированным шагом
- `QS3DREFERENCEDELETE` — удаление неиспользуемого Level/Grid с отказом при наличии зависимых элементов
- `QS3DREFERENCES` — список ссылок Level/Grid и количества зависимых элементов
- `QS3DCOLUMNJIG`, `QS3DBEAMJIG`, `QS3DSLABJIG`, `QS3DWALLJIG`, `QS3DCURTAINJIG` — временный live-solid preview при создании с обратной связью по размерам/ориентации и сохранением только при commit
- `QS3DRIBBON` — согласование/создание QS3D Ribbon через runtime UI types `Autodesk.Windows`, уже загруженные AutoCAD
- `QS3DREFRESH` — обновление model browser
- `QS3DABOUT` — информация о host/runtime

Закрепляемое рабочее пространство содержит вкладки Tools, Project и Levels & Grids. Project browser показывает entity, принадлежащие QS3D, синхронизируется с AutoCAD pickfirst selection, отображает свойства геометрии, объемов и placement references и позволяет запускать безопасное редактирование. Элементы palette могут переключаться между вьетнамским и английским языками.

Созданная геометрия содержит типизированные QS3D XData. Идентификатор/имя проекта хранится в DWG Named Objects Dictionary, поэтому состояние QS3D сохраняется вместе с чертежом. Текущая metadata использует обратно совместимую schema `QS3D2` для ссылок Level/Grid и продолжает читать legacy entity `QS3D1`. При изменении свойств solid или размещения по Level QS3D сохраняет semantic ID, одновременно заменяя или перемещая физическую геометрию, чтобы BOQ metadata не расходилась с видимой моделью.

Реализация JIG/Grid-manager завершена на уровне исходного кода, но по-прежнему требует native acceptance на реальном хосте. Hosted build не является доказательством корректной работы cursor preview, визуального Ribbon, undo/redo или persistence во всех поддерживаемых поколениях AutoCAD.

### Граница Ribbon

Ribbon bridge намеренно **не** компилируется напрямую против `AdWindows.dll` или `Autodesk.Windows`. Hosted CI не может заменить или смоделировать эту native UI dependency AutoCAD. `QS3DRIBBON` во время runtime разрешает уже загруженные AutoCAD UI assembly/types, создает идемпотентную вкладку QS3D с панелями Model/References/Review и мягко отказывает, чтобы palette/model commands оставались доступными, если Ribbon API недоступен.

Успешная hosted-компиляция доказывает только то, что source bridge остается безопасным для host. `ribbon_surface` и `ribbon_visual_qa` остаются native acceptance gates; legacy matrix AutoCAD 2021–2024 можно квалифицировать отдельно, а стандартная production qualification matrix остается AutoCAD 2025, 2026 и 2027 до намеренного изменения release policy.

## Сборка и поставка

GitHub `CI` собирает и smoke-test нейтральный к host Core, компилирует единый legacy net48 payload для AutoCAD 2021–2024, net8 payload для AutoCAD 2025–2026 и net10 payload для AutoCAD 2027 с использованием принадлежащих Autodesk package; проверяет command/bundle architecture, упаковывает engineering release candidate и сквозным образом проверяет release provenance/checksums. Autodesk assemblies используются только как compile-time dependencies и не входят в release payload QS3D.

CI также проверяет сам native-acceptance tooling и подтверждает, что synthetic evidence с check в состоянии `pending` отклоняется как для стандартной современной matrix, так и для отдельной legacy matrix AutoCAD 2021–2024. Hosted CI никогда не создает native PASS.

`./scripts/package.ps1 -Version <version>` создает:

- `artifacts/QS3D-AutoCAD-<version>.zip`
- `artifacts/QS3D-AutoCAD-<version>-Setup.exe`
- `artifacts/RELEASE-PROVENANCE.json`
- `artifacts/SHA256SUMS.txt`

`RELEASE-PROVENANCE.json` фиксирует точный source commit, version, три семейства runtime payload, состояние подписи, размеры artifact и SHA-256 hash. `./scripts/verify-artifacts.ps1 -Version <version>` независимо проверяет этот contract.

Setup executable содержит bundle и устанавливает его в общий для всех пользователей каталог Autodesk `ApplicationPlugins`. Install/upgrade выполняется через staging и безопасен для rollback; Setup отказывается выполнять install, upgrade или `--uninstall`, пока AutoCAD запущен.

Публикация tag работает по принципу fail-closed: tagged SHA должен находиться в `main`, точно совпадать с repository variable `QS3D_NATIVE_ACCEPTED_SHA`, а реальные secrets Authenticode PFX/password должны быть настроены. Workflow подписывает plugin assemblies и Setup.exe, проверяет подписи/provenance и только затем создает GitHub prerelease. Ручная packaging подходит для engineering validation, но не должна представляться как подписанный production release, если provenance сообщает `signed=false`.

Текущий плагин не отправляет telemetry и не выполняет production licensing calls. Текущая политика privacy описана в `docs/PRIVACY.md`, а release/signing gates — в `docs/RELEASE-SECURITY.md`.

Зеленая source build не означает native runtime qualification. Точный созданный bundle все еще требует acceptance testing в реальном AutoCAD. Стандартная официальная release matrix остается AutoCAD 2025/2026/2027. AutoCAD 2021/2022/2023/2024 используют отдельную legacy evidence matrix, и каждый тестируемый host должен иметь evidence с реального host, прежде чем его можно назвать native-qualified. Evidence для AutoCAD 2026 также должна фиксировать конкретный CLR, наблюдаемый после загрузки QS3D, чтобы переход AutoCAD 2026.1.2+ к хосту .NET 10 не был скрыт. Точный evidence workflow описан в `docs/NATIVE-ACCEPTANCE.md`.

Архитектуру, процесс сборки и native acceptance gates см. в `docs/IMPLEMENTATION-PLAN.md` и `docs/BUILD.md`.
