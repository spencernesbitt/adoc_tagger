using System.Text;
namespace tagger.core;

public static class Templates
{
    public static IncludeTemplate GetIncludeTemplate (IncludeTemplateType templateType)
    {
        switch (templateType)
        {           
            case IncludeTemplateType.Sidebar:
                var replacementMarker = "// xrefs";
                var templateBuilder = new StringBuilder(".Tags");
                templateBuilder.AppendLine();
                templateBuilder.AppendLine("[sidebar]");
                templateBuilder.AppendLine(@"****");
                templateBuilder.AppendLine(replacementMarker);
                templateBuilder.AppendLine(@"****");
                return  new IncludeTemplate(templateBuilder.ToString(), replacementMarker);
            default:
                return new IncludeTemplate(string.Empty, string.Empty);

        }
    }
}