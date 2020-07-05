using autosupport_lsp_server;
using autosupport_lsp_server.Symbols;
using autosupport_lsp_server.Symbols.Impl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DefinitionFileBuillder
{
    public class DefinitionFileBuilder
    {
        private bool nextCommentIsAction = false;
        private StringBuilder currentComment = new StringBuilder();

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
            // TODO
            Console.WriteLine($"found finished {(nextCommentIsAction ? "action" : "non-action")} comment: <{currentComment}>");

            currentComment.Clear();
        }

        public void StartOfRules()
        {
            CommentIsFinished();
        }

        public void AddRule(string ruleName, ISymbol[] symbols)
        {
            rules.Add(ruleName, new Rule(ruleName, symbols));
        }
    }
}
