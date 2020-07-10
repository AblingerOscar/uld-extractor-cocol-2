using autosupport_lsp_server;
using autosupport_lsp_server.Symbols;
using autosupport_lsp_server.Symbols.Impl;
using autosupport_lsp_server.Symbols.Impl.Terminals;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Xml;

namespace DefinitionFileBuillder
{
    public class DefinitionFileBuilder
    {
        private const string WHITESPACE_RULE = "$$ws";

        private bool nextCommentIsAction = false;
        private StringBuilder currentComment = new StringBuilder();
        private CommentParserState commentParserState = CommentParserState.Heading;

        private List<string> keywords = new List<string>();

        private string languageId;
        private string languageFilePattern;
        private IList<CommentRule> documentationComments = new List<CommentRule>();
        private IList<CommentRule> normalComments = new List<CommentRule>();
        private IList<string> startRules = new List<string>();
        private Dictionary<string, IRule> rules = new Dictionary<string, IRule>();
        private IList<string> rulesToForceWhitespaceInBetween = new List<string>();

        public IList<string> Errors = new List<string>();

        public DefinitionFileBuilder()
        {
            rules.Add(WHITESPACE_RULE,
                new Rule(WHITESPACE_RULE,
                    new ISymbol[]
                    {
                        new AnyWhitespaceTerminal(),
                        new OneOf(true, new[] { WHITESPACE_RULE })
                    }));
        }

        public IAutosupportLanguageDefinition Build()
        {
            CommentIsFinished();

            rulesToForceWhitespaceInBetween.ForEach(ruleName =>
                rules[ruleName] = new Rule(ruleName, AddForcedWhitespace(rules[ruleName].Symbols)));
            AddWhitespaceRulesToStartRules();

            return new AutosupportLanguageDefinition(
                    languageId, languageFilePattern, commentRules: new CommentRules(normalComments.ToArray(), documentationComments.ToArray()), startRules.ToArray(), rules
                );
        }

        private void AddWhitespaceRulesToStartRules()
        {
            foreach (var startRule in startRules)
            {
                var newSymbols = new List<ISymbol>(rules[startRule].Symbols);

                // add optional whitespace at the start of the start rule(s)
                newSymbols.Insert(0, new OneOf(true, new[] {WHITESPACE_RULE}));

                ConvertWsAtEndToOptional(startRule, newSymbols);

                rules[startRule] = new Rule(startRule, newSymbols);
            }
        }

        private void ConvertWsAtEndToOptional(string startRule, IList<ISymbol> newSymbols, ISet<string> previousRules = null)
        {
            if (previousRules != null && previousRules.Contains(startRule))
               return;

            previousRules ??= new HashSet<string>();
            previousRules.Add(startRule);

            // convert any whitespace rules at the end to be optional
            var lastWsSymbolIdx = newSymbols.Count - 1;
            ISymbol lastWsSymbol = null;
            while (lastWsSymbolIdx >= 0 && lastWsSymbol == null)
            {
                var symbol = newSymbols[lastWsSymbolIdx];
                if (symbol is NonTerminal || symbol is OneOf)
                    lastWsSymbol = symbol;
                else
                    --lastWsSymbolIdx;
            }

            lastWsSymbol?.Match(
                terminal: _ => throw new Exception(), // shouldn't ever be possible
                nonTerminal =>
                {
                    if (nonTerminal.ReferencedRule == WHITESPACE_RULE)
                        newSymbols[lastWsSymbolIdx] = new OneOf(true, new[] { WHITESPACE_RULE });
                    else
                        ConvertWsAtEndToOptional(
                            nonTerminal.ReferencedRule,
                            new List<ISymbol>(rules[nonTerminal.ReferencedRule].Symbols),
                            previousRules);
                },
                action: _ => throw new Exception(), // shouldn't ever be possible
                oneOf =>
                {
                    if (oneOf.Options.Contains(WHITESPACE_RULE))
                        newSymbols[lastWsSymbolIdx] = new OneOf(true, oneOf.Options);
                    else
                        oneOf.Options.ForEach(option => ConvertWsAtEndToOptional(
                            option,
                            new List<ISymbol>(rules[option].Symbols),
                            previousRules));

                }
            );
        }

