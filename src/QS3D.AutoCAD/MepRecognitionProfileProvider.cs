using System.Globalization;
using System.Text;
using System.Xml;
using QS3D.Platform.Parity;

namespace QS3D.AutoCAD;

internal static class MepRecognitionProfileProvider
{
    private static readonly object Gate = new();
    private static MepRecognitionProfile _current = MepRecognitionProfiles.CreateDefault();
    private static bool _isCustom;
    private static string? _lastError;

    static MepRecognitionProfileProvider() => Reload();

    internal static MepRecognitionProfile Current
    {
        get { lock (Gate) return _current; }
    }

    internal static string ProfilePath => MepRecognitionProfileStore.ProfilePath;

    internal static bool IsCustom
    {
        get { lock (Gate) return _isCustom; }
    }

    internal static string? LastError
    {
        get { lock (Gate) return _lastError; }
    }

    internal static bool Reload()
    {
        lock (Gate)
        {
            if (!MepRecognitionProfileStore.TryLoad(out var profile, out var exists, out var error))
            {
                _current = MepRecognitionProfiles.CreateDefault();
                _isCustom = false;
                _lastError = error;
                return false;
            }
            _current = profile;
            _isCustom = exists;
            _lastError = null;
            return true;
        }
    }

    internal static void Save(MepRecognitionProfile profile)
    {
        if (profile is null) throw new ArgumentNullException(nameof(profile));
        MepRecognitionProfileStore.SaveAtomic(profile);
        lock (Gate)
        {
            _current = profile;
            _isCustom = true;
            _lastError = null;
        }
    }

    internal static void SaveDefault() => Save(MepRecognitionProfiles.CreateDefault());
}

internal static class MepRecognitionProfileStore
{
    private const int MaxProfileBytes = 512 * 1024;
    private const int MaxRules = 500;
    private const int MaxTokensPerRule = 100;
    private const string RootName = "qs3dMepRecognitionProfile";
    private const string Version = "1";

