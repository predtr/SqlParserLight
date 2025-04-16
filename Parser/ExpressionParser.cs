using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SqlParserLib.AST;
using SqlParserLib.Core;
using SqlParserLib.Exceptions;

namespace SqlParserLib.Parser
{
    /// <summary>
    /// Parser for SQL expressions
    /// </summary>
    public class ExpressionParser
    {
        private readonly HashSet<string> _functionKeywords;

        /// <summary>
        /// Creates a new ExpressionParser with the given function keywords
        /// </summary>
        public ExpressionParser(HashSet<string> functionKeywords)
        {
            _functionKeywords = functionKeywords ?? throw new ArgumentNullException(nameof(functionKeywords));
        }

        /// <summary>
        /// Main entry point for parsing expressions - used by tests
        /// </summary>
        public SqlExpression Parse(ParserContext context)
        {
            return ParseExpression(context);
        }

        /// <summary>
        /// Parses a single column or expression in a SELECT list
        /// </summary>
        public SqlColumn ParseColumnOrExpression(ParserContext context)
        {
            // Start with a new column object
            var column = new SqlColumn();

            try
            {
                // Check specifically for CASE as a special column type
                if (context.Current().Type == SqlTokenType.KEYWORD &&
                    context.Current().Literal != null &&
                    (SqlKeyword)context.Current().Literal == SqlKeyword.CASE)
                {
                    return ParseCaseExpression(context);
                }

                // Parse as a general expression first - handles all cases including functions and concatenation
                SqlExpression expr = ParseExpression(context);

                // If it's a simple column reference, convert to a column
                if (expr.Type == ExpressionType.ColumnReference && expr.ReferencedColumns.Count == 1)
                {
                    var colRef = expr.ReferencedColumns[0];
                    if (!string.IsNullOrEmpty(colRef.TableName))
                    {
                        column.TableName = colRef.TableName;
                        column.Name = colRef.ColumnName;
                    }
                    else
                    {
                        column.Name = colRef.ColumnName;
                    }
                }
                else
                {
                    // It's a more complex expression
                    column.Expression = expr;
                    column.Name = DetermineExpressionName(expr);
                }

                // Check for AS keyword for alias
                if (context.MatchKeyword(SqlKeyword.AS))
                {
                    // AS keyword found, next token should be the alias
                    if (context.Current().Type == SqlTokenType.IDENTIFIER ||
                        context.Current().Type == SqlTokenType.STRING)
                    {
                        column.Alias = context.Consume().Lexeme;
                        // Remove quotes if present
                        if (column.Alias.StartsWith("'") && column.Alias.EndsWith("'"))
                        {
                            column.Alias = column.Alias.Substring(1, column.Alias.Length - 2);
                        }
                    }
                    else
                    {
                        throw new ParseException($"Expected alias name after AS keyword at line {context.Current().Line}");
                    }
                }
                // Check for implicit alias (no AS keyword)
                else if (context.Current().Type == SqlTokenType.IDENTIFIER)
                {
                    column.Alias = context.Consume().Lexeme;
                }
            }
            catch (ParseException ex)
            {
                throw; // Just rethrow without wrapping
            }

            return column;
        }

        /// <summary>
        /// Determines a suitable name for an expression
        /// </summary>
        private string DetermineExpressionName(SqlExpression expr)
        {
            if (expr == null) return "Expression";

            switch (expr.Type)
            {
                case ExpressionType.FunctionCall:
                    return expr.FunctionName + "Expression";
                case ExpressionType.Case:
                    return "CaseExpression";
                case ExpressionType.BinaryExpression:
                    if (expr.Operator == "+")
                        return "ConcatenationExpression";
                    else
                        return expr.Operator + "Expression";
                default:
                    return "Expression";
            }
        }

