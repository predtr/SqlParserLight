using System;
using System.Collections.Generic;
using System.Linq;
using SqlParserLib.AST;
using SqlParserLib.Core;
using SqlParserLib.Exceptions;

namespace SqlParserLib.Parser
{
    /// <summary>
    /// A specialized parser for SQL SELECT statements
    /// </summary>
    public class SelectStatementParser
    {
        private readonly ExpressionParser _expressionParser;
        private readonly JoinParser _joinParser;
        private readonly TableSourceParser _tableSourceParser;
        
        /// <summary>
        /// Creates a new SelectStatementParser with the given function keywords
        /// </summary>
        public SelectStatementParser(HashSet<string> functionKeywords)
        {
            _expressionParser = new ExpressionParser(functionKeywords);
            _joinParser = new JoinParser(_expressionParser);
            _tableSourceParser = new TableSourceParser();
        }

        /// <summary>
        /// Parses a SELECT statement from the token stream
        /// </summary>
        public SqlStatement Parse(ParserContext context, List<string> skippedExpressions)
        {
            var statement = new SqlStatement();

            // Expect "SELECT" keyword
            context.ExpectKeyword(SqlKeyword.SELECT);

            // Parse columns in SELECT clause
            statement.Columns = ParseColumns(context, skippedExpressions);

            // Check for missing or error in FROM clause
            if (context.IsAtEnd())
            {
                throw new ParseException("Unexpected end of SQL. Expected FROM clause.", context.Current().Line);
            }

            // Try to recover if we're not at FROM and might have skipped complex expressions
            if (context.Current().Type != SqlTokenType.KEYWORD || 
                context.Current().Literal == null || 
                (SqlKeyword)context.Current().Literal != SqlKeyword.FROM)
            {
                // Try to skip to FROM keyword
                SkipToFromKeyword(context);
            }

            // Now expect the FROM keyword
            context.ExpectKeyword(SqlKeyword.FROM);

            // Parse main table
            statement.MainTable = _tableSourceParser.Parse(context);

            // Parse JOIN clauses if present
            while (!context.IsAtEnd() && context.IsJoinKeyword())
            {
                statement.Joins.Add(_joinParser.ParseJoinClause(context));
            }

            // Parse WHERE clause if present
            if (context.MatchKeyword(SqlKeyword.WHERE))
            {
                statement.WhereCondition = _expressionParser.ParseExpression(context);

                // Extract any parameters from the WHERE condition
                foreach (var param in statement.WhereCondition.Parameters)
                {
                    if (!statement.Parameters.Contains(param))
                    {
                        statement.Parameters.Add(param);
                    }
                }
            }

            // Connect columns to their tables
            ResolveColumnTables(statement);

            // Build join hierarchy tree
            BuildJoinHierarchy(statement);

            return statement;
        }

        /// <summary>
        /// Parses the column list in a SELECT statement
        /// </summary>
        private List<SqlColumn> ParseColumns(ParserContext context, List<string> skippedExpressions)
        {
            var columns = new List<SqlColumn>();
            bool expectComma = false;

            // Loop until we find FROM or end of input
            while (!context.IsAtEnd())
            {
                // Check for FROM clause - this signals the end of column list
                if (context.Current().Type == SqlTokenType.KEYWORD &&
                    (SqlKeyword)context.Current().Literal == SqlKeyword.FROM)
                {
                    break;
                }

                // Handle comma separators between columns
                if (expectComma)
                {
                    if (!context.Match(SqlTokenType.COMMA))
                    {
                        // No comma where expected - must be syntax error or end of column list
                        break;
                    }
                    expectComma = false;
                }

                // Store starting position for error recovery
                int startPos = context.CurrentPosition;
                string originalExpression = "";

                try
                {
                    // Try to capture the original expression for better error reporting
                    originalExpression = _expressionParser.PeekNextColumnExpression(context);

                    // Attempt to parse the column
                    SqlColumn column = _expressionParser.ParseColumnOrExpression(context);
                    columns.Add(column);

                    // Next token should be a comma or FROM
                    expectComma = true;
                }
                catch (ParseException ex)
                {
                    // On error, capture the skipped expression
                    string skippedText = originalExpression;
                    if (string.IsNullOrEmpty(skippedText))
                    {
                        skippedText = context.GetTokensBetween(startPos, context.CurrentPosition);
                    }

                    skippedExpressions.Add(skippedText);

                    Console.WriteLine($"Warning: Skipped unparseable column expression: {skippedText}");
                    Console.WriteLine($"Error details: {ex.Message}");

                    // Skip until comma or FROM
                    SkipResult skipResult = SkipToNextColumnOrFrom(context);

                    // Add placeholder column for the skipped part
                    columns.Add(new SqlColumn
                    {
                        Name = $"UnparseableExpression_{skippedExpressions.Count}",
                        Expression = new SqlExpression
                        {
                            Type = ExpressionType.Unparseable,
                            UnparseableText = skippedText
                        }
                    });

                    // Handle result of skipping
                    if (skipResult == SkipResult.FoundFrom)
                    {
                        // We found FROM, so we're done with columns
                        break;
                    }
                    else if (skipResult == SkipResult.FoundComma)
                    {
                        // We found and consumed a comma, so expect next column
                        expectComma = false;
                    }
                    else // SkipResult.EndOfInput
                    {
                        // We hit the end, so stop parsing columns
                        break;
                    }
                }
            }

            return columns;
        }

