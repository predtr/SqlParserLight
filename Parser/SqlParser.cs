using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SqlParserLib.AST;
using SqlParserLib.Core;
using SqlParserLib.Exceptions;
using SqlParserLib.Interfaces;
using SqlParserLib.Lexer;

namespace SqlParserLib.Parser
{
    /// <summary>
    /// Main SQL Parser class that orchestrates the parsing process
    /// </summary>
    public class SqlParser : ISqlParser
    {
        private readonly ISqlLexer _lexer;
        private List<string> _skippedExpressions = new List<string>();
        private readonly HashSet<string> _functionKeywords;

        /// <summary>
        /// Gets any expressions that were skipped during parsing
        /// </summary>
        public List<string> SkippedExpressions => _skippedExpressions;

        /// <summary>
        /// Initializes a new instance of the SqlParser class with the default lexer
        /// </summary>
        public SqlParser() : this(new SqlLexer())
        {
        }

        /// <summary>
        /// Initializes a new instance of the SqlParser class with a custom lexer
        /// </summary>
        public SqlParser(ISqlLexer lexer)
        {
            _lexer = lexer ?? throw new ArgumentNullException(nameof(lexer));
            
            // Initialize common function names for better recognition
            _functionKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "ISNULL", "COALESCE", "DATEDIFF", "DATEADD", "CONVERT", "CAST", "COUNT",
                "SUM", "AVG", "MIN", "MAX", "SUBSTRING", "CONCAT", "REPLACE", "GETDATE",
                "UPPER", "LOWER", "TRIM", "LTRIM", "RTRIM", "ROUND", "CEILING", "FLOOR",
                "NULLIF", "STUFF", "YEAR", "MONTH", "DAY", "STRING_AGG"
            };
        }

        /// <summary>
        /// Parse a SQL query and return a structured representation
        /// </summary>
        public SqlStatement Parse(string sql)
        {
            // Reset skipped expressions
            _skippedExpressions = new List<string>();

            // Tokenize the SQL statement
            List<SqlToken> tokens = _lexer.Tokenize(sql);

            // Create a new parser context to track the parsing state
            var context = new ParserContext(tokens);

            // Parse the SELECT statement
            var selectParser = new SelectStatementParser(_functionKeywords);
            return selectParser.Parse(context, _skippedExpressions);
        }