        /// <summary>
        /// Parses a CASE expression
        /// </summary>
        public SqlColumn ParseCaseExpression(ParserContext context)
        {
            // First parse the CASE expression itself
            SqlExpression caseExpr = ParseNestedCaseExpression(context);

            // Create the column with this expression
            var column = new SqlColumn
            {
                Expression = caseExpr,
                Name = "CaseExpression"
            };

            // Check for AS keyword for alias
            if (context.MatchKeyword(SqlKeyword.AS))
            {
                // AS keyword found, next token should be the alias
                if (context.Current().Type == SqlTokenType.IDENTIFIER ||
                    context.Current().Type == SqlTokenType.STRING)
                {
                    column.Alias = context.Consume().Lexeme;
                    // Remove quotes if present
                    if (column.Alias.StartsWith("'") && column.Alias.EndsWith("'"))
                    {
                        column.Alias = column.Alias.Substring(1, column.Alias.Length - 2);
                    }
                }
                else
                {
                    throw new ParseException($"Expected alias name after AS keyword at line {context.Current().Line}");
                }
            }
            // Check for implicit alias (no AS keyword)
            else if (context.Current().Type == SqlTokenType.IDENTIFIER)
            {
                column.Alias = context.Consume().Lexeme;
            }

            return column;
        }

        /// <summary>
        /// Parses a nested CASE expression
        /// </summary>
        public SqlExpression ParseNestedCaseExpression(ParserContext context)
        {
            var caseExpression = new SqlExpression { Type = ExpressionType.Case };

            // Expect CASE keyword
            context.ExpectKeyword(SqlKeyword.CASE);

            // Check if this is a simple CASE expression (with a value to compare)
            if (context.Current().Type != SqlTokenType.KEYWORD ||
                context.Current().Literal == null ||
                (SqlKeyword)context.Current().Literal != SqlKeyword.WHEN)
            {
                // This is a simple CASE expression with a value
                caseExpression.CaseValue = ParseExpression(context);
            }

            // Parse WHEN clauses
            while (context.MatchKeyword(SqlKeyword.WHEN))
            {
                var whenClause = new SqlCaseWhen();

                // Parse the WHEN condition expression
                whenClause.WhenExpression = ParseExpression(context);

                // Expect THEN
                context.ExpectKeyword(SqlKeyword.THEN);

                // Parse the THEN result expression
                whenClause.ThenExpression = ParseExpression(context);

                // Add the WHEN clause
                caseExpression.WhenClauses.Add(whenClause);

                // Add referenced columns from the WHEN and THEN expressions
                TransferReferencesAndParameters(caseExpression, whenClause.WhenExpression);
                TransferReferencesAndParameters(caseExpression, whenClause.ThenExpression);
            }

            // Parse optional ELSE clause
            if (context.MatchKeyword(SqlKeyword.ELSE))
            {
                caseExpression.ElseExpression = ParseExpression(context);
                TransferReferencesAndParameters(caseExpression, caseExpression.ElseExpression);
            }

            // Expect END
            context.ExpectKeyword(SqlKeyword.END);

            return caseExpression;
        }

        /// <summary>
        /// Checks if the current position looks like the start of a function call
        /// </summary>
        public bool IsLikelyFunction(ParserContext context)
        {
            // Save current position
            int savePosition = context.CurrentPosition;
            bool isFunction = false;

            try
            {
                // Check for identifier/keyword followed by left paren
                if ((context.Current().Type == SqlTokenType.IDENTIFIER || context.Current().Type == SqlTokenType.KEYWORD) &&
                    context.LookAhead(1).Type == SqlTokenType.LEFT_PAREN)
                {
                    // Check if it's a known function name
                    string potentialFunction = context.Current().Lexeme;

                    isFunction = _functionKeywords.Contains(potentialFunction);
                }
            }
            finally
            {
                // Restore position
                context.SetPosition(savePosition);
            }

            return isFunction;
        }

        /// <summary>
        /// Peeks ahead to get the next column expression
        /// </summary>
        public string PeekNextColumnExpression(ParserContext context)
        {
            int savePosition = context.CurrentPosition;
            StringBuilder sb = new StringBuilder();
            int parenLevel = 0;
            bool inCase = false;
            int caseLevel = 0;

            try
            {
                bool foundEndMarker = false;

                while (!context.IsAtEnd() && !foundEndMarker)
                {
                    SqlToken token = context.Current();

                    // Track parentheses nesting
                    if (token.Type == SqlTokenType.LEFT_PAREN)
                    {
                        parenLevel++;
                    }
                    else if (token.Type == SqlTokenType.RIGHT_PAREN)
                    {
                        parenLevel--;
                    }
                    // Track CASE...END nesting
                    else if (token.Type == SqlTokenType.KEYWORD && token.Literal != null)
                    {
                        SqlKeyword keyword = (SqlKeyword)token.Literal;
                        if (keyword == SqlKeyword.CASE)
                        {
                            inCase = true;
                            caseLevel++;
                        }
                        else if (keyword == SqlKeyword.END && inCase)
                        {
                            caseLevel--;
                            if (caseLevel == 0) inCase = false;
                        }
                        // If we see a FROM keyword outside of any nesting, stop
                        else if (keyword == SqlKeyword.FROM && parenLevel == 0 && caseLevel == 0)
                        {
                            foundEndMarker = true;
                            break; // Don't include FROM in the expression
                        }
                    }

                    // Check for comma outside nesting (end of column)
                    if (token.Type == SqlTokenType.COMMA &&
                        parenLevel == 0 && caseLevel == 0)
                    {
                        foundEndMarker = true;
                        break;
                    }

                    // Append the token
                    sb.Append(token.Lexeme).Append(" ");

                    // Move to next token without affecting the actual parser position
                    context.PeekConsume();
                }

                return sb.ToString().Trim();
            }
            finally
            {
                // Reset the position
                context.SetPosition(savePosition);
            }
        }

