namespace BranchScribe.Models;

public class FormSchema
{
    public string SchemaId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0";
    public List<FormField> Fields { get; set; } = [];
}

public class FormField
{
    public string FieldId { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Type { get; set; } = "text"; // text, number, date, boolean, select
    public bool Required { get; set; }
    public List<string> ValidationRules { get; set; } = [];
    public string PromptTemplate { get; set; } = string.Empty;
    public List<string> DependsOn { get; set; } = [];
    public List<string> Options { get; set; } = []; // for select type
}
