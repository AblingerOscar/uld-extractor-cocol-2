using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

// ReSharper disable once CheckNamespace
public static class CocolExtractor
{
    public static DefinitionFileBuilder.DefinitionFileBuilder Builder { get; private set; }

    // ReSharper disable InconsistentNaming

    // all characters that are valid in XML
    public static readonly ImmutableHashSet<char> ANY_CHARACTER_SET = ImmutableHashSet.CreateRange(
            Enumerable.Range(char.MinValue, char.MaxValue - char.MinValue)
            .Select(i => (char)i)
            .Where(XmlConvert.IsXmlChar));
    public static readonly ImmutableHashSet<char> ANY_UPPERCASE_SET = ImmutableHashSet.CreateRange(Enumerable.Range('A', 'Z' - 'A' + 1).Select(i => (char)i));
    public static readonly ImmutableHashSet<char> ANY_LOWERCASE_SET = ImmutableHashSet.CreateRange(Enumerable.Range('a', 'z' - 'a' + 1).Select(i => (char)i));
    public static readonly ImmutableHashSet<char> ANY_LETTER_SET = ImmutableHashSet.CreateRange(ANY_CHARACTER_SET.Where(char.IsLetter));
    public static readonly ImmutableHashSet<char> ANY_DIGIT_SET = ImmutableHashSet.CreateRange(ANY_CHARACTER_SET.Where(char.IsDigit));
    public static readonly ImmutableHashSet<char> ANY_LETTER_OR_DIGIT_SET = ImmutableHashSet.CreateRange(ANY_LETTER_SET.Union(ANY_DIGIT_SET));
    public static readonly ImmutableHashSet<char> ANY_WHITESPACE_SET = ImmutableHashSet.CreateRange(ANY_CHARACTER_SET.Where(char.IsWhiteSpace));
    // ReSharper restore InconsistentNaming

    public static void Main(string[] args)
    {
        var outputFile = args.Length == 0 ? Console.ReadLine() : args[0];
        if (outputFile == null || Path.GetFileName(outputFile) == "")
        {
            Console.WriteLine("The path for the definition file is not valid");
            return;
        }

        Builder = new DefinitionFileBuilder.DefinitionFileBuilder();
        Console.WriteLine("finished set-up");

        Cocol2Extractor.Main(args.Skip(1).ToArray());

        if (Builder.Errors.Count > 0)
        {
            Console.WriteLine($"Found {Builder.Errors.Count} error{(Builder.Errors.Count == 1 ? "" : "s")} while parsing:");

            foreach (var error in Builder.Errors)
                Console.WriteLine('\t' + error);
        }

        Console.WriteLine("building file");
        var langDef = Builder.Build();

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

        Console.WriteLine("Serializing…");
        var xml = langDef.SerializeToXLinq();

        Console.WriteLine($"Writing xml into {outputFile}…");
        using (var stream = new StreamWriter(outputFile, false)) {
            new XDocument(new XDeclaration("1.0", "UTF-16", "yes"), xml).Save(stream, SaveOptions.None);
        }
        Console.WriteLine("All done.");
    }
}

