using autosupport_lsp_server;
using autosupport_lsp_server.Symbols.Impl.Terminals;
using DefinitionFileBuillder;
using System;
using System.Linq;

public static class CocolExtractor
{
    public static DefinitionFileBuilder Builder { get; private set; }

    public static void Main(string[] args)
    {
        Builder = new DefinitionFileBuilder();
        Console.WriteLine("finished set-up");


        Cocol2Extractor.Main(args);


        Console.WriteLine("building file");
        var langDef = Builder.Build();

        Console.WriteLine($"While Building the file, there were {Builder.Errors.Count} error{(Builder.Errors.Count == 1 ? "" : "s")}");

        foreach (var error in Builder.Errors)
            Console.WriteLine('\t' + error);


        Console.WriteLine("Verifying result…");
        var errors = langDef.VerifyAndGetErrors();
        if (errors.Length == 0)
            Console.WriteLine("No errors found :)");
        else
            Console.WriteLine($"Found {errors.Length} error{(errors.Length == 1 ? "" : "s")}:");

        foreach (var error in errors)
            Console.WriteLine('\t' + error);

        if (errors.Length != 0)
            return;

        Console.WriteLine("Printing Language Definition File to STDOUT:");
        Console.WriteLine();
        Console.WriteLine(langDef.SerializeToXLinq());
    }
}
