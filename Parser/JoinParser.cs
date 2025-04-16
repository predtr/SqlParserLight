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

            return condition;
        }
    }
}