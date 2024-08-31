using System.Text;
using System.Text.RegularExpressions;

namespace tagger.core;
/// <summary>
/// 
/// </summary>
/// <param name="linkURL">The relative or absolute link to the file being referenced</param>
/// <param name="canonicalName">The alias to use for the link</param>
public class CrossReference
{    
    private readonly string linkUrl;
    private readonly Regex linkRegEx;
     
    public CrossReference(string linkUrl)
    {
        this.linkUrl = linkUrl.Replace('\\', '/');
        // Regular expression to detect a string ending in .adoc or .tags and pick out the filename
        this.linkRegEx = new Regex(@"^(.+\/|.+\\)?(.+?)(?=\.?adoc|tags)");
    }

    /// <summary>
    /// The name used as an alias for the Xref link
    /// </summary>
    public string CanonicalName
    {
        get 
        {
            //Console.WriteLine($"Getting Canonical Name from: {this.linkUrl}");
            var linkUrlMatch = this.linkRegEx.Match(this.linkUrl);
            if (!linkUrlMatch.Success)
            {
                //Console.WriteLine("Could not match to linkRegEx");
                return this.linkUrl;
            }
            var fileNameWithoutExtension = linkUrlMatch.Groups["2"].Value;
            //Console.WriteLine($"fileNameWithoutExtension: {fileNameWithoutExtension}");
            var canonicalName = this.GetCanonicalNameFromFileName(fileName: fileNameWithoutExtension);
            return canonicalName;
        }
    }

    /// <summary>
    /// The URL to the file being refrenced
    /// </summary>
    public string LinkUrl {get {return this.linkUrl;}}

    /// <summary>
    /// The text to use in a file to implement the cross-reference
    /// </summary>
    public override string ToString()
    {
        return $"xref:{LinkUrl}[{CanonicalName}]";
    }

    public override int GetHashCode()
    {
        return this.linkUrl.GetHashCode();
    }

    public override bool Equals(Object? o) {
        if (!(o is CrossReference)) return false;
        CrossReference cr = (CrossReference)o;
        return cr.LinkUrl.Equals(this.linkUrl);
    }

    private string GetCanonicalNameFromFileName(string fileName)
    {
        var nameParts = fileName.Split('_');
        var canonicalName = new StringBuilder();
        if (nameParts.Length > 0)
        {          
            bool firstWordAdded = false;  
            for (int i = 0; i < nameParts.Length; i++)
            {            
                var word = nameParts[i];
                //Skip first work if it is tag
                if (i == 0 && word.ToLower() == "tag")
                {
                    continue;
                }
                if (firstWordAdded)
                {
                    canonicalName.Append(" ");
                }                    

                var firstLetter = char.ToUpper(word[0]);                    

                canonicalName.Append(firstLetter);
                canonicalName.Append(word.Substring(1));
                firstWordAdded =  true;
            } 
            return canonicalName.ToString();               
        }

        return fileName;
    }
}