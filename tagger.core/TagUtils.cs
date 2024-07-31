using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace tagger.core;

public class TagUtils
{
    private readonly IFileSystem fileSystem;
    private readonly Regex tagRegEx;
    private readonly Regex xrefRegEx;
    
    /// <summary>
    /// Constructor to supclport unit testing with mock FileSystem 
    /// </summary>
    /// <param name="fileSystem"></param>
    public TagUtils(IFileSystem fileSystem)
    {
        this.fileSystem = fileSystem;
        //match any name that starts with "tag_" and ends with ".adoc" with some words in-between
        this.tagRegEx = new Regex(@"^(tag_)(\\w+)(\\.adoc)$"); 
        //match xrefs of the form:
        /*
            * xref:Tags/tag_methodic.adoc[#Methodic#]
            * xref:../data_mesh_event_streaming.adoc[Data Mesh Event Streaming]
            * xref:some_file.adoc[Name]
            xref:Some_file.adoc[name]
        */
        this.xrefRegEx = new Regex(@"^\*? ?xref:(.+? ?=\/)?\/?(.+?)(?=\[)(\[.*\])");
        // match adoc files such as:
        /*        
            Tags/tag_methodic.adoc
            ../data_mesh_event_streaming.adoc
            some_file.adoc
            Some_file.adoc
        */
        this.adocFileRegEx = new Regex(@"^(.+\/)?(.+?)(?=\.adoc)");
        // match tags files such as:
        /*        
            Tags/tag_methodic.tags
            ../data_mesh_event_streaming.tags
            some_file.tags
            Some_file.tags
        */
        this.tagsFileRegEx = new Regex(@"^(.+\/)?(.+?)(?=\.tags)");
    }

