using System.CommandLine;

using tagger.core;

namespace tagger.program;

class Program
{
    static async Task<int> Main(string[] args)
    {        
        var rootCommand = ConfigureCommandLine();
        try
        {
            await rootCommand.InvokeAsync(args);
            return 0;            
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return 1;
        }        
    }

    static RootCommand ConfigureCommandLine()
    {
        var rootCommand = new RootCommand
        {
            Description = "Modifies tags on the sepcified file and updates the related index files."            
        };

        var tagCommand = new Command("tag", "Modify tags for a file");
        rootCommand.Add(tagCommand);
        var tagAddCommand = new Command("add", "Add a tag to a file")
        {           
        };
        tagCommand.Add(tagAddCommand);

        var fileArgument = new Argument<string>
        (
            name: "file", 
            description: "the file being tagged, e.g. my-interesting-note.adoc"        
        );

        tagAddCommand.Add(fileArgument);

        var tagArgument = new Argument<string>
        (
            name: "tag name", 
            description: "the tag to apply, e.g. \"Best Life Hacks\""        
        );

        tagAddCommand.Add(tagArgument);

        var templateOption =  new Option<string>(["-t", "--template"], "The template that controls how tags are displayed");
        tagAddCommand.Add(templateOption);
        
        tagAddCommand.SetHandler(async (fileArgumentValue, tagArgumentValue, templateOptionValue) =>
        {            
            // call tag utils to tag the file
            // ToDo: Pass in template file
            var utils = new TagUtils();
            await utils.TagNoteFileAsync(fileArgumentValue, tagArgumentValue);                            
        }, fileArgument, tagArgument, templateOption);

        return rootCommand;
    }
}
