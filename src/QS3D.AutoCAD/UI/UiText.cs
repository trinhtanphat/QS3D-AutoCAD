namespace QS3D.AutoCAD.UI;

internal enum UiLanguage
{
    Vietnamese,
    English
}

internal static class UiText
{
    private static readonly IReadOnlyDictionary<string, string> Vietnamese = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["tools"] = "Công cụ",
        ["project"] = "Dự án",
        ["referencesTab"] = "Cao độ & Trục",
        ["init"] = "Khởi tạo dự án",
        ["level"] = "Cao độ",
        ["grid"] = "Trục",
        ["column"] = "Cột",
        ["beam"] = "Dầm",
        ["slab"] = "Sàn",
        ["wall"] = "Tường",
        ["curtain"] = "Vách kính",
        ["section"] = "Mặt cắt",
        ["boq"] = "Bóc khối lượng",
        ["about"] = "Giới thiệu",
        ["refresh"] = "Làm mới",
        ["select"] = "Chọn trong bản vẽ",
        ["edit"] = "Sửa thuộc tính",
        ["language"] = "English",
        ["kind"] = "Loại",
        ["name"] = "Tên",
        ["handle"] = "Handle",
        ["elements"] = "Đối tượng QS3D",
        ["assignLevel"] = "Gán vào cao độ",
        ["moveLevel"] = "Đổi cao độ + cập nhật phụ thuộc",
        ["bindGrid"] = "Gán tham chiếu trục",
        ["gridArray"] = "Tạo dãy trục theo khoảng cách",
        ["referenceDelete"] = "Xóa cao độ/trục an toàn",
        ["referenceList"] = "Liệt kê phụ thuộc"
    };

    private static readonly IReadOnlyDictionary<string, string> English = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["tools"] = "Tools",
        ["project"] = "Project",
        ["referencesTab"] = "Levels & Grids",
        ["init"] = "Initialize Project",
        ["level"] = "Level",
        ["grid"] = "Grid",
        ["column"] = "Column",
        ["beam"] = "Beam",
        ["slab"] = "Slab",
        ["wall"] = "Wall",
        ["curtain"] = "Curtain",
        ["section"] = "Section",
        ["boq"] = "Quantity Takeoff",
        ["about"] = "About",
        ["refresh"] = "Refresh",
        ["select"] = "Select in drawing",
        ["edit"] = "Edit properties",
        ["language"] = "Tiếng Việt",
        ["kind"] = "Kind",
        ["name"] = "Name",
        ["handle"] = "Handle",
        ["elements"] = "QS3D elements",
        ["assignLevel"] = "Assign to Level",
        ["moveLevel"] = "Move Level + dependents",
        ["bindGrid"] = "Bind Grid references",
        ["gridArray"] = "Create spaced Grid array",
        ["referenceDelete"] = "Delete Level/Grid safely",
        ["referenceList"] = "List dependencies"
    };

    public static event EventHandler? LanguageChanged;

    public static UiLanguage Language { get; private set; } = UiLanguage.Vietnamese;

    public static string Get(string key)
    {
        var source = Language == UiLanguage.Vietnamese ? Vietnamese : English;
        return source.TryGetValue(key, out var value) ? value : key;
    }

    public static void Toggle()
    {
        Language = Language == UiLanguage.Vietnamese ? UiLanguage.English : UiLanguage.Vietnamese;
        LanguageChanged?.Invoke(null, EventArgs.Empty);
    }
}