        /// <summary>
        /// Result of skipping to next delimiter
        /// </summary>
        private enum SkipResult
        {
            FoundComma,
            FoundFrom,
            EndOfInput
        }

        /// <summary>
        /// Skips tokens until the next column or FROM clause
        /// </summary>
        private SkipResult SkipToNextColumnOrFrom(ParserContext context)
        {
            int parenLevel = 0;
            int caseLevel = 0;

            // Skip tokens until we find a comma (next column) or FROM clause
            while (!context.IsAtEnd())
            {
                // Track balanced parentheses and CASE...END blocks
                if (context.Current().Type == SqlTokenType.LEFT_PAREN)
                {
                    parenLevel++;
                }
                else if (context.Current().Type == SqlTokenType.RIGHT_PAREN)
                {
                    if (parenLevel > 0) parenLevel--;
                }
                else if (context.Current().Type == SqlTokenType.KEYWORD)
                {
                    SqlKeyword keyword = (SqlKeyword)context.Current().Literal;

                    if (keyword == SqlKeyword.CASE)
                    {
                        caseLevel++;
                    }
                    else if (keyword == SqlKeyword.END && caseLevel > 0)
                    {
                        caseLevel--;
                    }
                    else if (keyword == SqlKeyword.FROM && parenLevel == 0 && caseLevel == 0)
                    {
                        // Found FROM clause outside of any parentheses or CASE blocks
                        return SkipResult.FoundFrom;
                    }
                }

                // Only consider commas outside of parentheses and CASE blocks
                if (context.Current().Type == SqlTokenType.COMMA &&
                    parenLevel == 0 && caseLevel == 0)
                {
                    // Found comma - consume it
                    context.Consume();
                    return SkipResult.FoundComma;
                }

                // Consume the current token and continue
                context.Consume();
            }

            // If we reach the end of input
            return SkipResult.EndOfInput;
        }

        /// <summary>
        /// Resolves column tables in a SQL statement
        /// </summary>
        private void ResolveColumnTables(SqlStatement statement)
        {
            // Dictionary to map table aliases to their actual table sources
            var tableMap = new Dictionary<string, SqlTableSource>(StringComparer.OrdinalIgnoreCase);

            // Add main table to the map
            if (statement.MainTable != null)
            {
                tableMap[statement.MainTable.TableName] = statement.MainTable;
                tableMap[statement.MainTable.GetFullName()] = statement.MainTable;
                if (!string.IsNullOrEmpty(statement.MainTable.Alias))
                {
                    tableMap[statement.MainTable.Alias] = statement.MainTable;
                }
            }

            // Add joined tables to the map
            foreach (var join in statement.Joins)
            {
                tableMap[join.Table.TableName] = join.Table;
                tableMap[join.Table.GetFullName()] = join.Table;
                if (!string.IsNullOrEmpty(join.Table.Alias))
                {
                    tableMap[join.Table.Alias] = join.Table;
                }
            }

            // Collect all parameters from columns
            foreach (var column in statement.Columns)
            {
                if (column.IsExpression)
                {
                    foreach (var param in column.Expression.Parameters)
                    {
                        if (!statement.Parameters.Contains(param))
                        {
                            statement.Parameters.Add(param);
                        }
                    }
                }
            }

            // Resolve column tables
            foreach (var column in statement.Columns)
            {
                if (column.IsExpression)
                {
                    // This is an expression
                    // Resolve each referenced column
                    foreach (var colRef in column.Expression.ReferencedColumns)
                    {
                        if (!string.IsNullOrEmpty(colRef.TableName))
                        {
                            if (tableMap.TryGetValue(colRef.TableName, out var table))
                            {
                                // Found the table for this column reference
                                if (column.Table == null)
                                {
                                    // Set the first referenced table as the column's table
                                    column.Table = table;
                                }
                            }
                        }
                        else if (statement.MainTable != null)
                        {
                            // Column without explicit table name - assume main table
                            colRef.TableName = statement.MainTable.GetEffectiveName();
                        }
                    }

                    // If no table was set from referenced columns, use main table
                    if (column.Table == null && statement.MainTable != null)
                    {
                        column.Table = statement.MainTable;
                    }
                }
                else
                {
                    // Regular column
                    // If column already has a table name, find that table
                    if (!string.IsNullOrEmpty(column.TableName))
                    {
                        if (tableMap.TryGetValue(column.TableName, out var table))
                        {
                            column.Table = table;
                        }
                    }
                    else if (statement.MainTable != null)
                    {
                        // Column doesn't have explicit table name, try to find it
                        // For simplicity, we'll just assume it belongs to the main table
                        column.Table = statement.MainTable;
                    }
                }
            }

            // Also collect parameters from WHERE condition
            if (statement.WhereCondition != null)
            {
                foreach (var param in statement.WhereCondition.Parameters)
                {
                    if (!statement.Parameters.Contains(param))
                    {
                        statement.Parameters.Add(param);
                    }
                }
            }
        }

