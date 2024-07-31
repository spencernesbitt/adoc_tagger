namespace tagger.program;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Called with args: ");
        foreach (var arg in args){
            Console.WriteLine(arg);
        }

        Console.WriteLine("---");
    }
}