        public void NextCommentIsAction(bool isAction)
        {
            CommentIsFinished();
            nextCommentIsAction = isAction;
        }

        public void AddCommentChar(char ch)
            => currentComment.Append(ch);

        public void CommentIsFinished()
        {
            if (nextCommentIsAction && currentComment.Length > 0)
            {
                switch (commentParserState)
                {
                    case CommentParserState.Heading:
                        InterpretHeadingComment();
                        break;
                    case CommentParserState.Grammar:
                        // TODO
                        break;
                }
            }

            currentComment.Clear();
        }

        private void InterpretHeadingComment()
        {
            var comment = currentComment.ToString().Trim().Split(' ');
            if (comment.Length < 2)
            {
                Console.WriteLine("heading annotation does not have enough arguments: <" + comment.JoinToString(" ") + ">");
                return;
            }

            var arg = comment[1];

            switch (comment[0])
            {
                case "id":
                    languageId = arg;
                    break;
                case "filePattern":
                    languageFilePattern = arg;
                    break;
                default:
                    Console.WriteLine("unrecognised heading annotation: " + arg);
                    break;
            }
        }

        public void StartOfGrammar()
        {
            CommentIsFinished();
        }

        public void StartOfRules()
        {
            CommentIsFinished();
        }

        public void AddStartRule(string ruleName)
        {
            startRules.Add(ruleName);
        }

        public void AddKeyword(string keyword) => keywords.Add(keyword);

        public void AddRule(string ruleName, ISymbol[] symbols, bool forceWhitespacesInBetween = true, bool forceWhitespacesAtTheEnd = false)
        {
            if (forceWhitespacesInBetween)
                rulesToForceWhitespaceInBetween.Add(ruleName);
            if (forceWhitespacesAtTheEnd)
                symbols = symbols.Append(new NonTerminal(WHITESPACE_RULE)).ToArray();

            rules.Add(ruleName, new Rule(ruleName, symbols));
        }

        private IEnumerable<ISymbol> AddForcedWhitespace(IReadOnlyList<ISymbol> symbols)
        {
            var forcedWs = new NonTerminal(WHITESPACE_RULE);
            var symbolsWithWs = new List<ISymbol>();

            var i = -1;
            foreach(var symbol in symbols)
            {
                ++i;
                symbol.Match(
                    terminal =>
                    {
                        symbolsWithWs.Add(terminal);
                        if (terminal is StringTerminal str
                            && keywords.Contains(str.String)
                            && SymbolAtPositionCanOnlyBeKeyword(symbols, i + 1))
                            symbolsWithWs.Add(forcedWs);
                        else // token
                            symbolsWithWs.Add(new OneOf(true, new[] { WHITESPACE_RULE }));
                    },
                    nonTerminal =>
                    {
                        symbolsWithWs.Add(nonTerminal);
                    },
                    action =>
                    {
                        // insert action at the end, but before any whitespace
                        if (symbolsWithWs.Count != 0
                            && symbolsWithWs.Last() is NonTerminal nt
                            && nt.ReferencedRule == WHITESPACE_RULE)
                            symbolsWithWs.Insert(symbolsWithWs.Count - 1, action);
                        else
                            symbolsWithWs.Add(action);
                    },
                    oneOf =>
                    {
                        symbolsWithWs.Add(oneOf);
                    }
                );
            };

            return symbolsWithWs;
        }

        private bool SymbolAtPositionCanOnlyBeKeyword(IReadOnlyList<ISymbol> symbols, int i, IImmutableList<string> visitedSymbols = null)
        {
            if (symbols.Count <= i)
                return false;

            visitedSymbols ??= ImmutableArray.Create<string>();
            var symbol = symbols[i];

            return symbol.Match(
                terminal => terminal is StringTerminal strTerm && keywords.Contains(strTerm.String),
                nonTerminal =>
                {
                    if (visitedSymbols.Contains(nonTerminal.ReferencedRule))
                        return false;
                    return SymbolAtPositionCanOnlyBeKeyword(
                        rules[nonTerminal.ReferencedRule].Symbols,
                        0,
                        visitedSymbols.Add(nonTerminal.ReferencedRule));
                },
                action => SymbolAtPositionCanOnlyBeKeyword(symbols, i + 1),
                oneOf =>
                    oneOf.Options.All(opt =>
                        !visitedSymbols.Contains(opt)
                        && SymbolAtPositionCanOnlyBeKeyword(rules[opt].Symbols, 0, visitedSymbols.Add(opt)))
                    && (!oneOf.AllowNone || SymbolAtPositionCanOnlyBeKeyword(symbols, i + 1))
            );
        }

