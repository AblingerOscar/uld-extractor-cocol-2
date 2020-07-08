using autosupport_lsp_server;
using autosupport_lsp_server.Symbols;
using autosupport_lsp_server.Symbols.Impl;
using autosupport_lsp_server.Symbols.Impl.Terminals;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace DefinitionFileBuillder
{
    public class DefinitionFileBuilder
    {
        private bool nextCommentIsAction = false;
        private StringBuilder currentComment = new StringBuilder();
        private CommentParserState commentParserState = CommentParserState.Heading;

        private string languageId;
        private string languageFilePattern;
        private IList<CommentRule> documentationComments = new List<CommentRule>();
        private IList<CommentRule> normalComments = new List<CommentRule>();
        private IList<string> startRules = new List<string>();
        private Dictionary<string, IRule> rules = new Dictionary<string, IRule>();

        public IList<string> Errors = new List<string>();

        public DefinitionFileBuilder()
        {

        }

        public IAutosupportLanguageDefinition Build()
        {
            CommentIsFinished();

            return new AutosupportLanguageDefinition(
                    languageId, languageFilePattern, commentRules: new CommentRules(normalComments.ToArray(), documentationComments.ToArray()), startRules.ToArray(), rules
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

        public void AddRule(string ruleName, ISymbol[] symbols)
        {
            rules.Add(ruleName, new Rule(ruleName, symbols));
        }

        public void AddCharacterSetRule(string name, ISet<char> chSet)
        {
            ISymbol[] symbols;

            if (chSet.SetEquals(CocolExtractor.ANY_CHARACTER_SET))
                symbols = new[] { new AnyCharacterTerminal() };
            else
            {
                var symbolNames = new List<string>();

                ExtractAllSubsets(chSet, symbolNames);

                foreach (var ch in chSet)
                {
                    var chRuleName = "$$character_" + ch;

                    symbolNames.Add(chRuleName);
                    if (!rules.ContainsKey(chRuleName))
                        rules.Add(chRuleName, new Rule(chRuleName, new[] { new StringTerminal(ch.ToString()) }));
                }

                if (symbolNames.Count == 1)
                    symbols = new[] { new NonTerminal(symbolNames[0]) };
                else
                    symbols = new[] { new OneOf(false, symbolNames.ToArray()) };
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
            ExtractSubsetIfPossible(chSet, symbolNames, ImmutableHashSet.Create('\n'), "$$any_lineend", () => new AnyLineEndTerminal());
            ExtractSubsetIfPossible(chSet, symbolNames, ImmutableHashSet.Create('\r'), "$$any_lineend", () => new AnyLineEndTerminal());
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
