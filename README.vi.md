# QS3D AutoCAD

**Ngôn ngữ:** [English](README.md) · [Tiếng Việt](README.vi.md) · [简体中文](README.zh-CN.md) · [한국어](README.ko.md) · [Русский](README.ru.md) · [Français](README.fr.md)

QS3D AutoCAD là host dành cho Autodesk AutoCAD của QS3D, phục vụ quy trình mô hình kết cấu và bóc tách khối lượng.

## Các thế hệ AutoCAD được hỗ trợ

- AutoCAD 2021–2024: dùng một payload host legacy .NET Framework 4.8, biên dịch với `AutoCAD.NET` 24.0.0 do Autodesk sở hữu và được nạp từ dòng bundle `R24.0-R24.3`
- AutoCAD 2025: payload host .NET 8, biên dịch với `AutoCAD.NET` 25.0.1 do Autodesk sở hữu
- AutoCAD 2026: dùng cùng họ payload target .NET 8; native acceptance ghi nhận CLR thực tế của host vì AutoCAD 2026.1.2+ chuyển host sang .NET 10
- AutoCAD 2027: payload host .NET 10, biên dịch với `AutoCAD.NET` 26.0.0 do Autodesk sở hữu
- mã phụ thuộc Autodesk được tách khỏi QS3D Core trung lập với host
- triển khai bằng AutoCAD `.bundle`
- pipeline release tạo cả bundle zip portable và bộ cài tự chứa `QS3D-AutoCAD-<version>-Setup.exe`

Payload legacy chỉ được build một lần với managed SDK của AutoCAD 2021 và tái sử dụng cho AutoCAD 2021, 2022, 2023 và 2024. Ma trận tương thích managed của Autodesk hỗ trợ rõ các SDK R24.x cũ trên các host R24.x mới hơn, vì vậy QS3D không nhân bản bốn binary net48 tương đương. Payload legacy được build và đóng gói độc lập với các payload .NET 8/.NET 10 hiện đại; việc bổ sung discovery cho 2022–2024 không làm hạ cấp binary của 2025–2027.

AutoCAD 2026 có thêm một ranh giới native-runtime: payload QS3D 2026 phát hành vẫn target .NET 8, trong khi AutoCAD 2026 thực tế có thể báo CLR major 8 hoặc 10 tùy mức cập nhật. Hosted CI chỉ xác thực tính tương thích của source/packaging. Nó không thể thay thế việc nạp đúng candidate vào cài đặt AutoCAD 2026 thật và ghi nhận CLR/native checks quan sát được.

## Quy trình mô hình hóa đã triển khai

Chạy `QS3D` để lazy-load plugin và mở workspace QS3D có thể dock. Host hiện tại triển khai:

- `QS3DINIT` — khởi tạo/đổi tên project QS3D lưu trong DWG
- `QS3DLEVEL` — mốc Level
- `QS3DGRID` — trục Grid
- `QS3DCOLUMN` — cột kết cấu 3D
- `QS3DBEAM` — dầm 3D định hướng theo mặt bằng
- `QS3DSLAB` — sàn 3D hình chữ nhật
- `QS3DWALL` — tường 3D định hướng theo mặt bằng
- `QS3DCURTAIN` — panel curtain dạng module
- `QS3DSECTION` — ký hiệu mặt cắt
- `QS3DBOQ` — tổng hợp khối lượng từ các entity được gắn tag QS3D
- `QS3DEDIT` — chỉnh thuộc tính QS3D và rebuild solid vật lý khi kích thước thay đổi
- `QS3DASSIGNLEVEL` — gán một phần tử kết cấu vào QS3D Level rồi di chuyển/rebuild đến cao độ đó
- `QS3DLEVELMOVE` — thay đổi cao độ Level và truyền dịch chuyển Z đến toàn bộ phần tử kết cấu đang bind với Level
- `QS3DBINDGRID` — gắn một hoặc hai tham chiếu Grid semantic vào phần tử kết cấu
- `QS3DGRIDSNAP` — rebuild đồng thời hình học và metadata của phần tử bind với Grid
- `QS3DREFERENCERENAME` — đổi tên tham chiếu Level/Grid nhưng giữ nguyên semantic ID và binding
- `QS3DLEVELSEQUENCE` — đánh thứ tự tên Level theo cao độ
- `QS3DGRIDSEQUENCE` — đánh lại thứ tự một họ Grid song song theo vị trí không gian
- `QS3DCLEARREFS` — xóa tham chiếu bố trí Level/Grid mà không di chuyển hình học
- `QS3DGRIDARRAY` — tạo một dãy Grid song song có tên với khoảng cách cố định
- `QS3DREFERENCEDELETE` — xóa Level/Grid không còn dùng và từ chối xóa nếu vẫn có phần tử phụ thuộc
- `QS3DREFERENCES` — liệt kê tham chiếu Level/Grid và số lượng phần tử phụ thuộc
- `QS3DCOLUMNJIG`, `QS3DBEAMJIG`, `QS3DSLABJIG`, `QS3DWALLJIG`, `QS3DCURTAINJIG` — preview live-solid tạm thời khi tạo hình, có phản hồi kích thước/hướng và chỉ lưu khi commit
- `QS3DRIBBON` — reconcile/tạo QS3D Ribbon thông qua các runtime UI type `Autodesk.Windows` đã được AutoCAD load
- `QS3DREFRESH` — refresh model browser
- `QS3DABOUT` — thông tin host/runtime