        /// <summary>
        /// Builds a join hierarchy graph for the SQL statement
        /// </summary>
        private void BuildJoinHierarchy(SqlStatement statement)
        {
            var joinGraph = new Dictionary<string, List<JoinEdge>>();

            // Add nodes for all tables
            joinGraph[statement.MainTable.GetEffectiveName()] = new List<JoinEdge>();

            foreach (var join in statement.Joins)
            {
                string tableName = join.Table.GetEffectiveName();
                if (string.IsNullOrEmpty(tableName))
                {
                    throw new ParseException("Join table name cannot be null or empty.", -1);
                }

                if (!joinGraph.ContainsKey(tableName))
                {
                    joinGraph[tableName] = new List<JoinEdge>();
                }

                // Extract table names from join condition
                string leftTable = join.Condition.LeftColumn.TableName;
                string rightTable = join.Condition.RightColumn.TableName;

                // Skip join conditions with invalid table names
                if (string.IsNullOrEmpty(leftTable) || string.IsNullOrEmpty(rightTable))
                {
                    Console.WriteLine($"Warning: Skipping join condition with invalid table names: Left='{leftTable}', Right='{rightTable}'");
                    continue;
                }

                // Resolve actual table names from aliases if needed
                leftTable = ResolveTableAlias(statement, leftTable);
                rightTable = ResolveTableAlias(statement, rightTable);

                // Ensure both tables exist in the graph
                if (!joinGraph.ContainsKey(leftTable))
                {
                    joinGraph[leftTable] = new List<JoinEdge>();
                }

                if (!joinGraph.ContainsKey(rightTable))
                {
                    joinGraph[rightTable] = new List<JoinEdge>();
                }

                // Add bidirectional edges for the join
                joinGraph[leftTable].Add(new JoinEdge
                {
                    SourceTable = leftTable,
                    TargetTable = rightTable,
                    Join = join
                });

                joinGraph[rightTable].Add(new JoinEdge
                {
                    SourceTable = rightTable,
                    TargetTable = leftTable,
                    Join = join
                });
            }

            // Store the join graph in the statement
            statement.JoinGraph = joinGraph;
        }

        /// <summary>
        /// Resolves a table alias to its actual table name
        /// </summary>
        private string ResolveTableAlias(SqlStatement statement, string tableNameOrAlias)
        {
            // Check if it matches the main table
            if (string.Equals(tableNameOrAlias, statement.MainTable.TableName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tableNameOrAlias, statement.MainTable.GetFullName(), StringComparison.OrdinalIgnoreCase) ||
                string.Equals(tableNameOrAlias, statement.MainTable.Alias, StringComparison.OrdinalIgnoreCase))
            {
                return statement.MainTable.GetEffectiveName();
            }

            // Check if it matches any joined table
            foreach (var join in statement.Joins)
            {
                if (string.Equals(tableNameOrAlias, join.Table.TableName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(tableNameOrAlias, join.Table.GetFullName(), StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(tableNameOrAlias, join.Table.Alias, StringComparison.OrdinalIgnoreCase))
                {
                    return join.Table.GetEffectiveName();
                }
            }

            // Couldn't resolve, return as is
            return tableNameOrAlias;
        }

        /// <summary>
        /// Attempts to skip to the FROM keyword
        /// </summary>
        private void SkipToFromKeyword(ParserContext context)
        {
            int parenLevel = 0;
            bool found = false;

            // Skip tokens until we find the FROM keyword outside of any parentheses
            while (!context.IsAtEnd() && !found)
            {
                // Track balanced parentheses
                if (context.Current().Type == SqlTokenType.LEFT_PAREN)
                {
                    parenLevel++;
                }
                else if (context.Current().Type == SqlTokenType.RIGHT_PAREN)
                {
                    if (parenLevel > 0) parenLevel--;
                }
                // Check for FROM but only if we're not inside parentheses
                else if (parenLevel == 0 && 
                         context.Current().Type == SqlTokenType.KEYWORD &&
                         context.Current().Literal != null &&
                         (SqlKeyword)context.Current().Literal == SqlKeyword.FROM)
                {
                    found = true;
                    break;
                }

                // Move to next token if we haven't found FROM
                if (!found)
                {
                    context.Consume();
                }
            }
        }
    }
}