        /// <summary>
        /// Parses a SQL expression
        /// </summary>
        public SqlExpression ParseExpression(ParserContext context)
        {
            // Start with additive expressions (handles string concatenation with +)
            SqlExpression left = ParseAdditiveExpression(context);

            // Look for logical operators (AND, OR)
            while (context.Current().Type == SqlTokenType.KEYWORD &&
                   context.Current().Literal != null &&
                  ((SqlKeyword)context.Current().Literal == SqlKeyword.AND ||
                   (SqlKeyword)context.Current().Literal == SqlKeyword.OR))
            {
                // Create a logical expression
                SqlExpression logical;
                if (left.Type == ExpressionType.LogicalExpression)
                {
                    logical = left;
                }
                else
                {
                    logical = new SqlExpression { Type = ExpressionType.LogicalExpression };
                    logical.Conditions.Add(left);

                    // Copy referenced columns and parameters
                    TransferReferencesAndParameters(logical, left);
                }

                // Add the operator
                logical.LogicalOperators.Add(context.Consume().Lexeme);

                // Parse the right side
                SqlExpression right = ParseAdditiveExpression(context);
                logical.Conditions.Add(right);

                // Transfer references and parameters
                TransferReferencesAndParameters(logical, right);

                // Update left for the next iteration
                left = logical;
            }

            return left;
        }

        /// <summary>
        /// Parses an additive expression (involving + and - operators)
        /// </summary>
        private SqlExpression ParseAdditiveExpression(ParserContext context)
        {
            // Parse the first term
            SqlExpression left = ParseMultiplicativeExpression(context);

            // Look for + and - operators
            while (context.Current().Type == SqlTokenType.PLUS ||
                   context.Current().Type == SqlTokenType.MINUS)
            {
                bool isPlus = context.Current().Type == SqlTokenType.PLUS;

                // Create a binary expression
                SqlExpression binary = new SqlExpression { Type = ExpressionType.BinaryExpression };
                binary.Left = left;

                // Get the operator
                binary.Operator = isPlus ? "+" : "-";
                context.Consume();

                // Parse the right side
                binary.Right = ParseMultiplicativeExpression(context);

                // Transfer references and parameters
                TransferReferencesAndParameters(binary, binary.Left);
                TransferReferencesAndParameters(binary, binary.Right);

                // Update left for the next iteration
                left = binary;
            }

            return left;
        }

        /// <summary>
        /// Parses a multiplicative expression (involving * and / operators)
        /// </summary>
        private SqlExpression ParseMultiplicativeExpression(ParserContext context)
        {
            // Parse the first term
            SqlExpression left = ParseComparisonExpression(context);

            // Look for * and / operators
            while (context.Current().Type == SqlTokenType.STAR ||
                   (context.Current().Type == SqlTokenType.IDENTIFIER &&
                    context.Current().Lexeme.Equals("/", StringComparison.OrdinalIgnoreCase)))
            {
                // Create a binary expression
                SqlExpression binary = new SqlExpression { Type = ExpressionType.BinaryExpression };
                binary.Left = left;

                // Get the operator
                binary.Operator = context.Current().Type == SqlTokenType.STAR ? "*" : "/";
                context.Consume();

                // Parse the right side
                binary.Right = ParseComparisonExpression(context);

                // Transfer references and parameters
                TransferReferencesAndParameters(binary, binary.Left);
                TransferReferencesAndParameters(binary, binary.Right);

                // Update left for the next iteration
                left = binary;
            }

            return left;
        }