        /// <summary>
        /// Finds a column or alias in the SQL statement and generates a path from its table to the main table
        /// </summary>
        public ColumnPathResult GetColumnPath(SqlStatement statement, string dataFieldName, string mainTable = null)
        {
            // Check if main table matches
            if (mainTable != null && mainTable != statement.MainTable.TableName)
                return null;

            var result = new ColumnPathResult
            {
                DataFieldName = dataFieldName,
                Found = false
            };

            // Check if it's a parameter
            if (dataFieldName.StartsWith("@"))
            {
                if (statement.Parameters.Contains(dataFieldName))
                {
                    result.Found = true;
                    result.IsParameter = true;
                    return result;
                }
            }

            // First, check for direct column matches
            foreach (var column in statement.Columns)
            {
                // Skip unparseable expressions
                if (column.IsExpression && column.Expression.Type == ExpressionType.Unparseable)
                    continue;

                // Check column name
                if (string.Equals(column.Name, dataFieldName, StringComparison.OrdinalIgnoreCase))
                {
                    result.Found = true;
                    result.IsColumn = true;
                    result.TableName = column.Table?.GetEffectiveName();
                    break;
                }

                // Check column alias
                if (string.Equals(column.Alias, dataFieldName, StringComparison.OrdinalIgnoreCase))
                {
                    result.Found = true;
                    result.IsAlias = true;
                    result.TableName = column.Table?.GetEffectiveName();
                    break;
                }
            }

            // If not found as a direct column or alias, check inside expressions
            if (!result.Found)
            {
                foreach (var column in statement.Columns)
                {
                    // Skip unparseable expressions
                    if (column.IsExpression && column.Expression.Type == ExpressionType.Unparseable)
                        continue;

                    if (column.IsExpression)
                    {
                        // Check in referenced columns within expressions
                        foreach (var refCol in column.Expression.ReferencedColumns)
                        {
                            if (string.Equals(refCol.ColumnName, dataFieldName, StringComparison.OrdinalIgnoreCase))
                            {
                                result.Found = true;
                                result.IsColumn = true;
                                result.TableName = refCol.TableName;
                                break;
                            }
                        }

                        if (result.Found)
                            break;
                    }
                }
            }

            // If still not found, check join conditions
            if (!result.Found)
            {
                foreach (var join in statement.Joins)
                {
                    // Check left side of join condition
                    if (string.Equals(join.Condition.LeftColumn.ColumnName, dataFieldName, StringComparison.OrdinalIgnoreCase))
                    {
                        result.Found = true;
                        result.IsColumn = true;
                        result.TableName = join.Condition.LeftColumn.TableName;
                        break;
                    }

                    // Check right side of join condition
                    if (string.Equals(join.Condition.RightColumn.ColumnName, dataFieldName, StringComparison.OrdinalIgnoreCase))
                    {
                        result.Found = true;
                        result.IsColumn = true;
                        result.TableName = join.Condition.RightColumn.TableName;
                        break;
                    }
                }
            }

            // If found a match and it has a table, build the path
            if (result.Found && !string.IsNullOrEmpty(result.TableName))
            {
                // Get actual table by resolving potential aliases
                SqlTableSource tableSource = null;

                // Check if it's the main table
                if (string.Equals(result.TableName, statement.MainTable.GetEffectiveName(), StringComparison.OrdinalIgnoreCase))
                {
                    tableSource = statement.MainTable;
                }
                else
                {
                    // Check joined tables
                    foreach (var join in statement.Joins)
                    {
                        if (string.Equals(result.TableName, join.Table.GetEffectiveName(), StringComparison.OrdinalIgnoreCase))
                        {
                            tableSource = join.Table;
                            break;
                        }
                    }
                }

                // If it's not the main table, build path from main table to this table
                if (tableSource != null && !string.Equals(result.TableName, statement.MainTable.GetEffectiveName(), StringComparison.OrdinalIgnoreCase))
                {
                    var paths = statement.GetJoinPath(result.TableName);

                    if (paths.Count > 0)
                    {
                        // Use first available path
                        var path = paths[0];
                        var pathBuilder = new StringBuilder();

                        // Start with main table
                        pathBuilder.Append(statement.MainTable.TableName);

                        // Add each join in the path
                        foreach (var edge in path.Edges)
                        {
                            // Find the join condition for this edge
                            SqlJoinCondition joinCondition = null;

                            if (edge.Join != null && edge.Join.Condition != null)
                            {
                                joinCondition = edge.Join.Condition;
                            }

                            if (joinCondition != null)
                            {
                                // Find the column from the source table
                                string joinColumnName = null;

                                if (string.Equals(joinCondition.LeftColumn.TableName, edge.SourceTable, StringComparison.OrdinalIgnoreCase))
                                {
                                    joinColumnName = joinCondition.LeftColumn.ColumnName;
                                }
                                else if (string.Equals(joinCondition.RightColumn.TableName, edge.SourceTable, StringComparison.OrdinalIgnoreCase))
                                {
                                    joinColumnName = joinCondition.RightColumn.ColumnName;
                                }

                                if (joinColumnName != null)
                                {
                                    // Get target table name (without alias)
                                    string targetTableName = null;
                                    foreach (var joinEdge in statement.Joins)
                                    {
                                        if (string.Equals(joinEdge.Table.GetEffectiveName(), edge.TargetTable, StringComparison.OrdinalIgnoreCase))
                                        {
                                            targetTableName = joinEdge.Table.TableName;
                                            break;
                                        }
                                    }

                                    if (targetTableName == null) targetTableName = edge.TargetTable;

                                    // Add to path: .[JoinColumnName]TableName
                                    pathBuilder.Append($".[{joinColumnName}]{targetTableName}");
                                }
                            }
                        }

                        result.DataTablePath = pathBuilder.ToString();
                    }
                    else
                    {
                        // If no path found, just use table name
                        result.DataTablePath = result.TableName;
                    }
                }
                else
                {
                    // If it's the main table, just use the table name
                    result.DataTablePath = statement.MainTable.TableName;
                }
            }

            return result;
        }
    }
}