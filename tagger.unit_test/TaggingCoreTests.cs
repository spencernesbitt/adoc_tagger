using System.IO;
using Newtonsoft.Json;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit.Abstractions;
using tagger.core;

namespace tagger.unit_test;

public class TaggerCoreTests
{
    private readonly ITestOutputHelper testOutput;
    public TaggerCoreTests(ITestOutputHelper output)
    {
        this.testOutput = output;
        var converter = new OutputConverter(output);
        Console.SetOut(converter);        
    }

    private class OutputConverter : TextWriter
    {
        ITestOutputHelper _output;
        public OutputConverter(ITestOutputHelper output)
        {
            _output = output;
        }
        public override Encoding Encoding
        {
            get { return Encoding.UTF8; }
        }
        public override void WriteLine(string message)
        {
            _output.WriteLine(message);
        }
        public override void WriteLine(string format, params object[] args)
        {
            _output.WriteLine(format, args);
        }

        public override void Write(char value)
        {
            throw new NotSupportedException("This text writer only supports WriteLine(string) and WriteLine(string, params object[]).");
        }
    }

    [Fact]
    public void FileTests()
    {
        var path = "./Tests";
        var rootPath = this.GetExecutionPath();
        var newPath = Path.Combine(rootPath, path);
        //Directory.CreateDirectory(newPath);
        testOutput.WriteLine(newPath);
        Assert.True(true);
    }

    [Fact]
    public void RegexTest()
    {
        var tagRegExpPattern = "^(tag_)(\\w+)(\\.adoc)$";
        var tagRegEx = new Regex(tagRegExpPattern);
        var tagMatch = tagRegEx.Match("tag_this_is_a_tag.adoc");
        if (tagMatch.Success)
        {
            
            var matchAsString = JsonConvert.SerializeObject(tagMatch, Formatting.Indented,  
                new JsonSerializerSettings()
                { 
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                });

            //testOutput.WriteLine($"TagMatch: {matchAsString}");
            testOutput.WriteLine($"NamePart: {tagMatch.Groups["2"].Value}");

        }
        else
        {
            testOutput.WriteLine($"No Match");
        }
        Assert.True(true);
    }

    [Fact]
    public void TagUtilsShoudlIdentifyTagsFromFileNames()
    {        
        #region Arrange
        var potentialTagFiles = "somerandomfile.adoc;tag_this_is_a_tag.adoc;tag_not_a_tag.txt"; 
        var utils = new TagUtils();      
        #endregion Arrange
        
        #region Act
        testOutput.WriteLine($"Testing fileNames: {potentialTagFiles}");  
        var tags = utils.GetTagsFromFileNames(potentialTagFiles)?.ToList() ?? new List<Tag>();
        testOutput.WriteLine($"Found tags: {JsonConvert.SerializeObject(tags)}");
        #endregion Act

        #region Assert
        Assert.Single(tags);
        Assert.Equal("This Is A Tag", tags[0].Name);
        Assert.Equal("tag_this_is_a_tag.adoc", tags[0].FileName);
        #endregion Assert
    }