        /// <summary>
        /// Parses a comparison expression (involving =, <>, >, <, etc.)
        /// </summary>
        private SqlExpression ParseComparisonExpression(ParserContext context)
        {
            // Parse the first operand
            SqlExpression left = ParsePrimaryExpression(context);

            // Look for comparison operators
            if (context.Current().Type == SqlTokenType.EQUAL ||
                context.Current().Type == SqlTokenType.NOT_EQUAL ||
                context.Current().Type == SqlTokenType.BANG_EQUAL ||
                context.Current().Type == SqlTokenType.LESS ||
                context.Current().Type == SqlTokenType.GREATER ||
                context.Current().Type == SqlTokenType.LESS_EQUAL ||
                context.Current().Type == SqlTokenType.GREATER_EQUAL)
            {
                // Create a binary expression
                SqlExpression binary = new SqlExpression { Type = ExpressionType.BinaryExpression };
                binary.Left = left;

                // Map the operator token to a string
                switch (context.Current().Type)
                {
                    case SqlTokenType.EQUAL: binary.Operator = "="; break;
                    case SqlTokenType.NOT_EQUAL:
                    case SqlTokenType.BANG_EQUAL: binary.Operator = "<>"; break;
                    case SqlTokenType.LESS: binary.Operator = "<"; break;
                    case SqlTokenType.GREATER: binary.Operator = ">"; break;
                    case SqlTokenType.LESS_EQUAL: binary.Operator = "<="; break;
                    case SqlTokenType.GREATER_EQUAL: binary.Operator = ">="; break;
                }

                context.Consume();

                // Parse the right operand
                binary.Right = ParsePrimaryExpression(context);

                // Transfer references and parameters
                TransferReferencesAndParameters(binary, binary.Left);
                TransferReferencesAndParameters(binary, binary.Right);

                return binary;
            }

            // Check for IS NULL / IS NOT NULL
            if (context.MatchKeyword(SqlKeyword.IS))
            {
                SqlExpression isExpr = new SqlExpression
                {
                    Type = ExpressionType.IsNullExpression,
                    Left = left,
                    IsNullCheck = true
                };

                // Check for NOT NULL
                if (context.MatchKeyword(SqlKeyword.NOT))
                {
                    isExpr.IsNull = false;
                    if (!context.MatchKeyword(SqlKeyword.NULL))
                        throw new ParseException($"Expected NULL after IS NOT at line {context.Current().Line}");
                }
                else if (context.MatchKeyword(SqlKeyword.NULL))
                {
                    isExpr.IsNull = true;
                }
                else
                {
                    throw new ParseException($"Expected NULL or NOT NULL after IS at line {context.Current().Line}");
                }

                // Transfer references and parameters
                TransferReferencesAndParameters(isExpr, left);

                return isExpr;
            }

            return left;
        }

