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
    private readonly string workingPath;
    private readonly Regex tagRegEx;
    private readonly Regex xrefRegEx;
    
    /// <summary>
    /// Constructor to supclport unit testing with mock FileSystem 
    /// </summary>
    /// <param name="fileSystem">Allows injection of Mock file System for testing</param>
    /// /// <param name="fileSystem">Allows specification of working folder to resolve relative paths in mock file system</param>
    public TagUtils(IFileSystem fileSystem, string workingPath)
    {
        this.fileSystem = fileSystem;
        this.workingPath = workingPath;
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
        //this.adocFileRegEx = new Regex(@"^(.+\/)?(.+?)(?=\.adoc)");
        // match tags files such as:
        /*        
            Tags/tag_methodic.tags
            ../data_mesh_event_streaming.tags
            some_file.tags
            Some_file.tags
        */
        //this.tagsFileRegEx = new Regex(@"^(.+\/)?(.+?)(?=\.tags)");
    }

    //uses System.IO.Abstractions default implementaion that calls System.IO.FileSystem under the hood
    public TagUtils():this(fileSystem: new FileSystem(), string.Empty)
    {
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="filePath" example="some_interesting_note.adoc">Relative or Absolute Path to file being tagged</param>
    /// <param name="tag" example="Special Interest">the tag to apply</param>
    /// <returns></returns>
    public async Task TagNoteFileAsync(string noteFilePath, string tag)
    {       
        // Xref the tag index file to the tags file for the file being tagged (Links back to all files that reference the same tag)
        var tagsFilePath = this.GetTagsIncludeFilePath(noteFilePath: noteFilePath); // ./some_interesting_note.tags
        Console.WriteLine($"tagsFilePath: {tagsFilePath}");
        var tagIndexFilePath = this.GetTagIndexFilePath(noteFilePath:noteFilePath, tag: tag); // ./Tags/some_tag.adoc
        Console.WriteLine($"tagIndexFilePath: {tagIndexFilePath}");

        // Updates the ./some_interesting_note.tags file with the new tag
        await UpdateFileCrossReferences(fileToUpdate: tagsFilePath, fileToReference: tagIndexFilePath, templateType: IncludeTemplateType.Sidebar);

        // Import the tags file into the file being tagged (shows the tags on the file)
        await IncludeTagsFileInNoteFile(noteFilePath: noteFilePath, tagsFilePath: tagsFilePath);

        // Updates the ./Tags/{tag_file}.adoc file with a xref to the note file being tagged
        await UpdateFileCrossReferences(fileToUpdate: tagIndexFilePath, fileToReference: noteFilePath, templateType: IncludeTemplateType.None);
        
        // Xref the tag index file in the global Tags file (shoes the tag in the list of all tags) 
        var globalTagIndexPath = "_tag_index.adoc";
        await UpdateFileCrossReferences(fileToUpdate: globalTagIndexPath, fileToReference: tagIndexFilePath, templateType: IncludeTemplateType.None);
    }

    /// <summary>
    /// Returns the aboslute path for the relative path ./Tags/<some_tag>.adoc from the note path.
    /// </summary>
    /// <param name="noteFilePath"></param>
    /// <param name="tag"></param>
    /// <returns></returns>
    public string GetTagIndexFilePath(string noteFilePath, string tag)
    {
        var absNoteFilePath = GetAbsolutePath(noteFilePath);
        var directory = new FileInfo(absNoteFilePath).Directory.FullName;
        Console.WriteLine($"GetTagIndexFilePath - note file Directory: {directory}");
        var relativeTagPath = $"./Tags/{GetTagFileNameFromTag(tag)}.adoc";
        var newPath = this.fileSystem.Path.GetFullPath(this.fileSystem.Path.Combine(directory, relativeTagPath));
        // Create the folder if it does not already exist
        //var file = new FileInfo(newPath);
        this.fileSystem.Directory.CreateDirectory(newPath);
        //file.Directory.Create(); // If the directory already exists, this method does nothing.                
        Console.WriteLine($"GetTagIndexFilePath - new path: {newPath}");
        return newPath;
    }            

    /// <summary>
    /// Converts things like "Quite Interesting" to "quite_interesting"
    /// </summary>
    /// <param name="tag"></param>
    /// <returns></returns>
    public string GetTagFileNameFromTag(string tag)
    {
        return tag.ToLower().Replace(' ', '_');
    }

    public async Task IncludeTagsFileInNoteFile(string noteFilePath, string tagsFilePath)
    {
        var absNoteFilePath = GetAbsolutePath(noteFilePath);
        var absTagsFilePath = GetAbsolutePath(tagsFilePath);

        var relativePath = this.GetRelativePath(absNoteFilePath, absTagsFilePath);
        var includeText = $"include::{relativePath}[]";
        var includeExists = await FileContainsLine(filePath: absNoteFilePath, line: includeText);
        if (!includeExists)
        {
            await this.fileSystem.File.AppendAllTextAsync(absNoteFilePath, includeText);
        }
    }

    /// <summary>
    /// Replaces the current file contents with an updated list of cross references
    /// </summary>
    /// <param name="fileToUpdate">Absolute or Relative path to file being updated</param>
    /// <param name="fileToReference">Absolute or Relative path to file being refrenced</param>
    /// <returns></returns>
    public async Task UpdateFileCrossReferences(string fileToUpdate, string fileToReference, IncludeTemplateType templateType)
    {       
        Console.WriteLine($"UpdateFileCrossReferences - fileToUpdate: {fileToUpdate}");
        var absFileToUpdatePath = GetAbsolutePath(fileToUpdate);
        Console.WriteLine($"UpdateFileCrossReferences - absFileToUpdatePath: {absFileToUpdatePath}");
        // Build a list of existing notes and replace all content with the updated list in alphabetical order
        var crossReferences = (await GetXrefsFromFileAsync(absFileToUpdatePath)).ToHashSet();
        var newXref =  GetFileXReference(fromFilePath: absFileToUpdatePath, toFilePath: fileToReference);
        //Using a hashset, so adding an existing item does nothing
        crossReferences.Add(newXref);
        await this.WriteXrefsToFile(filePath: absFileToUpdatePath, xrefs: crossReferences, templateType: templateType);
    }

    /// <summary>
    /// This writes xrefs to a file as a set of bullet points in alphapbetical order. 
    /// It replaces the content of the file with a newly created list, based on any
    /// existing xrefs and the new one. Each xref is only added once.
    /// </summary>
    /// <param name="filePath">Absolute or relative path to the file</param>
    /// <param name="xref"></param>
    public async Task WriteXrefsToFile(string filePath, IEnumerable<CrossReference> xrefs, IncludeTemplateType templateType)
    {
        var absPath = GetAbsolutePath(path: filePath);
        
        var template = Templates.GetIncludeTemplate(templateType);
        var xrefsBuilder = new StringBuilder();
        
        var sortedXrefs = xrefs.OrderBy(xref => xref.CanonicalName);
        foreach (var xref in sortedXrefs)
        {
            xrefsBuilder.AppendLine($"* {xref.ToString()}");
        }
        this.fileSystem.File.CreateText(absPath);
        string textToWrite;
        if (!string.IsNullOrWhiteSpace(template.Template))
        {
            textToWrite = template.Template.Replace(template.ReplaceMarker, xrefsBuilder.ToString()); 
        }
        else
        {
            textToWrite = xrefsBuilder.ToString();
        }               
                
        await this.fileSystem.File.WriteAllTextAsync(absPath, textToWrite);        
    }

    public string GetAbsolutePath (string path)
    {
        Console.WriteLine($"GetAbsolutePath checking path: {path}");
        if (!this.fileSystem.Path.IsPathRooted(path))
        {
            var basePath = GetExecutionPath();
            return this.fileSystem.Path.Combine(basePath, path);
        }
        return path;
    }

    private string GetExecutionPath()
    {
        if (!string.IsNullOrWhiteSpace(this.workingPath))
        {
            return this.workingPath;
        }
        string path = string.Empty;
        var baseExecutionPath = Assembly.GetExecutingAssembly().Location;
        var baseDirectory = Directory.GetParent(baseExecutionPath);
        path = baseDirectory?.FullName?? string.Empty;
        return path;
    }

    /// <summary>
    /// Specialized Search. Expects the xref to exist on a single line, not be split over two or more. 
    /// </summary>
    /// <param name="filePath">The absolute or relative path to the file</param>
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
        if (!noteFilePath.EndsWith(".adoc"))
        {
            throw new InvalidOperationException("The file name bust end in '.adoc'");
        }
        return noteFilePath.Replace(".adoc", ".tags");
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
        var relativePath = this.GetRelativePath(fromFilePath, toFilePath);
        var xref = new CrossReference(linkUrl: relativePath);
        return xref;
    }

    public string GetRelativePath(string fromPath, string toPath)
    {
        var fromDir = new FileInfo(fromPath).Directory.FullName;
        var toDir = new FileInfo(toPath).Directory.FullName;
        var relativeDir = Path.GetRelativePath(fromDir, toDir);
        var relativeFile = Path.Combine(relativeDir, new FileInfo(toPath).Name); 
        return relativeFile;
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
        if (!this.fileSystem.File.Exists(absFilePath))
        {
            throw new InvalidOperationException($"{absFilePath} cannot be found;");
        }

        var fileLines = await this.fileSystem.File.ReadAllLinesAsync(absFilePath);
        for (int i=0; i<fileLines.Length; i++)
        {
            if (fileLines[i] == line)
            {
                return true;
            }
        }

        return false;
    }
}