    [Fact]
    public void TagUtilsShouldIdentifyTagsFromDirectory()
    {
        #region Arrange
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { @"c:\tags\tag_this_is_a_tag.adoc", new MockFileData("This is a tage file") },
            { @"c:\tags\jQuery.js", new MockFileData("this is some js file. not a tag file") },
            { @"c:\tags\tag_this_is_not_a_tag.txt", new MockFileData(new byte[] { 0x12, 0x34, 0x56, 0xd2 }) }
        });
        #endregion Arrange

        var files = fileSystem.Directory.GetFiles(@"c:\tags");
        testOutput.WriteLine(JsonConvert.SerializeObject(files));

        #region Act
        var utils = new TagUtils(fileSystem);
        var tags = utils.GetTagsInFolder(@"c:\tags")?.ToList()?? new List<Tag>();
        #endregion Act

        #region Assert
        Assert.Single(tags);
        Assert.Equal("This Is A Tag", tags[0].Name);
        Assert.Equal(@"c:\tags\tag_this_is_a_tag.adoc", tags[0].FileName);
        #endregion Assert
    }

    [Fact]
    public void TagUtilsShouldGenerateTagFromName()
    {
        var tagName = "DotNet CLI";
        var utils = new TagUtils(); 
        var tags = utils.GetTagsFromTagNames(tagName)?.ToList() ?? new List<Tag>();
        
        Assert.Single(tags);
        Assert.Equal("DotNet CLI", tags[0].Name);
        Assert.Equal("tag_dotnet_cli.adoc", tags[0].FileName);
    }

    [Fact]
    public void TagUtilsShouldReturnAValidTagIncludesFile()
    {
        #region Arrange
        var utils = new TagUtils();
        var fileToBeTagged = @"c:\Notes\some_interesting_note.adoc";        
        #endregion Arrange 
        #region Act
        var tagIncludeFile = utils.GetTagsIncludeFilePath(pathToFileBeingTagged: fileToBeTagged);
        #endregion Act
        #region Assert
        Assert.Equal(@"c:\Notes\some_interesting_note.tags", tagIncludeFile);
        #endregion Assert
    }

    [Fact]
    public async Task GivenAValidFileTagUtilsShouldGetAllExistingXrefs()
    {
        #region Arrange
        // Add a sidebar style tag file
        var tagFile = new StringBuilder("[sidebar]");
        tagFile.AppendLine();
        // Add a valid xref
        tagFile.AppendLine("* xref:Tags/tag_cross_cloud_data_sharing.adoc[#Cross Cloud Data Sharing#]");
        // Add a duplicate reference that shoudl get filtered out on processinf
        tagFile.AppendLine("* xref:Tags/tag_cross_cloud_data_sharing.adoc[#Cross Cloud Data Sharing#]");
        // Add another xref
        tagFile.AppendLine("* xref:Tags/tag_another_cross_reference.adoc[#Another Cross Reference#]");
        var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            { @"c:\tags\something_interesting.tags", new MockFileData(tagFile.ToString())}           
        });
        var utils = new TagUtils(fileSystem: fileSystem);
        #endregion Arrange
        #region Act
        //var xrefs = (await GetXrefsFromFileAsync(filePath: @"c:\tags\something_interesting.tags", fileSystem: fileSystem)).ToList();
        var xrefs = (await utils.GetXrefsFromFileAsync(filePath: @"c:\tags\something_interesting.tags")).ToList();
        testOutput.WriteLine($"xrefs: {JsonConvert.SerializeObject(xrefs)}");

        #endregion Act
        #region Assert
        Assert.Equal(2, xrefs.Count);
        #endregion Assert
    }

    [Fact]
    public async Task GivenValidXrefsAndFilePathTagUtilsShouldWriteXrefsToFile()
    {
        #region Arrange
        var xrefs = new List<CrossReference>(12);
        xrefs.Add(new CrossReference(linkUrl: @"Tags\tag_this_is_the_first_tag.adoc"));
        xrefs.Add(new CrossReference(linkUrl: @"Tags\tag_this_is_the_second_tag.adoc"));
        xrefs.Add(new CrossReference(linkUrl: @"Tags\tag_this_is_the_third_tag.adoc"));
        var fileSystem = new MockFileSystem();
        var utils = new TagUtils(fileSystem: fileSystem);
        #endregion Arrange

        #region Act
        var tagFilePath = @"c:\this_is_a_tag_inlude_file.tags"; 
        await utils.WriteXrefsToFile(filePath: tagFilePath, xrefs: xrefs);
        var fileLines = await fileSystem.File.ReadAllLinesAsync(tagFilePath);
        for (var i = 0; i<fileLines.Length; i++)
        {
            testOutput.WriteLine($"TagFileText Line {i}: {fileLines[i]}");
        }
        
        #endregion Act

        #region Assert
        Assert.Equal(5, fileLines.Length);

        #endregion Assert

    }

    private string GetExecutionPath()
    {
        string path = string.Empty;
        var baseExecutionPath = Assembly.GetExecutingAssembly().Location;
        var baseDirectory = Directory.GetParent(baseExecutionPath);
        path = baseDirectory?.FullName?? string.Empty;
        return path;
    }
}