    internal static string ProfilePath
    {
        get
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrWhiteSpace(root)) throw new InvalidOperationException("Windows application-data directory is unavailable.");
            return Path.Combine(root, "QS3D", "AutoCAD", "mep-recognition-profile.xml");
        }
    }

    internal static bool TryLoad(out MepRecognitionProfile profile, out bool exists, out string? error)
    {
        profile = MepRecognitionProfiles.CreateDefault();
        exists = false;
        error = null;
        string path;
        try { path = ProfilePath; }
        catch (Exception ex)
        {
            error = "Unable to resolve recognition profile path: " + ex.Message;
            return false;
        }
        if (!File.Exists(path)) return true;
        exists = true;

        try
        {
            var info = new FileInfo(path);
            if (info.Length <= 0 || info.Length > MaxProfileBytes)
                throw new InvalidDataException("Profile file must be non-empty and at most " + MaxProfileBytes + " bytes.");

            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = MaxProfileBytes,
                MaxCharactersFromEntities = 0
            };
            var document = new XmlDocument { XmlResolver = null };
            using (var reader = XmlReader.Create(path, settings)) document.Load(reader);
            var root = document.DocumentElement;
            if (root is null || !StringComparer.Ordinal.Equals(root.Name, RootName)) throw new InvalidDataException("Recognition profile root is invalid.");
            if (!StringComparer.Ordinal.Equals(root.GetAttribute("version"), Version)) throw new InvalidDataException("Recognition profile version is unsupported.");

            var rules = new List<MepRecognitionRule>();
            foreach (XmlNode node in root.ChildNodes)
            {
                if (node.NodeType is XmlNodeType.Comment or XmlNodeType.Whitespace) continue;
                if (node is not XmlElement element || !StringComparer.Ordinal.Equals(element.Name, "rule"))
                    throw new InvalidDataException("Recognition profile may contain only <rule> elements.");
                if (rules.Count >= MaxRules) throw new InvalidDataException("Recognition profile exceeds " + MaxRules + " rules.");
                rules.Add(ParseRule(element));
            }
            if (rules.Count == 0) throw new InvalidDataException("Recognition profile must contain at least one rule.");
            profile = new MepRecognitionProfile(rules);
            return true;
        }
        catch (Exception ex) when (IsRecoverable(ex))
        {
            profile = MepRecognitionProfiles.CreateDefault();
            error = "Invalid recognition profile; using built-in defaults: " + ex.Message;
            return false;
        }
    }

    internal static void SaveAtomic(MepRecognitionProfile profile)
    {
        if (profile is null) throw new ArgumentNullException(nameof(profile));
        if (profile.Rules.Count <= 0 || profile.Rules.Count > MaxRules)
            throw new InvalidOperationException("Recognition profile rule count must be within 1.." + MaxRules + ".");
        var path = ProfilePath;
        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory)) throw new InvalidOperationException("Recognition profile directory is invalid.");
        Directory.CreateDirectory(directory);
        var tempPath = path + ".tmp-" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        var backupPath = path + ".bak";
        try
        {
            WriteXml(profile, tempPath);
            var info = new FileInfo(tempPath);
            if (info.Length <= 0 || info.Length > MaxProfileBytes) throw new InvalidDataException("Serialized recognition profile exceeds the safe size bound.");
            if (File.Exists(path))
            {
                if (File.Exists(backupPath)) File.Delete(backupPath);
                File.Replace(tempPath, path, backupPath, true);
                if (File.Exists(backupPath)) File.Delete(backupPath);
            }
            else File.Move(tempPath, path);
        }
        finally
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); }
            catch (Exception ex) when (IsRecoverable(ex)) { }
        }
    }

    private static MepRecognitionRule ParseRule(XmlElement element)
    {
        var id = RequiredAttribute(element, "id");
        if (!int.TryParse(RequiredAttribute(element, "priority"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var priority))
            throw new InvalidDataException("Rule " + id + " priority is invalid.");
        if (!Enum.TryParse(RequiredAttribute(element, "discipline"), true, out MepDiscipline discipline) || !Enum.IsDefined(typeof(MepDiscipline), discipline))
            throw new InvalidDataException("Rule " + id + " discipline is invalid.");
        var category = RequiredAttribute(element, "category");
        if (!Enum.TryParse(RequiredAttribute(element, "source"), true, out MepRecognitionSource source) ||
            source == MepRecognitionSource.None || (source & ~MepRecognitionSource.LayerOrBlockName) != MepRecognitionSource.None)
            throw new InvalidDataException("Rule " + id + " recognition source is invalid.");

        MepElementKind? mepKind = null;
        var kindText = element.GetAttribute("mepKind");
        if (!string.IsNullOrWhiteSpace(kindText))
        {
            if (!Enum.TryParse(kindText, true, out MepElementKind parsedKind) || !Enum.IsDefined(typeof(MepElementKind), parsedKind))
                throw new InvalidDataException("Rule " + id + " MEP kind is invalid.");
            mepKind = parsedKind;
        }

        var tokens = new List<string>();
        foreach (XmlNode node in element.ChildNodes)
        {
            if (node.NodeType is XmlNodeType.Comment or XmlNodeType.Whitespace) continue;
            if (node is not XmlElement token || !StringComparer.Ordinal.Equals(token.Name, "token"))
                throw new InvalidDataException("Rule " + id + " may contain only <token> elements.");
            if (tokens.Count >= MaxTokensPerRule) throw new InvalidDataException("Rule " + id + " exceeds " + MaxTokensPerRule + " tokens.");
            tokens.Add(RequiredAttribute(token, "value"));
        }
        if (tokens.Count == 0) throw new InvalidDataException("Rule " + id + " must contain at least one token.");
        return new MepRecognitionRule(id, priority, discipline, category, tokens, source, mepKind);
    }

    private static void WriteXml(MepRecognitionProfile profile, string path)
    {
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = true,
            NewLineHandling = NewLineHandling.Entitize,
            CloseOutput = true
        };
        using var writer = XmlWriter.Create(path, settings);
        writer.WriteStartDocument();
        writer.WriteStartElement(RootName);
        writer.WriteAttributeString("version", Version);
        foreach (var rule in profile.Rules)
        {
            if (rule.Tokens.Count <= 0 || rule.Tokens.Count > MaxTokensPerRule)
                throw new InvalidOperationException("Rule " + rule.Id + " token count is invalid.");
            writer.WriteStartElement("rule");
            writer.WriteAttributeString("id", rule.Id);
            writer.WriteAttributeString("priority", rule.Priority.ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("discipline", rule.Discipline.ToString());
            writer.WriteAttributeString("category", rule.Category);
            writer.WriteAttributeString("source", rule.Source.ToString());
            if (rule.MepKind.HasValue) writer.WriteAttributeString("mepKind", rule.MepKind.Value.ToString());
            foreach (var token in rule.Tokens)
            {
                writer.WriteStartElement("token");
                writer.WriteAttributeString("value", token);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    private static string RequiredAttribute(XmlElement element, string name)
    {
        var value = element.GetAttribute(name);
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidDataException("Missing attribute " + name + ".");
        return value.Trim();
    }

    private static bool IsRecoverable(Exception exception) =>
        exception is not OutOfMemoryException && exception is not StackOverflowException && exception is not AccessViolationException;
}
