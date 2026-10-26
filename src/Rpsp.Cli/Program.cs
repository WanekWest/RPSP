namespace Rpsp.Cli;

internal static class Program
{
    public static int Main(string[] args)
    {
        return ReplayCli.Run(args, Console.Out, Console.Error);
    }
}
