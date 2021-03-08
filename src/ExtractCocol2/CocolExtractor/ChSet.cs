using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using uld.definition;
using uld.definition.Symbols;
using uld.definition.Symbols.Impl.Terminals;

public readonly struct ChSet
{
    public ImmutableArray<ISymbol>? Symbols { get; }

    public static readonly ChSet Any = new ChSet(new AnyCharacterTerminal());
    public static readonly ChSet EndOfLine = new ChSet(new AnyLineEndTerminal());

    public static ChSet FromCh(char ch) => new ChSet(new OneCharOfTerminal(new[] { ch }));

    public static ChSet FromChRange(char start, char end)
        => new ChSet(new OneCharOfTerminal(
            Enumerable.Range(start, end - start + 1)
                .Select(i => (char)i)
                .ToArray()));

    public static ChSet operator+(ChSet set1, ChSet set2)
    {
        var allSymbols = (set1.Symbols ?? ImmutableArray<ISymbol>.Empty)
            .Concat(set2.Symbols ?? ImmutableArray<ISymbol>.Empty);

        var chars = new HashSet<char>();
        var exceptChars = new HashSet<char>();

        foreach (var symbol in allSymbols)
        {
            if (symbol is AnyCharacterTerminal)
                return Any;
            if (symbol is OneCharOfTerminal oneCharOf)
                oneCharOf.Chars.ForEach(ch => chars.Add(ch));
            if (symbol is AnyCharExceptTerminal anyCharExcept)
                anyCharExcept.Chars.ForEach(ch => exceptChars.Add(ch));
            if (symbol is AnyLineEndTerminal anyLineEndTerminal)
                chars.Add('\n');
        }

        return new ChSet(
            new ISymbol[]
            {
                chars.Count > 0 ? new OneCharOfTerminal(chars.ToArray()) : null,
                exceptChars.Count > 0 ? new AnyCharExceptTerminal(exceptChars.ToArray()) : null
            }.WhereNotNull());
    }

    public static ChSet operator -(ChSet set1, ChSet set2)
    {
        return (set1.Symbols, set2.Symbols) switch
        {
            (null, null) => new ChSet(),
            (_, null) => set1,
            (null, _) => new ChSet(set2.Symbols.Value.Select<ISymbol, ISymbol>(s =>
            {
                if (s is AnyCharacterTerminal)
                    return null;
                if (s is OneCharOfTerminal oneCharOf)
                    return new AnyCharExceptTerminal(oneCharOf.Chars.ToArray());
                if (s is AnyCharExceptTerminal anyCharExcept)
                    return new OneCharOfTerminal(anyCharExcept.Chars.ToArray());

                return new AnyCharExceptTerminal(new[] { '\n' });
            })),
            (_, _) => Minus(set1.Symbols, set2.Symbols)
        };
    }

    public override string ToString()
        => !Symbols.HasValue || Symbols.Value.IsEmpty
            ? "<empty>"
            : Symbols.Value.JoinToString(" | ");

    private ChSet(ISymbol symbol)
    {
        Symbols = ImmutableArray.Create(symbol);
    }

    private ChSet(IEnumerable<ISymbol> symbols)
    {
        Symbols = ImmutableArray.CreateRange(symbols);
    }

    private static ChSet Minus(IEnumerable<ISymbol> symbols1, IEnumerable<ISymbol> symbols2)
    {
        var chars = new HashSet<char>();
        var exceptChars = new HashSet<char>();

        bool set1HasAnyAsBase = false;

        foreach (var symbol in symbols1)
        {
            switch (symbol)
            {
                case AnyCharacterTerminal _:
                    set1HasAnyAsBase = true;
                    break;
                case OneCharOfTerminal oneCharOf:
                    oneCharOf.Chars.ForEach(ch => chars.Add(ch));
                    break;
                case AnyCharExceptTerminal anyCharExcept:
                    set1HasAnyAsBase = true;
                    anyCharExcept.Chars.ForEach(ch => exceptChars.Add(ch));
                    break;
                case AnyLineEndTerminal _:
                    chars.Add('\n');
                    break;
            }
        }

        foreach (var symbol in symbols2)
        {
            switch (symbol)
            {
                case AnyCharacterTerminal _:
                    // shouldn't exist in set2
                    throw new Exception("anyCharacter in subtrahend");
                case OneCharOfTerminal oneCharOf:
                    oneCharOf.Chars.ForEach(ch =>
                    {
                        chars.Remove(ch);
                        if (set1HasAnyAsBase)
                            exceptChars.Add(ch);
                    });
                    break;
                case AnyCharExceptTerminal anyCharExcept:
                    // shouldn't exist in set2
                    throw new Exception("anyCharacterExcept in subtrahend");
                case AnyLineEndTerminal _:
                    exceptChars.Add('\n');
                    break;
            }
        }

        if (set1HasAnyAsBase)
            return new ChSet(new AnyCharExceptTerminal(exceptChars.ToArray()));

        return new ChSet(
            new ISymbol[]
            {
                chars.Count > 0 ? new OneCharOfTerminal(chars.ToArray()) : null,
                exceptChars.Count > 0 ? new AnyCharExceptTerminal(exceptChars.ToArray()) : null
            }.WhereNotNull());
    }
}
