using HL7Workbench.Core;

namespace HL7Workbench.Workspace;

public sealed partial class MessageSearch
{
    private sealed record Criterion(SearchCondition Condition, Hl7Path? Path, MessageSearchQuery Literal);
    private readonly record struct Evaluation(bool Matches, long Count, long First, string Location);
    private static Evaluation Evaluate(IHl7TextSource source, Func<Hl7Structure> structure, Criterion criterion, bool fullCount, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var condition = criterion.Condition; var operation = condition.Operator;
        if (condition.Target == SearchTarget.MessageText)
        {
            var hits = Count(source, new(0, source.Length), criterion.Literal, token, !fullCount);
            var matched = operation == SearchOperator.DoesNotContain ? hits.Count == 0 : hits.Count > 0;
            return new(matched, fullCount ? hits.Count : 1, hits.First, "Text");
        }
        var scanned = structure();
        if (condition.Target == SearchTarget.Segment)
        {
            // Core retains opaque lines with a three-character preview identifier.
            // Count only segments whose structure Core actually exposed (or bare IDs).
            var present = scanned.Segments.Any(s => s.Identifier == condition.Path && (s.Fields.Count > 0 || s.Range.Length == 3));
            return new(operation == SearchOperator.Exists ? present : !present, 1, -1, condition.Path);
        }
        var path = criterion.Path!; var occurrence = 0; var exists = false; long count = 0; var location = condition.Path;
        var negative = operation is SearchOperator.DoesNotContain or SearchOperator.DoesNotEqual;
        foreach (var segment in scanned.Segments.Where(s => s.Identifier == path.Segment))
        {
            token.ThrowIfCancellationRequested(); occurrence++;
            var field = segment.Fields.FirstOrDefault(f => f.Number == path.Field);
            if (field is null) continue;
            foreach (var repetition in field.Children)
            {
                token.ThrowIfCancellationRequested();
                var node = path.Component is { } component ? repetition.Children.FirstOrDefault(c => c.Number == component) : repetition;
                if (path.Subcomponent is { } subcomponent) node = node?.Children.FirstOrDefault(c => c.Number == subcomponent);
                if (node is null) continue;
                exists = true;
                if (operation is SearchOperator.Exists or SearchOperator.DoesNotExist)
                    return new(operation == SearchOperator.Exists, 1, -1, condition.Path);
                var hits = operation is SearchOperator.Equals or SearchOperator.DoesNotEqual ?
                    (node.Range.Length == criterion.Literal.Value.Length && string.Equals(source.Read(node.Range), criterion.Literal.Value, Comparison(criterion.Literal)) ? 1L : 0L) :
                    Count(source, node.Range, criterion.Literal, token, !fullCount).Count;
                if (hits == 0) continue;
                if (count == 0) location = new Hl7Path(path.Segment, path.Field, occurrence, repetition.Number, path.Component, path.Subcomponent).ToString();
                count += hits;
                // A single matching repetition decides positive ANY-value comparisons
                // and disproves negatives. Missing values are handled separately below.
                if (!fullCount) return new(!negative, 1, -1, location);
            }
        }
        var matchedField = operation switch
        {
            SearchOperator.DoesNotExist => !exists,
            SearchOperator.Exists => exists,
            SearchOperator.DoesNotContain or SearchOperator.DoesNotEqual => exists && count == 0,
            _ => count > 0
        };
        return new(matchedField, fullCount ? count : 1, -1, location);
    }
}
