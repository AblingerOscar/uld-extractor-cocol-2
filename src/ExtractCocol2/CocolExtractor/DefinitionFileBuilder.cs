using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Xml;
using autosupport_lsp_server;
using autosupport_lsp_server.Symbols;
using autosupport_lsp_server.Symbols.Impl;
using autosupport_lsp_server.Symbols.Impl.Terminals;
using Action = autosupport_lsp_server.Symbols.Impl.Action;

// ReSharper disable once CheckNamespace
namespace DefinitionFileBuilder
{
    public class DefinitionFileBuilder
    {
        private const string WHITESPACE_RULE = "$$ws";

        private bool nextCommentIsAction;
        private bool nextCommentIsAroundAction;
        private readonly StringBuilder currentComment = new StringBuilder();
        private readonly List<IAction> aroundActions = new List<IAction>();
        private readonly List<IAction> specificActions = new List<IAction>();
        private CommentParserState commentParserState = CommentParserState.HEADING;

        private readonly List<string> keywords = new List<string>();

        private string languageId;
        private string languageFilePattern;
        private readonly IList<CommentRule> documentationComments = new List<CommentRule>();
        private readonly IList<CommentRule> normalComments = new List<CommentRule>();
        private readonly IList<string> startRules = new List<string>();
        private readonly Dictionary<string, IRule> rules = new Dictionary<string, IRule>();
        private readonly IList<string> rulesToForceWhitespaceInBetween = new List<string>();

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

        public ILanguageDefinition Build()
        {
            rulesToForceWhitespaceInBetween.ForEach(ruleName =>
                rules[ruleName] = new Rule(ruleName, AddForcedWhitespace(rules[ruleName].Symbols)));
            AddWhitespaceRulesToStartRules();

            return new LanguageDefinition(
                languageId, languageFilePattern,
                new CommentRules(normalComments.ToArray(), documentationComments.ToArray()),
                startRules.ToArray(), rules
            );
        }

        private void AddWhitespaceRulesToStartRules()
        {
            foreach (var startRule in startRules)
            {
                var newSymbols = new List<ISymbol>(rules[startRule].Symbols);

                // add optional whitespace at the start of the start rule(s)
                newSymbols.Insert(0, new OneOf(true, new[] { WHITESPACE_RULE }));

                ConvertWsAtEndToOptional(startRule, newSymbols);

                rules[startRule] = new Rule(startRule, newSymbols);
            }
        }

        private void ConvertWsAtEndToOptional(string startRule, IList<ISymbol> newSymbols,
            ISet<string> previousRules = null)
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

        public void NextCommentIsAroundAction(bool isAction)
        {
            nextCommentIsAroundAction = nextCommentIsAction && isAction;
        }

        public void AddCommentChar(char ch)
            => currentComment.Append(ch);

