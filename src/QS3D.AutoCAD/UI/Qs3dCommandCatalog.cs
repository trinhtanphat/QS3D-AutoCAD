namespace QS3D.AutoCAD.UI;

internal sealed record Qs3dCommandDescriptor(
    string Command,
    string LabelKey,
    string Section,
    string Keywords,
    bool Primary = false);

internal static class Qs3dCommandCatalog
{
    public const string SectionProject = "project";
    public const string SectionModel = "model";
    public const string SectionReferences = "references";
    public const string SectionReview = "review";
    public const string SectionSystem = "system";

    public static IReadOnlyList<Qs3dCommandDescriptor> All { get; } =
    [
        new("QS3DINIT", "init", SectionProject, "project initialize bootstrap du an khoi tao", true),
        new("QS3DLEVEL", "level", SectionReferences, "level elevation cao do tang", true),
        new("QS3DGRID", "grid", SectionReferences, "grid axis truc luoi", true),
        new("QS3DCOLUMNJIG", "columnLive", SectionModel, "column cot live jig 3d", true),
        new("QS3DBEAMJIG", "beamLive", SectionModel, "beam dam live jig 3d", true),
        new("QS3DSLABJIG", "slabLive", SectionModel, "slab san live jig 3d", true),
        new("QS3DWALLJIG", "wallLive", SectionModel, "wall tuong live jig 3d", true),
        new("QS3DCURTAINJIG", "curtainLive", SectionModel, "curtain vach kinh live jig 3d", true),
        new("QS3DCOLUMN", "column", SectionModel, "column cot basic"),
        new("QS3DBEAM", "beam", SectionModel, "beam dam basic"),
        new("QS3DSLAB", "slab", SectionModel, "slab san basic"),
        new("QS3DWALL", "wall", SectionModel, "wall tuong basic"),
        new("QS3DCURTAIN", "curtain", SectionModel, "curtain vach kinh basic"),
        new("QS3DSECTION", "section", SectionModel, "section mat cat"),
        new("QS3DASSIGNLEVEL", "assignLevel", SectionReferences, "assign level gan cao do"),
        new("QS3DLEVELMOVE", "moveLevel", SectionReferences, "move level dependents doi cao do"),
        new("QS3DBINDGRID", "bindGrid", SectionReferences, "bind grid reference gan truc"),
        new("QS3DGRIDSNAP", "gridSnap", SectionReferences, "grid snap geometry truc"),
        new("QS3DREFERENCERENAME", "referenceRename", SectionReferences, "rename level grid doi ten"),
        new("QS3DLEVELSEQUENCE", "levelSequence", SectionReferences, "sequence level elevation danh so"),
        new("QS3DGRIDSEQUENCE", "gridSequence", SectionReferences, "sequence grid family danh so"),
        new("QS3DCLEARREFS", "clearRefs", SectionReferences, "clear references go tham chieu"),
        new("QS3DGRIDARRAY", "gridArray", SectionReferences, "grid array spacing day truc"),
        new("QS3DREFERENCEDELETE", "referenceDelete", SectionReferences, "safe delete level grid xoa"),
        new("QS3DREFERENCES", "referenceList", SectionReferences, "references dependencies phu thuoc"),
        new("QS3DEDIT", "edit", SectionReview, "edit properties sua thuoc tinh", true),
        new("QS3DBOQ", "boq", SectionReview, "boq quantity takeoff khoi luong", true),
        new("QS3DREFRESH", "refresh", SectionReview, "refresh browser workspace lam moi"),
        new("QS3DRIBBON", "ribbon", SectionSystem, "ribbon tab workspace"),
        new("QS3DABOUT", "about", SectionSystem, "about runtime clr version", true),
        new("QS3D", "workspace", SectionSystem, "open workspace palette qs3d")
    ];

    public static IEnumerable<Qs3dCommandDescriptor> InSection(string section) =>
        All.Where(item => string.Equals(item.Section, section, StringComparison.Ordinal));

    public static IEnumerable<Qs3dCommandDescriptor> Search(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<Qs3dCommandDescriptor>();
        }

        var normalized = query.Trim();
        return All.Where(item =>
            item.Command.IndexOf(normalized, StringComparison.OrdinalIgnoreCase) >= 0 ||
            UiText.Get(item.LabelKey).IndexOf(normalized, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
            item.Keywords.IndexOf(normalized, StringComparison.OrdinalIgnoreCase) >= 0);
    }
}

internal static class Qs3dCommandDispatcher
{
    public static void Execute(string command)
    {
        var document = Autodesk.AutoCAD.ApplicationServices.Core.Application.DocumentManager.MdiActiveDocument;
        document?.SendStringToExecute(command + " ", true, false, false);
    }
}