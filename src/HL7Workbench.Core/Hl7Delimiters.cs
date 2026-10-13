namespace HL7Workbench.Core;

public sealed record Hl7Delimiters(char Field, char Component, char Repetition,
    char? Escape, char? Subcomponent, char? Truncation = null)
{
    public static Hl7Delimiters Default { get; } = new('|', '^', '~', '\\', '&');

    internal bool IsValid
    {
        get
        {
            var characters = new char?[] { Field, Component, Repetition, Escape, Subcomponent, Truncation }
                .Where(c => c.HasValue).Select(c => c!.Value).ToArray();
            return characters.Distinct().Count() == characters.Length && characters.All(c => c is >= '!' and <= '~') &&
                (Subcomponent is null || Escape is not null) && (Truncation is null || Subcomponent is not null);
        }
    }
}