        private void CommentIsFinished()
        {
            if (nextCommentIsAction && currentComment.Length > 0)
            {
                switch (commentParserState)
                {
                    case CommentParserState.HEADING:
                        InterpretHeadingComment();
                        break;
                    case CommentParserState.GRAMMAR:
                        InterpretGrammarComment();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            currentComment.Clear();
        }

        private void InterpretHeadingComment()
        {
            var comment = currentComment.ToString().Trim().Split(' ');
            if (comment.Length < 2)
            {
                Errors.Add("heading annotation does not have enough arguments: <"
                           + comment.JoinToString(" ")
                           + ">");
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
                    Errors.Add("unrecognised heading annotation: " + arg);
                    break;
            }
        }

        private void InterpretGrammarComment()
        {
            var comment = currentComment.ToString().Trim();
            if (nextCommentIsAroundAction)
                aroundActions.Add(new Action(comment));
            else
                specificActions.Add(new Action(comment));
        }

        public IAction[] PopAllAroundActions()
        {
            CommentIsFinished();
            var actions = aroundActions.ToArray();
            aroundActions.Clear();
            return actions;
        }

        public IAction[] PopAllSpecificActions()
        {
            CommentIsFinished();
            var actions = specificActions.ToArray();
            specificActions.Clear();
            return actions;
        }

        public void StartOfGrammar()
        {
            CommentIsFinished();
            commentParserState = CommentParserState.GRAMMAR;
        }

        public void AddStartRule(string ruleName)
        {
            startRules.Add(ruleName);
        }

        public void AddKeyword(string keyword) => keywords.Add(keyword);

        public void AddRule(
            string ruleName,
            ISymbol[] symbols,
            bool forceWhitespacesInBetween = true,
            bool forceWhitespacesAtTheEnd = false)
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
            foreach (var symbol in symbols)
            {
                ++i;
                symbol.Match(
                    terminal =>
                    {
                        symbolsWithWs.Add(terminal);
                        if (terminal is StringTerminal str
                            && keywords.Contains(str.String)
                            // ReSharper disable once AccessToModifiedClosure
                            && SymbolAtPositionCanOnlyBeKeyword(symbols, i + 1))
                            symbolsWithWs.Add(forcedWs);
                        else // token
                            symbolsWithWs.Add(new OneOf(true, new[] { WHITESPACE_RULE }));
                    },
                    nonTerminal => { symbolsWithWs.Add(nonTerminal); },
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
                    oneOf => { symbolsWithWs.Add(oneOf); }
                );
            }

            return symbolsWithWs;
        }

        private bool SymbolAtPositionCanOnlyBeKeyword(IReadOnlyList<ISymbol> symbols, int i,
            IImmutableList<string> visitedSymbols = null)
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

        public void AddComments(string start, string end, bool isDocumentation = false)
        {
            if (isDocumentation)
                documentationComments.Add(new CommentRule(start, end, " "));
            else
                normalComments.Add(new CommentRule(start, end, " "));
        }

        public void AddCharacterSetRule(string name, ChSet chSet)
        {
            if (!chSet.Symbols.HasValue)
            {
                Errors.Add($"Character set {name} is an empty set. No rule was added!");
                return;
            }

            var additionalRules = new List<string>(2);
            var symbols = new List<ISymbol>();

            var nonXmlCharacters = chSet.Symbols.Value.SelectMany(s => s switch
            {
                OneCharOfTerminal oneCharOf => oneCharOf.Chars,
                AnyCharExceptTerminal anyCharExcept => anyCharExcept.Chars,
                _ => Enumerable.Empty<char>()
            }).ToImmutableHashSet();



            // sanitizing all characters to be xml-valid
            foreach (var symbol in chSet.Symbols.Value)
            {
                switch (symbol)
                {
                    case OneCharOfTerminal oneCharOf:
                        var xmlChars = oneCharOf.Chars.Where(XmlConvert.IsXmlChar).ToHashSet();
                        ExtractAllSubsets(xmlChars, additionalRules);

                        if (xmlChars.Count > 0)
                            symbols.Add(new OneCharOfTerminal(xmlChars.ToArray()));
                        break;
                    case AnyCharExceptTerminal anyCharExcept:
                        symbols.Add(
                            new AnyCharExceptTerminal(anyCharExcept.Chars.Where(XmlConvert.IsXmlChar).ToArray()));
                        break;
                    default:
                        symbols.Add(symbol);
                        break;
                }
            }

            if (additionalRules.Count == 0)
                rules.Add(name, new Rule(name, symbols));
            else if (symbols.Count == 0 && additionalRules.Count == 1)
                rules.Add(name, new Rule(name, new ISymbol[] { new NonTerminal(additionalRules[0]) }));
            else if (symbols.Count == 0)
                rules.Add(name, new Rule(name, new ISymbol[] { new OneOf(false, additionalRules.ToArray()) }));
            else
            {
                for (var i = 0; i < symbols.Count; i++)
                {
                    var symbol = symbols[i];
                    var ruleName = name + '|' + i;
                    rules.Add(ruleName, new Rule(ruleName, new[] { symbol }));
                    additionalRules.Add(ruleName);
                }

                rules.Add(name, new Rule(name, new ISymbol[] { new OneOf(false, additionalRules.ToArray()) }));
            }
        }

        private void ExtractAllSubsets(ISet<char> chSet, List<string> symbolNames)
        {
            ExtractSubsetIfPossible(chSet, symbolNames, CocolExtractor.ANY_LETTER_OR_DIGIT_SET, "$$any_letter_or_digit",
                () => new AnyLetterOrDigitTerminal());
            ExtractSubsetIfPossible(chSet, symbolNames, CocolExtractor.ANY_LETTER_SET, "$$any_letter",
                () => new AnyLetterTerminal());
            ExtractSubsetIfPossible(chSet, symbolNames, CocolExtractor.ANY_DIGIT_SET, "$$any_digit",
                () => new AnyDigitTerminal());
            ExtractSubsetIfPossible(chSet, symbolNames, CocolExtractor.ANY_UPPERCASE_SET, "$$any_uppercase",
                () => new AnyUppercaseLetterTerminal());
            ExtractSubsetIfPossible(chSet, symbolNames, CocolExtractor.ANY_LOWERCASE_SET, "$$any_lowercase",
                () => new AnyLowercaseLetterTerminal());
            ExtractSubsetIfPossible(chSet, symbolNames, ImmutableHashSet.Create('\n'), "$$any_lineEnd",
                () => new AnyLineEndTerminal());
            ExtractSubsetIfPossible(chSet, symbolNames, ImmutableHashSet.Create('\r'), "$$any_lineEnd",
                () => new AnyLineEndTerminal());
            ExtractSubsetIfPossible(chSet, symbolNames, CocolExtractor.ANY_WHITESPACE_SET, "$$any_whitespace",
                () => new AnyWhitespaceTerminal());
        }

        private void ExtractSubsetIfPossible(ISet<char> chSet, IList<string> symbolNames, ImmutableHashSet<char> subset,
            string name, Func<ISymbol> createSymbol)
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
            HEADING,
            GRAMMAR
        }
    }
}
