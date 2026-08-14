using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;

namespace QS3D.AutoCAD.UI;

internal readonly record struct Qs3dPreviewAnnotation(string Text, double TextHeight);

internal sealed class Qs3dPointPreviewJig : DrawJig
{
    private readonly Point3d _basePoint;
    private readonly string _message;
    private readonly Func<Point3d, IEnumerable<Entity>> _previewFactory;
    private readonly Func<Point3d, Qs3dPreviewAnnotation?>? _annotationFactory;
    private readonly Func<Point3d, Point3d> _normalizePoint;
    private readonly bool _useBasePoint;
    private Point3d _currentPoint;
    private bool _hasSample;

    public Qs3dPointPreviewJig(
        Point3d basePoint,
        string message,
        Func<Point3d, IEnumerable<Entity>> previewFactory,
        Func<Point3d, Point3d>? normalizePoint = null,
        bool useBasePoint = true,
        Func<Point3d, Qs3dPreviewAnnotation?>? annotationFactory = null)
    {
        _basePoint = basePoint;
        _message = message;
        _previewFactory = previewFactory;
        _annotationFactory = annotationFactory;
        _normalizePoint = normalizePoint ?? (point => point);
        _useBasePoint = useBasePoint;
        _currentPoint = basePoint;
    }

    public Point3d Point => _currentPoint;

    public PromptResult Drag(Editor editor) => editor.Drag(this);

    protected override SamplerStatus Sampler(JigPrompts prompts)
    {
        var options = new JigPromptPointOptions
        {
            Message = _message
        };

        if (_useBasePoint)
        {
            options.BasePoint = _basePoint;
            options.UseBasePoint = true;
        }

        var result = prompts.AcquirePoint(options);
        if (result.Status == PromptStatus.Cancel)
        {
            return SamplerStatus.Cancel;
        }
        if (result.Status != PromptStatus.OK)
        {
            return SamplerStatus.NoChange;
        }

        var next = _normalizePoint(result.Value);
        if (_hasSample && next.DistanceTo(_currentPoint) <= Tolerance.Global.EqualPoint)
        {
            return SamplerStatus.NoChange;
        }

        _currentPoint = next;
        _hasSample = true;
        return SamplerStatus.OK;
    }

    protected override bool WorldDraw(WorldDraw draw)
    {
        if (!_hasSample || (_useBasePoint && _currentPoint.DistanceTo(_basePoint) <= Tolerance.Global.EqualPoint))
        {
            return true;
        }

        try
        {
            foreach (var entity in _previewFactory(_currentPoint))
            {
                using (entity)
                {
                    entity.WorldDraw(draw);
                }
            }

            DrawAnnotation(draw);
        }
        catch (ArgumentException)
        {
            // Degenerate cursor frames are expected while sampling. Nothing is persisted.
        }
        catch (Autodesk.AutoCAD.Runtime.Exception)
        {
            // The geometry/text engine may reject transient near-zero frames. Keep the jig alive.
        }

        return true;
    }

    private void DrawAnnotation(WorldDraw draw)
    {
        if (_annotationFactory?.Invoke(_currentPoint) is not Qs3dPreviewAnnotation annotation ||
            string.IsNullOrWhiteSpace(annotation.Text) ||
            !double.IsFinite(annotation.TextHeight) ||
            annotation.TextHeight <= 0)
        {
            return;
        }

        var offset = annotation.TextHeight * 1.75;
        using var text = new DBText
        {
            TextString = annotation.Text,
            Height = annotation.TextHeight,
            Position = _currentPoint + new Vector3d(offset, offset, offset * 0.25)
        };
        text.WorldDraw(draw);
    }
}