        public void AddCharacterSetRule(string name, ISet<char> chSet)
        {
            ISymbol[] symbols;

            var sanitizedChSet = new HashSet<char>(chSet.Where(XmlConvert.IsXmlChar));

            if (sanitizedChSet.Count < chSet.Count)
                Console.WriteLine($"Warning: some characters were dropped from character set {name} as they are not valid " +
                    $"xml characters: <{chSet.Except(sanitizedChSet).JoinToString(">, <")}>");

            if (sanitizedChSet.SetEquals(CocolExtractor.ANY_CHARACTER_SET))
                symbols = new[] { new AnyCharacterTerminal() };
            else
            {
                var symbolNames = new List<string>();

                ExtractAllSubsets(sanitizedChSet, symbolNames);

                foreach (var ch in sanitizedChSet)
                {
                    var chRuleName = "$$character_" + ch;

                    symbolNames.Add(chRuleName);
                    if (!rules.ContainsKey(chRuleName))
                        rules.Add(chRuleName, new Rule(chRuleName, new[] { new StringTerminal(ch.ToString()) }));
                }

                symbols = symbolNames.Count == 1
                    ? new ISymbol[] { new NonTerminal(symbolNames[0]) }
                    : new ISymbol[] { new OneOf(false, symbolNames.ToArray()) };
            }

            rules.Add(name, new Rule(name, symbols));
        }

        private void ExtractAllSubsets(ISet<char> chSet, List<string> symbolNames)
        {
            ExtractSubsetIfPossible(chSet, symbolNames, CocolExtractor.ANY_LETTER_OR_DIGIT_SET, "$$any_letter_or_digit", () => new AnyLetterOrDigitTerminal());
            ExtractSubsetIfPossible(chSet, symbolNames, CocolExtractor.ANY_LETTER_SET, "$$any_letter", () => new AnyLetterTerminal());
            ExtractSubsetIfPossible(chSet, symbolNames, CocolExtractor.ANY_DIGIT_SET, "$$any_digit", () => new AnyDigitTerminal());
            ExtractSubsetIfPossible(chSet, symbolNames, CocolExtractor.ANY_UPPERCASE_SET, "$$any_uppercase", () => new AnyUppercaseLetterTerminal());
            ExtractSubsetIfPossible(chSet, symbolNames, CocolExtractor.ANY_LOWERCASE_SET, "$$any_lowercase", () => new AnyLowercaseLetterTerminal());
            ExtractSubsetIfPossible(chSet, symbolNames, ImmutableHashSet.Create('\n'), "$$any_lineEnd", () => new AnyLineEndTerminal());
            ExtractSubsetIfPossible(chSet, symbolNames, ImmutableHashSet.Create('\r'), "$$any_lineEnd", () => new AnyLineEndTerminal());
            ExtractSubsetIfPossible(chSet, symbolNames, CocolExtractor.ANY_WHITESPACE_SET, "$$any_whitespace", () => new AnyWhitespaceTerminal());
        }

        private void ExtractSubsetIfPossible(ISet<char> chSet, IList<string> symbolNames, ImmutableHashSet<char> subset, string name, Func<ISymbol> createSymbol)
        {
            if (subset.IsSubsetOf(chSet))
            {
                chSet.ExceptWith(subset);
                symbolNames.Add(name);

                if (!rules.ContainsKey(name))
                    rules.Add(name, new Rule(name, new[] { createSymbol() }));
            }
        }

        private enum CommentParserState
        {
            Heading,
            Grammar
        }
    }
}
