using DefinitionFileBuillder;
using System;
using System.Collections.Generic;
using System.Text;

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

        Console.WriteLine(langDef);
    }
}
