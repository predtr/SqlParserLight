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

            // Check if it's a subquery in parentheses
            if (context.Current().Type == SqlTokenType.LEFT_PAREN)
            {
                // Handle subquery as a derived table
                context.Consume(); // Consume the left parenthesis
                
                // Skip through the subquery content until we find the closing parenthesis
                int parenthesisLevel = 1;
                while (!context.IsAtEnd() && parenthesisLevel > 0)
                {
                    if (context.Current().Type == SqlTokenType.LEFT_PAREN)
                    {
                        parenthesisLevel++;
                    }
                    else if (context.Current().Type == SqlTokenType.RIGHT_PAREN)
                    {
                        parenthesisLevel--;
                    }
                    
                    // If we found the matching closing parenthesis, stop
                    if (parenthesisLevel == 0)
                    {
                        break;
                    }
                    
                    // Otherwise, consume the token and continue
                    context.Consume();
                }
                
                // Consume the right parenthesis
                if (!context.IsAtEnd())
                {
                    context.Consume();
                }
                
                // For subqueries, we use a placeholder table name
                table.TableName = "DerivedTable";
                
                // Parse the alias which is required for derived tables
                if (context.MatchKeyword(SqlKeyword.AS))
                {
                    table.Alias = context.Consume().Lexeme;
                }
                else if (context.Current().Type == SqlTokenType.IDENTIFIER)
                {
                    table.Alias = context.Consume().Lexeme;
                }
                
                return table;
            }

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