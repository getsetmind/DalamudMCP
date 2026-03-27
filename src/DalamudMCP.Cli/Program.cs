namespace DalamudMCP.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        await using Stream stdout = Console.OpenStandardOutput();
        return await CliProgram.RunAsync(args, Console.Out, Console.Error, stdout, CancellationToken.None).ConfigureAwait(false);
    }
}
