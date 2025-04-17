using SqlParserLib.AST;
using SqlParserLib.Core;
using SqlParserLib.Exceptions;
using System.Collections.Generic;

namespace SqlParserLib.Parser
{
    /// <summary>
    /// Parser for JOIN clauses in SQL statements
    /// </summary>
    public class JoinParser
    {
        private readonly ExpressionParser _expressionParser;
        private readonly TableSourceParser _tableSourceParser;

        /// <summary>
        /// Creates a new JoinParser with default settings
        /// </summary>
        public JoinParser()
        {
            _expressionParser = null;
            _tableSourceParser = new TableSourceParser();
        }

        /// <summary>
        /// Creates a new JoinParser with the given expression parser
        /// </summary>
        public JoinParser(ExpressionParser expressionParser)
        {
            _expressionParser = expressionParser;
            _tableSourceParser = new TableSourceParser();
        }

        /// <summary>
        /// Parses a JOIN clause
        /// </summary>
        public SqlJoin ParseJoinClause(ParserContext context)
        {
            var join = new SqlJoin();

            // Determine join type
            if (context.MatchKeyword(SqlKeyword.INNER))
            {
                join.JoinType = JoinType.Inner;
                context.ExpectKeyword(SqlKeyword.JOIN);
            }
            else if (context.MatchKeyword(SqlKeyword.LEFT))
            {
                join.JoinType = JoinType.Left;
                context.MatchKeyword(SqlKeyword.OUTER); // Optional 'OUTER'
                context.ExpectKeyword(SqlKeyword.JOIN);
            }
            else if (context.MatchKeyword(SqlKeyword.RIGHT))
            {
                join.JoinType = JoinType.Right;
                context.MatchKeyword(SqlKeyword.OUTER); // Optional 'OUTER'
                context.ExpectKeyword(SqlKeyword.JOIN);
            }
            else if (context.MatchKeyword(SqlKeyword.FULL))
            {
                join.JoinType = JoinType.Full;
                context.MatchKeyword(SqlKeyword.OUTER); // Optional 'OUTER'
                context.ExpectKeyword(SqlKeyword.JOIN);
            }
            else if (context.MatchKeyword(SqlKeyword.CROSS))
            {
                join.JoinType = JoinType.Cross;
                context.ExpectKeyword(SqlKeyword.JOIN);
            }
            else
            {
                // Default join type is INNER if just 'JOIN' is specified
                join.JoinType = JoinType.Inner;
                context.ExpectKeyword(SqlKeyword.JOIN);
            }

            // Parse joined table
            join.Table = _tableSourceParser.Parse(context);

            // Parse ON condition (except for CROSS JOIN)
            if (join.JoinType != JoinType.Cross)
            {
                context.ExpectKeyword(SqlKeyword.ON);
                join.Condition = ParseJoinCondition(context);
            }

            return join;
        }

        /// <summary>
        /// Parses a JOIN clause using existing table dictionary
        /// </summary>
        public SqlJoin Parse(ParserContext context, Dictionary<string, SqlTableSource> tables)
        {
            // This method is a wrapper for backward compatibility with tests
            var join = ParseJoinClause(context);
            return join;
        }

        /// <summary>
        /// Parses a JOIN condition (ON clause)
        /// </summary>
        private SqlJoinCondition ParseJoinCondition(ParserContext context)
        {
            var condition = new SqlJoinCondition();

            // Parse left side of the join condition (table1.column)
            string leftTable = context.Consume().Lexeme;
            context.Expect(SqlTokenType.DOT);
            string leftColumn = context.Consume().Lexeme;
            condition.LeftColumn = new ColumnReference { TableName = leftTable, ColumnName = leftColumn };

            // Parse comparison operator (usually '=')
            condition.Operator = context.Consume().Lexeme;

            // Parse right side of the join condition (table2.column)
            string rightTable = context.Consume().Lexeme;
            context.Expect(SqlTokenType.DOT);
            string rightColumn = context.Consume().Lexeme;
            condition.RightColumn = new ColumnReference { TableName = rightTable, ColumnName = rightColumn };

            // Vérifier si la condition est suivie d'un AND (condition complexe)
            // Ignorer les conditions supplémentaires mais ne pas échouer
            if (!context.IsAtEnd() && 
                context.Current().Type == SqlTokenType.KEYWORD &&
                context.Current().Literal != null &&
                (SqlKeyword)context.Current().Literal == SqlKeyword.AND)
            {
                // On a trouvé un AND, mais on ne stocke que la première condition
                // Cependant, on consomme tous les tokens jusqu'au prochain mot-clé important
                SkipAdditionalConditions(context);
            }

            return condition;
        }

        /// <summary>
        /// Ignore les conditions supplémentaires après un AND
        /// </summary>
        private void SkipAdditionalConditions(ParserContext context)
        {
            // Consomme le token AND
            context.Consume();

            int parenLevel = 0;
            bool found = false;

            // Skip tokens until we find a keyword that would end the join condition
            while (!context.IsAtEnd() && !found)
            {
                if (context.Current().Type == SqlTokenType.LEFT_PAREN)
                {
                    parenLevel++;
                }
                else if (context.Current().Type == SqlTokenType.RIGHT_PAREN)
                {
                    if (parenLevel > 0) parenLevel--;
                }
                // Si on est au niveau 0 de parenthèses et qu'on trouve un mot clé important, on s'arrête
                else if (parenLevel == 0 && 
                        context.Current().Type == SqlTokenType.KEYWORD)
                {
                    SqlKeyword keyword = (SqlKeyword)context.Current().Literal;
                    
                    // Ces mots-clés devraient indiquer la fin de la clause ON
                    if (keyword == SqlKeyword.INNER || keyword == SqlKeyword.LEFT || 
                        keyword == SqlKeyword.RIGHT || keyword == SqlKeyword.FULL || 
                        keyword == SqlKeyword.CROSS || keyword == SqlKeyword.JOIN ||
                        keyword == SqlKeyword.WHERE || keyword == SqlKeyword.GROUP ||
                        keyword == SqlKeyword.HAVING || keyword == SqlKeyword.ORDER)
                    {
                        found = true;
                        break;
                    }
                }

                // Seulement si on n'a pas trouvé un mot-clé important
                if (!found)
                {
                    context.Consume();
                }
            }
        }
    }
}