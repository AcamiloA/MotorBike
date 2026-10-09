namespace UniversityParking.Mobile.Core;
public sealed class FilterState
{
    private readonly Dictionary<string,object?> defaults;
    public Dictionary<string,object?> DraftFilters {get;private set;}
    public IReadOnlyDictionary<string,object?> AppliedFilters {get;private set;}
    public int ActiveCount=>AppliedFilters.Count(x=>x.Key is not ("DateFrom" or "DateTo" or "DateFilterApplied" or "TargetUserId" or "TargetVehicleId") && IsActive(x.Key,x.Value))+
        (AppliedFilters.GetValueOrDefault("DateFilterApplied") is true?1:0);
    private bool IsActive(string key,object? value)
    {
        if(value is null||Equals(value,defaults.GetValueOrDefault(key)))return false;
        if(value is string text)return !string.IsNullOrWhiteSpace(text);
        if(value is TypeChoice choice)return !string.IsNullOrEmpty(choice.Code);
        var id=value.GetType().GetProperty("Id");return id is null||id.GetValue(value) is not null;
    }
    public bool CanClear=>ActiveCount>0;
    public FilterState(Dictionary<string,object?> defaults)
    {this.defaults=new(defaults);DraftFilters=new(defaults);AppliedFilters=new Dictionary<string,object?>(defaults);}
    public void Open()=>DraftFilters=new(AppliedFilters);
    public void Apply()=>AppliedFilters=new Dictionary<string,object?>(DraftFilters);
    public void Clear(){DraftFilters=new(defaults);AppliedFilters=new Dictionary<string,object?>(defaults);}
}
