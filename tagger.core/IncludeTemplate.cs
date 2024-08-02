namespace tagger.core;

/// <summary>
/// Represents the template as a string, usually in adoc syntax along with the string that we intend to replace with our specific text
/// </summary>
/// <param name="template"></param>
/// <param name="insertMarker"></param>
public class IncludeTemplate(string template, string replaceMarker)
{
    private readonly string _template = template;
    private readonly string _replaceMarker = replaceMarker;

    public string Template { get {return _template;} }
    public string ReplaceMarker {get {return _replaceMarker;} }
}