    //uses System.IO.Abstractions default implementaion that calls System.IO.FileSystem under the hood
    public TagUtils():this(fileSystem: new FileSystem())
    {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="filePath" example="some_interesting_note.adoc">Relative or Absolute Path to file being tagged</param>
    /// <param name="tag" example="Special Interest">the tag to apply</param>
    /// <returns></returns>
    public async Task<string> TagNoteFileAsync(string noteFilePath, string tag)
    {
        // Xref the tag index file to the tags file for the file being tagged (Links back to all files that reference the same tag)
        var tagsFilePath = this.GetTagsIncludeFilePath(noteFilePath: filePath); // ./some_interesting_note.tags
        var tagIndexFilePath = this.GetTagsIndexFilePath(tag: tag);             // ./Tags/some_tag.adoc
        await UpdateTagsFileWithTag(tagsFilePath: tagsFilePath, tag: tag);
        // Import the tags file into the file being tagged (shows the tags on the file)
        await IncludeTagsFileInNoteFile(noteFilePath: noteFilePath, tagsFilePath: tagsFilePath);
        // Xref the file being tagged in the tag index file (shows the file bein g tagged in the index)
        await UpdateTagIndexWithNoteRef(tagIndexFilePath: tagsIndexFilePath, noteFilePath: noteFilePath);
        // Xref the tag index file in the global Tags file (shoes the tag in the list of all tags) 
        var globalTagIndexPath = "_tag_index.adoc";
        await UpdateGlobalTagIndexWithTag(globalTagIndexPath: globalTagIndexPath, tagIndexFilePath: tagIndexFilePath);
    }

    public async Task IncludeTagsFileInNoteFile(string noteFilePath, string tagsFilePath)
    {
        var absNoteFilePath = GetAbsolutePath(noteFilePath);
        var absTagsFilePath = GetAbsolutePath(tagsFilePath);

        var relativePath = Path.GetRelativePath(absNoteFilePath, absTagsFilePath);
        var includeText = $"include::{relativePath}[]";
        var includeExists = await FileContainsLine(filePath: absNoteFilePath, line: includeText);
        if (!includeExists)
        {
            await this.fileSystem.File.AppendAllTextAsync(includeText);
        }
    }

    public async Task UpdateTagsFileWithTag(string tagsFilePath, string tagIndexFilePath)
    {
        var crossReferences = (await GetXrefsFromFileAsync(tagsFilePath)).ToHashSet();
        var newXref =  GetFileXReference(fromFilePath: tagsFilePath, toFilePath: tagIndexFilePath);
        //Using a hashset, so adding an existing item does nothing
        crossReferences.Add(newXref);
        await this.WriteXrefsToFile(filePath: tagsFilePath, xrefs: crossReferences, includeSidebar: false);
    }

    /// <summary>
    /// This writes xrefs to a file as a set of bullet points in alphapbetical order. 
    /// It replaces the content of the file with a newly created list, based on any
    /// existing xrefs and the new one. Each xref is only added once.
    /// </summary>
    /// <param name="filePath">Absolute or relative path to the file</param>
    /// <param name="xref"></param>
    public async Task WriteXrefsToFile(string filePath, IEnumerable<CrossReference> xrefs, bool includeSidebar = false)
    {
        var absPath = GetAbsolutePath(path: filePath);
        // Todo: this writes tags as a sidebar and unordered list. Make this configurable. 
        if (includeSidebar)
        {
            var textToWrite = new StringBuilder("[sidebar]");
            textToWrite.AppendLine();
            textToWrite.AppendLine();
        }
        var sortedXrefs = xrefs.OrderBy(xref => xref.CanonicalName);
        foreach (var xref in sortedXrefs)
        {
            textToWrite.AppendLine($"* {xref.ToString()}");
        }
        
        await this.fileSystem.File.WriteAllTextAsync(absPath, textToWrite.ToString());
    }

    public string GetAbsolutePath (string path)
    {
        if (!Path.IsPathRooted(path))
        {
            var basePath = GetExecutionPath();
            return Path.Combine(basePath, path);
        }
        return path;
    }

    private string GetExecutionPath()
    {
        string path = string.Empty;
        var baseExecutionPath = Assembly.GetExecutingAssembly().Location;
        var baseDirectory = Directory.GetParent(baseExecutionPath);
        path = baseDirectory?.FullName?? string.Empty;
        return path;
    }

    /// <summary>
    /// Specialized Search. Expects the xref to exist on a single line, not be split over two or more. 
    /// </summary>
    /// <param name="filePath">The absolute or relativer path to the file</param>
    /// <returns></returns>
    public async Task<IEnumerable<CrossReference>> GetXrefsFromFileAsync(string filePath)
    {
        var xrefs = new HashSet<CrossReference>();
        var absPath = GetAbsolutePath(filePath);
        if (this.fileSystem.File.Exists(absPath))
        {
            var fileLines = await this.fileSystem.File.ReadAllLinesAsync(absPath);
            for(int i = 0; i < fileLines.Length; i++)
            {                
                var xrefMatch = this.xrefRegEx.Match(fileLines[i]);
                if (!xrefMatch.Success)
                {
                    continue;
                }

                var referencedFilePath = xrefMatch.Groups["2"].Value;
                xrefs.Add(new CrossReference(linkUrl:referencedFilePath));                
            }
        }
        return xrefs;
    }


    /// <summary>
    /// Gets the include file used for tags associated with the file being tagged
    /// </summary>
    /// <param name="noteFilePath">Absolute or Relative Path to File Being Tagged</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public string GetTagsIncludeFilePath(string noteFilePath)
    {
        if (!pathToFileBeingTagged.EndsWith(".adoc"))
        {
            throw new InvalidOperationException("The file name bust end in '.adoc'");
        }
        return pathToFileBeingTagged.Replace(".adoc", ".tags");
    }

    /// <summary>
    /// Gets a relative cross reference between two files
    /// </summary>
    /// <param name="fromFilePath"></param>
    /// <param name="toFilePath"></param>
    /// <returns></returns>
    public CrossReference GetFileXReference(string fromFilePath, string toFilePath)
    {
        //* xref:Tags/tag_methodic.adoc[#Methodic#]
        var relativePath = Path.GetRelativePath(fromFilePath, toFilePath);

        var xref = new CrossReference(linkUrl: relativePath);
        return xref;
    }

    /// <summary>
    /// Checks a file for a matching line of text
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="line"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task<bool> FileContainsLine(string filePath, string line)
    {
        var absFilePath = GetAbsolutePath(filePath);
        if (!this.fileSystem.File.Exists(absfilePath))
        {
            throw new InvalidOperationException($"{absFilePath} cannot be found;")
        }

        var fileLines = await this.fileSystem.File.ReadAllLinesAsync();
        for (int i=0; i<fileLine.Length; i++)
        {
            if (fileLines == line)
            {
                return true;
            }
        }

        return false;
    }
}