        /// <summary>
        /// Parses a primary expression (literal, column reference, function call, etc.)
        /// </summary>
        private SqlExpression ParsePrimaryExpression(ParserContext context)
        {
            var expression = new SqlExpression();

            // Check for parameter
            if (context.Current().Type == SqlTokenType.PARAMETER)
            {
                string paramName = context.Current().Lexeme;
                expression.Type = ExpressionType.Parameter;
                expression.ParameterName = paramName;
                expression.Tokens.Add(context.Consume());
                expression.Parameters.Add(paramName);
                return expression;
            }
            // Check for string literal
            else if (context.Current().Type == SqlTokenType.STRING)
            {
                expression.Type = ExpressionType.Literal;
                expression.LiteralValue = context.Current().Literal;
                expression.LiteralType = "string";
                expression.Tokens.Add(context.Consume());
                return expression;
            }
            // Check for number literal
            else if (context.Current().Type == SqlTokenType.NUMBER)
            {
                expression.Type = ExpressionType.Literal;
                expression.LiteralValue = context.Current().Literal;
                expression.LiteralType = "number";
                expression.Tokens.Add(context.Consume());
                return expression;
            }
            // Check for functions and column references
            else if (context.Current().Type == SqlTokenType.IDENTIFIER ||
                     context.Current().Type == SqlTokenType.KEYWORD)
            {
                // Check for CASE keyword - handle separately
                if (context.Current().Type == SqlTokenType.KEYWORD &&
                    context.Current().Literal != null &&
                    (SqlKeyword)context.Current().Literal == SqlKeyword.CASE)
                {
                    return ParseNestedCaseExpression(context);
                }

                // Check if it's a function call
                if (context.LookAhead(1).Type == SqlTokenType.LEFT_PAREN)
                {
                    return ParseFunctionCallExpression(context);
                }

                // Check if it's a qualified column (table.column)
                if (context.Current().Type == SqlTokenType.IDENTIFIER &&
                    context.LookAhead(1).Type == SqlTokenType.DOT &&
                    context.LookAhead(2).Type == SqlTokenType.IDENTIFIER)
                {
                    string tableName = context.Consume().Lexeme;
                    context.Consume(); // Consume dot
                    string columnName = context.Consume().Lexeme;

                    expression.Type = ExpressionType.ColumnReference;

                    var colRef = new ColumnReference { TableName = tableName, ColumnName = columnName };
                    expression.ReferencedColumns.Add(colRef);

                    return expression;
                }

                // Special handling for keyword NULL
                if (context.Current().Type == SqlTokenType.KEYWORD &&
                    context.Current().Literal != null &&
                    (SqlKeyword)context.Current().Literal == SqlKeyword.NULL)
                {
                    expression.Type = ExpressionType.Literal;
                    expression.LiteralValue = null;
                    expression.LiteralType = "null";
                    expression.Tokens.Add(context.Consume());
                    return expression;
                }

                // If it's a simple identifier (column name)
                if (context.Current().Type == SqlTokenType.IDENTIFIER)
                {
                    SqlToken identToken = context.Consume();
                    expression.Type = ExpressionType.ColumnReference;

                    var colRef = new ColumnReference { ColumnName = identToken.Lexeme };
                    expression.ReferencedColumns.Add(colRef);

                    return expression;
                }

                // Other keywords - just consume
                expression.Type = ExpressionType.Generic;
                expression.Tokens.Add(context.Consume());
            }
            // Handle parenthesized expressions
            else if (context.Current().Type == SqlTokenType.LEFT_PAREN)
            {
                context.Consume(); // Consume the opening parenthesis
                expression = ParseExpression(context);
                context.Expect(SqlTokenType.RIGHT_PAREN); // Consume the closing parenthesis
            }
            else
            {
                // Other token types
                expression.Type = ExpressionType.Generic;
                expression.Tokens.Add(context.Consume());
            }

            return expression;
        }

        /// <summary>
        /// Parses a function call expression
        /// </summary>
        private SqlExpression ParseFunctionCallExpression(ParserContext context)
        {
            var expression = new SqlExpression { Type = ExpressionType.FunctionCall };

            // Get function name (could be a keyword or identifier)
            SqlToken functionToken = context.Current();
            expression.FunctionName = functionToken.Lexeme;
            context.Consume(); // Consume the function name

            // Expect opening parenthesis
            context.Expect(SqlTokenType.LEFT_PAREN);

            // Special handling for empty argument list
            if (context.Current().Type == SqlTokenType.RIGHT_PAREN)
            {
                context.Consume(); // Consume the closing parenthesis
                return expression;
            }

            // Parse arguments
            bool first = true;
            do
            {
                if (!first) context.Expect(SqlTokenType.COMMA);
                first = false;

                // Parse argument expression
                SqlExpression arg = ParseExpression(context);
                expression.Arguments.Add(arg);

                // Collect referenced columns and parameters
                TransferReferencesAndParameters(expression, arg);
            }
            while (context.Current().Type == SqlTokenType.COMMA);

            // Expect closing parenthesis
            context.Expect(SqlTokenType.RIGHT_PAREN);

            return expression;
        }

        /// <summary>
        /// Helper method to transfer column references and parameters between expressions
        /// </summary>
        private void TransferReferencesAndParameters(SqlExpression target, SqlExpression source)
        {
            if (source == null) return;

            // Transfer column references
            foreach (var colRef in source.ReferencedColumns)
            {
                if (!target.ReferencedColumns.Any(c =>
                    string.Equals(c.TableName, colRef.TableName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(c.ColumnName, colRef.ColumnName, StringComparison.OrdinalIgnoreCase)))
                {
                    target.ReferencedColumns.Add(colRef);
                }
            }

            // Transfer parameters
            foreach (var param in source.Parameters)
            {
                if (!target.Parameters.Contains(param))
                {
                    target.Parameters.Add(param);
                }
            }
        }
    }
}