Workspace có thể dock gồm các tab Tools, Project và Levels & Grids. Project browser liệt kê entity do QS3D sở hữu, đồng bộ với AutoCAD pickfirst selection, hiển thị thuộc tính hình học, khối lượng và placement reference, đồng thời có thể mở chỉnh sửa an toàn. Các control trong palette có thể chuyển giữa tiếng Việt và tiếng Anh.

Hình học được tạo mang QS3D XData có kiểu. Danh tính/tên project được lưu trong DWG Named Objects Dictionary nên trạng thái QS3D đi cùng bản vẽ. Metadata hiện tại dùng schema `QS3D2` tương thích ngược cho tham chiếu Level/Grid và vẫn đọc các entity legacy `QS3D1`. Khi thuộc tính solid hoặc placement theo Level thay đổi, QS3D giữ nguyên semantic ID trong khi thay thế hoặc di chuyển hình học vật lý, giúp metadata BOQ không lệch khỏi mô hình nhìn thấy.

Phần JIG/Grid-manager đã hoàn chỉnh ở mức source nhưng vẫn phải qua native acceptance trên host thật. Hosted build không phải bằng chứng rằng cursor preview, hình ảnh Ribbon, undo/redo hoặc persistence hoạt động đúng trên mọi thế hệ AutoCAD được hỗ trợ.

### Ranh giới Ribbon

Ribbon bridge chủ động **không** compile trực tiếp với `AdWindows.dll` hoặc `Autodesk.Windows`. Hosted CI không thể thay thế hoặc mock dependency UI native này của AutoCAD. `QS3DRIBBON` resolve assembly/type UI của AutoCAD đã được load tại runtime, tạo một tab QS3D idempotent với các panel Model/References/Review, và fail mềm để palette/model command vẫn dùng được nếu Ribbon API không khả dụng.

Hosted compile thành công chỉ chứng minh source bridge vẫn an toàn với host. `ribbon_surface` và `ribbon_visual_qa` vẫn là native acceptance gate; ma trận legacy AutoCAD 2021–2024 có thể được qualify riêng, còn ma trận qualification production mặc định vẫn là AutoCAD 2025, 2026 và 2027 cho đến khi release policy được chủ động thay đổi.

## Build và phát hành

GitHub `CI` build và smoke-test Core trung lập với host, compile một payload legacy net48 cho AutoCAD 2021–2024, payload net8 cho AutoCAD 2025–2026 và payload net10 cho AutoCAD 2027 bằng các package do Autodesk sở hữu; đồng thời kiểm tra command/bundle architecture, đóng gói engineering release candidate và xác minh release provenance/checksum end-to-end. Autodesk assembly chỉ là dependency ở thời điểm compile và không được đưa vào payload release của QS3D.

CI cũng kiểm tra chính native-acceptance tooling và chứng minh rằng evidence tổng hợp có check ở trạng thái `pending` sẽ bị từ chối cho cả ma trận hiện đại mặc định lẫn ma trận legacy AutoCAD 2021–2024 riêng. Hosted CI không bao giờ tự tạo native PASS.

`./scripts/package.ps1 -Version <version>` tạo:

- `artifacts/QS3D-AutoCAD-<version>.zip`
- `artifacts/QS3D-AutoCAD-<version>-Setup.exe`
- `artifacts/RELEASE-PROVENANCE.json`
- `artifacts/SHA256SUMS.txt`

`RELEASE-PROVENANCE.json` ghi lại chính xác source commit, version, ba họ payload runtime, trạng thái ký số, kích thước artifact và SHA-256 hash. `./scripts/verify-artifacts.ps1 -Version <version>` kiểm tra độc lập contract này.

Setup executable nhúng bundle và cài vào thư mục Autodesk `ApplicationPlugins` dùng chung cho mọi user. Install/upgrade được staging và có rollback an toàn; Setup từ chối install, upgrade hoặc `--uninstall` khi AutoCAD đang chạy.

Việc publish tag dùng cơ chế fail-closed: SHA của tag phải nằm trên `main`, phải khớp chính xác repository variable `QS3D_NATIVE_ACCEPTED_SHA`, và phải cấu hình secret Authenticode PFX/password thật. Workflow ký các plugin assembly và Setup.exe, xác minh chữ ký/provenance rồi mới tạo GitHub prerelease. Packaging thủ công vẫn phù hợp để engineering validation nhưng không được mô tả là signed production release khi provenance báo `signed=false`.

Plugin hiện tại không gửi telemetry và không thực hiện production licensing call. Xem `docs/PRIVACY.md` để biết trạng thái privacy hiện tại và `docs/RELEASE-SECURITY.md` để biết các release/signing gate.

Source build xanh không đồng nghĩa với native runtime qualification. Bundle chính xác được tạo ra vẫn phải acceptance-test trong AutoCAD thật. Ma trận release chính thức mặc định vẫn là AutoCAD 2025/2026/2027. AutoCAD 2021/2022/2023/2024 dùng ma trận evidence legacy riêng và mỗi host được test phải có evidence từ host thật trước khi có thể gọi là native-qualified. Evidence của AutoCAD 2026 cũng phải ghi lại CLR cụ thể quan sát được sau khi QS3D load để không che giấu quá trình chuyển host sang .NET 10 từ AutoCAD 2026.1.2+. Xem `docs/NATIVE-ACCEPTANCE.md` để biết chính xác evidence workflow.

Xem thêm `docs/IMPLEMENTATION-PLAN.md` và `docs/BUILD.md` để biết kiến trúc, build và các native acceptance gate.
