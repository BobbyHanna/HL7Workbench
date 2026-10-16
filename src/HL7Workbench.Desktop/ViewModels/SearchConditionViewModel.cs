using System.ComponentModel;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.ViewModels;

public sealed class SearchConditionViewModel(SearchCondition condition) : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public SearchCondition Snapshot => condition;
    public SearchTarget Target
    {
        get => condition.Target;
        set
        {
            if (value == condition.Target) return;
            var operation = SearchCondition.OperatorsFor(value).Contains(condition.Operator) ? condition.Operator : SearchCondition.OperatorsFor(value).FirstOrDefault();
            var path = condition.Path;
            if (value == SearchTarget.Segment) path = path.Length >= 3 ? path[..3] : "PID";
            else if (value == SearchTarget.Field && path.Length == 3 && new SearchCondition(SearchTarget.Segment, SearchOperator.Exists, path).ValidationError() is null) path += "-1";
            else if (value == SearchTarget.Field && path.Length == 0) path = "PID-3.1";
            condition = condition with { Target = value, Operator = operation, Path = path }; Changed();
        }
    }
    public SearchOperator Operator { get => condition.Operator; set { if (condition.Operator == value) return; condition = condition with { Operator = value }; Changed(); } }
    public string Path { get => condition.Path; set { if (condition.Path == value) return; condition = condition with { Path = value }; Changed(); } }
    public string Value { get => condition.Value; set { if (condition.Value == value) return; condition = condition with { Value = value }; Changed(); } }
    public bool CaseSensitive { get => condition.CaseSensitive; set { if (condition.CaseSensitive == value) return; condition = condition with { CaseSensitive = value }; Changed(); } }
    private void Changed() => PropertyChanged?.Invoke(this, new(null));
}
