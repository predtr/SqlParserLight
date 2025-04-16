using SqlParserLib.AST;
using SqlParserLib.Core;

namespace SqlParserLib.Parser
{
    /// <summary>
    /// Parser for table sources in SQL statements
    /// </summary>
    public class TableSourceParser
    {
        /// <summary>
        /// Parses a table source (main table or joined table)
        /// </summary>
        public SqlTableSource Parse(ParserContext context)
        {
            var table = new SqlTableSource();

            // Parse table name which might include schema
            string firstPart = context.Consume().Lexeme;

            // Keep track of full name in case of schema qualification
            string fullName = firstPart;

            // Check if it's a schema-qualified name (schema.table)
            if (context.Match(SqlTokenType.DOT))
            {
                // Consume second part
                string secondPart = context.Consume().Lexeme;
                fullName += "." + secondPart;

                // Check for three-part name (db.schema.table)
                if (context.Match(SqlTokenType.DOT))
                {
                    string thirdPart = context.Consume().Lexeme;
                    fullName += "." + thirdPart;
                }
            }

            // Set the full name as the table name as expected by tests
            table.TableName = fullName;

            // Check for table alias
            if (context.MatchKeyword(SqlKeyword.AS))
            {
                table.Alias = context.Consume().Lexeme;
            }
            else if (context.Current().Type == SqlTokenType.IDENTIFIER && !context.IsJoinKeyword())
            {
                table.Alias = context.Consume().Lexeme;
            }

            return table;
        }
    }
}