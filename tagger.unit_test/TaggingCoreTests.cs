using System.IO;
using System.Reflection;
using Xunit.Abstractions;
namespace tagger.unit_test;

public class TaggerCoreTests
{
    private readonly ITestOutputHelper testOutput;
    private readonly string rootFileFolder;
    public TaggerCoreTests(ITestOutputHelper output)
    {
        this.testOutput = output;
        this.rootFileFolder = this.GetExecutionDirectory();
    }

    [Fact]
    public void TaggerShoudlIdentifyTagFilesAsTags()
    {        
        var tagFolder = Path.Combine(rootFolder, "Tags");
        


        testOutput.WriteLine($"Using Tag folder: {tagFolder}");
        Assert.True(true);
    }

    private string GetTagsFolder()
    {

    }

    private string GetExecutionDirectory()
    {
        string path = string.Empty;
        var baseExecutionPath = Assembly.GetExecutingAssembly().Location;
        var baseDirectory = Directory.GetParent(baseExecutionPath);
        path = baseDirectory?.FullName?? string.Empty;
        return path;
    }
}