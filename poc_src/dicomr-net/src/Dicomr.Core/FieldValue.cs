namespace Dicomr.Core;

public enum FieldSource
{
    Standard,
    EnhancedMacro,
    Private,
    Inferred,
    Derived,
}

public abstract record FieldScalar
{
    public sealed record Text(string TextValue) : FieldScalar;

    public sealed record Number(double NumberValue) : FieldScalar;

    public sealed record Integer(long IntegerValue) : FieldScalar;

    public sealed record TextList(IReadOnlyList<string> Items) : FieldScalar;

    public sealed record NumberList(IReadOnlyList<double> Items) : FieldScalar;
}

public sealed record FieldValue
{
    public required string Keyword { get; init; }

    public string? Tag { get; init; }

    public string? Vr { get; init; }

    public string? Unit { get; init; }

    public FieldSource Source { get; init; }

    public string? SourceDetail { get; init; }

    public required FieldScalar Value { get; init; }

    public bool Varying { get; init; }

    public double? AsNumber => Value switch
    {
        FieldScalar.Number number => number.NumberValue,
        FieldScalar.Integer integer => integer.IntegerValue,
        _ => null,
    };

    public IReadOnlyList<double>? AsNumbers => Value switch
    {
        FieldScalar.NumberList list => list.Items,
        FieldScalar.Number number => [number.NumberValue],
        FieldScalar.Integer integer => [integer.IntegerValue],
        _ => null,
    };

    public string? AsText => Value is FieldScalar.Text text ? text.TextValue : null;

    public IReadOnlyList<string>? AsTexts => Value switch
    {
        FieldScalar.TextList list => list.Items,
        FieldScalar.Text text => [text.TextValue],
        _ => null,
    };
}
