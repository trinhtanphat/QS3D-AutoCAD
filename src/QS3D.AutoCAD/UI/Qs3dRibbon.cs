using System.Collections;
using System.Reflection;
using System.Windows.Input;
using AcApplication = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace QS3D.AutoCAD.UI;

internal static class Qs3dRibbon
{
    private const string TabId = "QS3D_AUTOCAD_TAB";
    private const string TabTitle = "QS3D";

    private static readonly RibbonPanelDefinition[] Panels =
    [
        new("Model", [
            new("Project", "QS3DINIT"),
            new("Level", "QS3DLEVEL"),
            new("Grid", "QS3DGRID"),
            new("Column", "QS3DCOLUMN"),
            new("Beam", "QS3DBEAM"),
            new("Slab", "QS3DSLAB"),
            new("Wall", "QS3DWALL"),
            new("Curtain", "QS3DCURTAIN"),
            new("Section", "QS3DSECTION")
        ]),
        new("References", [
            new("Assign Level", "QS3DASSIGNLEVEL"),
            new("Move Level", "QS3DLEVELMOVE"),
            new("Bind Grid", "QS3DBINDGRID"),
            new("Clear Refs", "QS3DCLEARREFS"),
            new("Grid Array", "QS3DGRIDARRAY"),
            new("References", "QS3DREFERENCES")
        ]),
        new("Review", [
            new("Workspace", "QS3D"),
            new("Edit", "QS3DEDIT"),
            new("Refresh", "QS3DREFRESH"),
            new("BOQ", "QS3DBOQ"),
            new("About", "QS3DABOUT")
        ])
    ];

    public static bool TryEnsure(out string message)
    {
        try
        {
            var componentManagerType = ResolveAutodeskWindowsType("Autodesk.Windows.ComponentManager")
                ?? throw new InvalidOperationException("Autodesk.Windows.ComponentManager is unavailable. AdWindows.dll must be loaded by AutoCAD.");
            var ribbon = componentManagerType
                .GetProperty("Ribbon", BindingFlags.Public | BindingFlags.Static)
                ?.GetValue(null)
                ?? throw new InvalidOperationException("AutoCAD RibbonControl is not available in the current workspace.");

            var tabs = GetRequiredProperty(ribbon, "Tabs");
            var existing = FindExistingTab(tabs);
            if (existing is not null)
            {
                SetPropertyIfWritable(existing, "IsActive", true);
                message = "QS3D Ribbon is ready.";
                return true;
            }

            var uiAssembly = ribbon.GetType().Assembly;
            var tabType = GetRequiredType(uiAssembly, "Autodesk.Windows.RibbonTab");
            var panelType = GetRequiredType(uiAssembly, "Autodesk.Windows.RibbonPanel");
            var panelSourceType = GetRequiredType(uiAssembly, "Autodesk.Windows.RibbonPanelSource");
            var rowType = GetRequiredType(uiAssembly, "Autodesk.Windows.RibbonRow");
            var buttonType = GetRequiredType(uiAssembly, "Autodesk.Windows.RibbonButton");

            var tab = Create(tabType);
            SetRequiredProperty(tab, "Title", TabTitle);
            SetPropertyIfWritable(tab, "Id", TabId);

            var tabPanels = GetRequiredProperty(tab, "Panels");
            foreach (var panelDefinition in Panels)
            {
                var panelSource = Create(panelSourceType);
                SetRequiredProperty(panelSource, "Title", panelDefinition.Title);
                var rows = GetRequiredProperty(panelSource, "Rows");

                foreach (var buttonDefinition in panelDefinition.Buttons)
                {
                    var row = Create(rowType);
                    var rowItems = GetRequiredProperty(row, "RowItems");
                    var button = Create(buttonType);
                    SetRequiredProperty(button, "Text", buttonDefinition.Label);
                    SetPropertyIfWritable(button, "ShowText", true);
                    SetPropertyIfWritable(button, "ToolTip", $"Run {buttonDefinition.Command}");
                    SetRequiredProperty(button, "CommandHandler", new RibbonCommand(buttonDefinition.Command));
                    AddToCollection(rowItems, button);
                    AddToCollection(rows, row);
                }

                var panel = Create(panelType);
                SetRequiredProperty(panel, "Source", panelSource);
                AddToCollection(tabPanels, panel);
            }

            AddToCollection(tabs, tab);
            SetPropertyIfWritable(tab, "IsActive", true);
            message = "QS3D Ribbon created from the AutoCAD runtime UI assembly.";
            return true;
        }
        catch (Exception exception) when (exception is not StackOverflowException and not OutOfMemoryException)
        {
            message = $"QS3D Ribbon unavailable: {exception.Message}";
            return false;
        }
    }

    private static Type? ResolveAutodeskWindowsType(string fullName)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = assembly.GetType(fullName, throwOnError: false, ignoreCase: false);
            if (type is not null)
            {
                return type;
            }
        }

        try
        {
            return Assembly.Load("AdWindows").GetType(fullName, throwOnError: false, ignoreCase: false);
        }
        catch
        {
            return null;
        }
    }

    private static Type GetRequiredType(Assembly assembly, string fullName) =>
        assembly.GetType(fullName, throwOnError: false, ignoreCase: false)
        ?? throw new InvalidOperationException($"AutoCAD UI type is missing: {fullName}.");

    private static object Create(Type type) =>
        Activator.CreateInstance(type)
        ?? throw new InvalidOperationException($"Could not create AutoCAD UI type {type.FullName}.");

    private static object GetRequiredProperty(object target, string name)
    {
        var property = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)
            ?? throw new MissingMemberException(target.GetType().FullName, name);
        return property.GetValue(target)
            ?? throw new InvalidOperationException($"{target.GetType().FullName}.{name} returned null.");
    }

    private static void SetRequiredProperty(object target, string name, object value)
    {
        var property = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        if (property is null || !property.CanWrite)
        {
            throw new MissingMemberException(target.GetType().FullName, name);
        }
        property.SetValue(target, value);
    }

    private static void SetPropertyIfWritable(object target, string name, object value)
    {
        var property = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        if (property?.CanWrite == true)
        {
            property.SetValue(target, value);
        }
    }

    private static void AddToCollection(object collection, object item)
    {
        var addMethod = collection.GetType()
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.Name == "Add")
            .Where(method => method.GetParameters().Length == 1)
            .FirstOrDefault(method => method.GetParameters()[0].ParameterType.IsAssignableFrom(item.GetType()));
        if (addMethod is null)
        {
            throw new MissingMethodException(collection.GetType().FullName, "Add");
        }
        addMethod.Invoke(collection, [item]);
    }

    private static object? FindExistingTab(object tabs)
    {
        if (tabs is not IEnumerable enumerable)
        {
            return null;
        }

        foreach (var tab in enumerable)
        {
            if (tab is null)
            {
                continue;
            }
            var type = tab.GetType();
            var id = type.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance)?.GetValue(tab) as string;
            var title = type.GetProperty("Title", BindingFlags.Public | BindingFlags.Instance)?.GetValue(tab) as string;
            if (string.Equals(id, TabId, StringComparison.Ordinal) || string.Equals(title, TabTitle, StringComparison.Ordinal))
            {
                return tab;
            }
        }

        return null;
    }

    private sealed record RibbonPanelDefinition(string Title, IReadOnlyList<RibbonButtonDefinition> Buttons);
    private sealed record RibbonButtonDefinition(string Label, string Command);

    private sealed class RibbonCommand(string command) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
            var document = AcApplication.DocumentManager.MdiActiveDocument;
            document?.SendStringToExecute(command + " ", true, false, false);
        }
    }
}
