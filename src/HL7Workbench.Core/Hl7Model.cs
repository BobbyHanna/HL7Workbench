using System.Collections.ObjectModel;

namespace HL7Workbench.Core;

public abstract class Hl7Element
{
    public string Raw { get; }
    public Hl7Value Value => new(Raw);
    internal int Start { get; }
    internal Hl7Element(string raw, int start) { Raw = raw; Start = start; }
}

public sealed class Hl7Subcomponent : Hl7Element
{
    internal Hl7Subcomponent(string raw, int start) : base(raw, start) { }
}

public sealed class Hl7Component : Hl7Element
{
    public ReadOnlyCollection<Hl7Subcomponent> Subcomponents { get; }
    internal Hl7Component(string raw, int start, List<Hl7Subcomponent> children) : base(raw, start) => Subcomponents = children.AsReadOnly();
}

public sealed class Hl7Repetition : Hl7Element
{
    public ReadOnlyCollection<Hl7Component> Components { get; }
    internal Hl7Repetition(string raw, int start, List<Hl7Component> children) : base(raw, start) => Components = children.AsReadOnly();
}

public sealed class Hl7Field : Hl7Element
{
    public int Number { get; }
    public ReadOnlyCollection<Hl7Repetition> Repetitions { get; }
    internal Hl7Field(string raw, int start, int number, List<Hl7Repetition> children) : base(raw, start)
    { Number = number; Repetitions = children.AsReadOnly(); }
}

public sealed class Hl7Segment : Hl7Element
{
    public string Identifier { get; }
    public string Terminator { get; }
    public ReadOnlyCollection<Hl7Field> Fields { get; }
    public bool CanEdit { get; }
    internal Hl7Segment(string raw, int start, string identifier, string terminator,
        List<Hl7Field> fields, bool canEdit) : base(raw, start)
    { Identifier = identifier; Terminator = terminator; Fields = fields.AsReadOnly(); CanEdit = canEdit; }

    public Hl7Field? GetField(int number)
    {
        if (number < 1) throw new ArgumentOutOfRangeException(nameof(number));
        return number <= Fields.Count ? Fields[number - 1] : null;
    }
}
