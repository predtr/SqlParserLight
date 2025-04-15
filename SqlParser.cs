using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SqlParserLib
{
	/// <summary>
	/// Main SQL Parser class that orchestrates the parsing process
	/// </summary>
	public class SqlParser
	{
		private readonly SqlLexer _lexer;
		private List<string> _skippedExpressions = new List<string>();
		private HashSet<string> _functionKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			"ISNULL", "COALESCE", "DATEDIFF", "DATEADD", "CONVERT", "CAST", "COUNT",
			"SUM", "AVG", "MIN", "MAX", "SUBSTRING", "CONCAT", "REPLACE", "GETDATE",
			"UPPER", "LOWER", "TRIM", "LTRIM", "RTRIM", "ROUND", "CEILING", "FLOOR",
			"NULLIF", "STUFF", "YEAR", "MONTH", "DAY", "STRING_AGG"
		};

		/// <summary>
		/// Gets any expressions that were skipped during parsing
		/// </summary>
		public List<string> SkippedExpressions => _skippedExpressions;

		public SqlParser()
		{
			_lexer = new SqlLexer();
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
			return ParseSelectStatement(context);
		}

		/// <summary>
		/// Finds a column or alias in the SQL statement and generates a path from its table to the main table
		/// </summary>
		/// <param name="statement">The parsed SQL statement</param>
		/// <param name="dataFieldName">The column or alias name to search for</param>
		/// <param name="mainTable">Optional main table name to validate against</param>
		/// <returns>A result containing whether the field was found and its path</returns>
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

		private SqlStatement ParseSelectStatement(ParserContext context)
		{
			var statement = new SqlStatement();

			// Expect "SELECT" keyword
			context.ExpectKeyword(SqlKeyword.SELECT);

			// Parse columns in SELECT clause
			statement.Columns = ParseColumns(context);

			// Expect "FROM" keyword
			context.ExpectKeyword(SqlKeyword.FROM);

			// Parse main table
			statement.MainTable = ParseTableSource(context);

			// Parse JOIN clauses if present
			while (!context.IsAtEnd() && context.IsJoinKeyword())
			{
				statement.Joins.Add(ParseJoinClause(context));
			}

			// Parse WHERE clause if present
			if (context.MatchKeyword(SqlKeyword.WHERE))
			{
				statement.WhereCondition = ParseExpression(context);

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

		private List<SqlColumn> ParseColumns(ParserContext context)
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
					originalExpression = PeekNextColumnExpression(context);

					// Check for common function keywords that might not be recognized as keywords
					bool isLikelyFunction = IsLikelyFunction(context);

					// Attempt to parse the column
					SqlColumn column = ParseColumnOrExpression(context);
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

					_skippedExpressions.Add(skippedText);

					Console.WriteLine($"Warning: Skipped unparseable column expression: {skippedText}");
					Console.WriteLine($"Error details: {ex.Message}");

					// Skip until comma or FROM
					SkipResult skipResult = SkipToNextColumnOrFrom(context);

					// Add placeholder column for the skipped part
					columns.Add(new SqlColumn
					{
						Name = $"UnparseableExpression_{_skippedExpressions.Count}",
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
		/// Checks if the current position looks like the start of a function call
		/// </summary>
		private bool IsLikelyFunction(ParserContext context)
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
		/// Result of skipping to next delimiter
		/// </summary>
		private enum SkipResult
		{
			FoundComma,
			FoundFrom,
			EndOfInput
		}

		private string PeekNextColumnExpression(ParserContext context)
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
					else if (token.Type == SqlTokenType.KEYWORD)
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

		private SkipResult SkipToNextColumnOrFrom(ParserContext context)
		{
			int parenLevel = 0;
			int caseLevel = 0;
			bool foundDelimiter = false;

			// Skip tokens until we find a comma (next column) or FROM clause
			while (!context.IsAtEnd() && !foundDelimiter)
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
		/// Parses a column or expression in the SELECT list
		/// </summary>
		private SqlColumn ParseColumnOrExpression(ParserContext context)
		{
			// Start with a new column object
			var column = new SqlColumn();

			try
			{
				// Check specifically for CASE as a special column type
				if (context.Current().Type == SqlTokenType.KEYWORD &&
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
				throw ex; // Rethrow to be handled by the caller
			}

			return column;
		}

		/// <summary>
		/// Determines a suitable name for an expression based on its type
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
		/// Parse a function expression, especially handling ISNULL, COALESCE, etc.
		/// </summary>
		private SqlExpression ParseFunctionExpression(ParserContext context)
		{
			var expression = new SqlExpression { Type = ExpressionType.FunctionCall };

			// Get function name (could be a keyword or identifier)
			string funcName = context.Current().Lexeme;
			expression.FunctionName = funcName;
			context.Consume();

			// Expect opening parenthesis
			context.Expect(SqlTokenType.LEFT_PAREN);

			// Parse arguments
			do
			{
				// Handle empty argument list or trailing comma
				if (context.Current().Type == SqlTokenType.RIGHT_PAREN)
					break;

				var arg = ParseExpressionArgument(context);
				expression.Arguments.Add(arg);

				// Collect referenced columns and parameters
				TransferReferencesAndParameters(expression, arg);
			}
			while (context.Match(SqlTokenType.COMMA));

			// Expect closing parenthesis
			context.Expect(SqlTokenType.RIGHT_PAREN);

			return expression;
		}

		private bool IsComparisonOperator(SqlTokenType tokenType)
		{
			return tokenType == SqlTokenType.EQUAL ||
				   tokenType == SqlTokenType.NOT_EQUAL ||
				   tokenType == SqlTokenType.GREATER ||
				   tokenType == SqlTokenType.GREATER_EQUAL ||
				   tokenType == SqlTokenType.LESS ||
				   tokenType == SqlTokenType.LESS_EQUAL;
		}

		private SqlColumn ParseCaseExpression(ParserContext context)
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

		private SqlColumn ParseFunctionCall(ParserContext context)
		{
			var column = new SqlColumn();
			var expression = new SqlExpression { Type = ExpressionType.FunctionCall };

			// Get function name
			SqlToken functionToken = context.Consume();
			expression.FunctionName = functionToken.Lexeme;
			column.Name = expression.FunctionName; // Use function name as column name

			// Special handling for DATEDIFF which has a specific format
			bool isDatediff = functionToken.Type == SqlTokenType.KEYWORD &&
							 (SqlKeyword)functionToken.Literal == SqlKeyword.DATEDIFF;

			// Expect opening parenthesis
			context.Expect(SqlTokenType.LEFT_PAREN);

			if (isDatediff)
			{
				// Handle DATEDIFF(datepart, startdate, enddate)

				// Parse datepart argument (e.g., DAY, MONTH, YEAR, etc.)
				var datepartArg = ParseDatePartArgument(context);
				expression.Arguments.Add(datepartArg);

				// Expect comma
				context.Expect(SqlTokenType.COMMA);

				// Parse start date
				var startDateArg = ParseExpressionArgument(context);
				expression.Arguments.Add(startDateArg);

				// Add referenced columns
				foreach (var colRef in startDateArg.ReferencedColumns)
				{
					if (!expression.ReferencedColumns.Any(c =>
						c.TableName == colRef.TableName && c.ColumnName == colRef.ColumnName))
					{
						expression.ReferencedColumns.Add(colRef);
					}
				}

				// Expect comma
				context.Expect(SqlTokenType.COMMA);

				// Parse end date
				var endDateArg = ParseExpressionArgument(context);
				expression.Arguments.Add(endDateArg);

				// Add referenced columns
				foreach (var colRef in endDateArg.ReferencedColumns)
				{
					if (!expression.ReferencedColumns.Any(c =>
						c.TableName == colRef.TableName && c.ColumnName == colRef.ColumnName))
					{
						expression.ReferencedColumns.Add(colRef);
					}
				}
			}
			else
			{
				// Handle regular functions
				// Parse arguments
				do
				{
					// Parse argument expression
					var arg = ParseExpressionArgument(context);
					expression.Arguments.Add(arg);

					// If we find columns in the arguments, track them
					foreach (var colRef in arg.ReferencedColumns)
					{
						if (!expression.ReferencedColumns.Any(c =>
							c.TableName == colRef.TableName && c.ColumnName == colRef.ColumnName))
						{
							expression.ReferencedColumns.Add(colRef);
						}
					}
				}
				while (context.Match(SqlTokenType.COMMA));
			}

			// Expect closing parenthesis
			context.Expect(SqlTokenType.RIGHT_PAREN);

			// Check for column alias
			if (context.MatchKeyword(SqlKeyword.AS))
			{
				column.Alias = context.Consume().Lexeme;
			}
			else if (context.Current().Type == SqlTokenType.IDENTIFIER)
			{
				column.Alias = context.Consume().Lexeme;
			}

			column.Expression = expression;
			return column;
		}

		private SqlExpression ParseDatePartArgument(ParserContext context)
		{
			var expression = new SqlExpression { Type = ExpressionType.Literal };

			// Check if it's a datepart keyword
			if (context.Current().Type == SqlTokenType.KEYWORD)
			{
				SqlKeyword keyword = (SqlKeyword)context.Current().Literal;

				if (keyword == SqlKeyword.DAY ||
					keyword == SqlKeyword.MONTH ||
					keyword == SqlKeyword.YEAR ||
					keyword == SqlKeyword.HOUR ||
					keyword == SqlKeyword.MINUTE ||
					keyword == SqlKeyword.SECOND)
				{
					expression.Tokens.Add(context.Consume());
					return expression;
				}
			}

			// If not a datepart keyword, treat as a regular expression
			return ParseExpressionArgument(context);
		}

		/// <summary>
		/// Parses a nested function expression (like CONVERT(...))
		/// </summary>
		private SqlExpression ParseNestedFunctionExpression(ParserContext context)
		{
			var expression = new SqlExpression { Type = ExpressionType.FunctionCall };

			// Get function name
			expression.FunctionName = context.Consume().Lexeme;

			// Expect opening parenthesis
			context.Expect(SqlTokenType.LEFT_PAREN);

			// Parse arguments
			do
			{
				// Handle empty argument list or trailing comma
				if (context.Current().Type == SqlTokenType.RIGHT_PAREN)
					break;

				var arg = ParseExpressionArgument(context);
				expression.Arguments.Add(arg);

				// Collect referenced columns
				foreach (var colRef in arg.ReferencedColumns)
				{
					if (!expression.ReferencedColumns.Any(c =>
						c.TableName == colRef.TableName && c.ColumnName == colRef.ColumnName))
					{
						expression.ReferencedColumns.Add(colRef);
					}
				}

				// Collect parameters
				foreach (var param in arg.Parameters)
				{
					if (!expression.Parameters.Contains(param))
					{
						expression.Parameters.Add(param);
					}
				}
			}
			while (context.Match(SqlTokenType.COMMA));

			// Expect closing parenthesis
			context.Expect(SqlTokenType.RIGHT_PAREN);

			return expression;
		}

		private SqlColumn ParseColumn(ParserContext context)
		{
			var column = new SqlColumn();

			// Check if it's a qualified name (table.column)
			if (context.LookAhead(1).Type == SqlTokenType.DOT)
			{
				column.TableName = context.Consume().Lexeme;
				context.Consume(); // Consume the dot
				column.Name = context.Consume().Lexeme;
			}
			else
			{
				column.Name = context.Consume().Lexeme;
			}

			// Check for column alias
			if (context.MatchKeyword(SqlKeyword.AS))
			{
				column.Alias = context.Consume().Lexeme;
			}
			else if (context.Current().Type == SqlTokenType.IDENTIFIER &&
					 !context.IsNextTokenInSelectList())
			{
				column.Alias = context.Consume().Lexeme;
			}

			return column;
		}

		private SqlTableSource ParseTableSource(ParserContext context)
		{
			var table = new SqlTableSource();

			// Parse table name which might include schema
			string firstPart = context.Consume().Lexeme;

			// Check if it's a schema-qualified name (schema.table)
			if (context.Match(SqlTokenType.DOT))
			{
				// The first part was the schema name
				table.SchemaName = firstPart;
				// The second part is the table name
				table.TableName = context.Consume().Lexeme;
			}
			else
			{
				// No schema specified, just the table name
				table.TableName = firstPart;
			}

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

		private SqlJoin ParseJoinClause(ParserContext context)
		{
			var join = new SqlJoin();

			// Determine join type
			if (context.MatchKeyword(SqlKeyword.INNER))
			{
				join.JoinType = JoinType.INNER;
				context.ExpectKeyword(SqlKeyword.JOIN);
			}
			else if (context.MatchKeyword(SqlKeyword.LEFT))
			{
				join.JoinType = JoinType.LEFT;
				context.MatchKeyword(SqlKeyword.OUTER); // Optional 'OUTER'
				context.ExpectKeyword(SqlKeyword.JOIN);
			}
			else if (context.MatchKeyword(SqlKeyword.RIGHT))
			{
				join.JoinType = JoinType.RIGHT;
				context.MatchKeyword(SqlKeyword.OUTER); // Optional 'OUTER'
				context.ExpectKeyword(SqlKeyword.JOIN);
			}
			else if (context.MatchKeyword(SqlKeyword.FULL))
			{
				join.JoinType = JoinType.FULL;
				context.MatchKeyword(SqlKeyword.OUTER); // Optional 'OUTER'
				context.ExpectKeyword(SqlKeyword.JOIN);
			}
			else if (context.MatchKeyword(SqlKeyword.CROSS))
			{
				join.JoinType = JoinType.CROSS;
				context.ExpectKeyword(SqlKeyword.JOIN);
			}
			else
			{
				// Default join type is INNER
				join.JoinType = JoinType.INNER;
				context.ExpectKeyword(SqlKeyword.JOIN);
			}

			// Parse joined table
			join.Table = ParseTableSource(context);

			// Parse ON condition (except for CROSS JOIN)
			if (join.JoinType != JoinType.CROSS)
			{
				context.ExpectKeyword(SqlKeyword.ON);
				join.Condition = ParseJoinCondition(context);
			}

			return join;
		}

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

		private SqlExpression ParseExpression(ParserContext context)
		{
			// Start with additive expressions (handles string concatenation with +)
			SqlExpression left = ParseAdditiveExpression(context);

			// Look for logical operators (AND, OR)
			while (context.Current().Type == SqlTokenType.KEYWORD &&
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

				// Parse the right side - this handles nested expressions correctly
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

		/// <summary>
		/// Parses a term (factor in an expression)
		/// </summary>
		private SqlExpression ParseTerm(ParserContext context)
		{
			return ParsePrimaryExpression(context);
		}

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

		private void BuildJoinHierarchy(SqlStatement statement)
		{
			var joinGraph = new Dictionary<string, List<JoinEdge>>();

			// Add nodes for all tables
			joinGraph[statement.MainTable.GetEffectiveName()] = new List<JoinEdge>();

			foreach (var join in statement.Joins)
			{
				string tableName = join.Table.GetEffectiveName();
				if (!joinGraph.ContainsKey(tableName))
				{
					joinGraph[tableName] = new List<JoinEdge>();
				}

				// Extract table names from join condition
				string leftTable = join.Condition.LeftColumn.TableName;
				string rightTable = join.Condition.RightColumn.TableName;

				// Resolve actual table names from aliases if needed
				leftTable = ResolveTableAlias(statement, leftTable);
				rightTable = ResolveTableAlias(statement, rightTable);

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

		private SqlExpression ParseExpressionArgument(ParserContext context)
		{
			return ParseExpression(context);
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
					(SqlKeyword)context.Current().Literal == SqlKeyword.CASE)
				{
					return ParseNestedCaseExpression(context);  // Use ParseNestedCaseExpression here, not ParseCaseExpression
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
		/// Parses a function call with specific name like ISNULL
		/// </summary>
		private SqlExpression ParseNamedFunctionExpression(ParserContext context)
		{
			var expression = new SqlExpression { Type = ExpressionType.FunctionCall };

			// Get function name
			expression.FunctionName = context.Consume().Lexeme;

			// Expect opening parenthesis
			context.Expect(SqlTokenType.LEFT_PAREN);

			// Parse arguments
			do
			{
				// Handle empty argument list or trailing comma
				if (context.Current().Type == SqlTokenType.RIGHT_PAREN)
					break;

				var arg = ParseExpressionArgument(context);
				expression.Arguments.Add(arg);

				// Collect referenced columns and parameters
				TransferReferencesAndParameters(expression, arg);
			}
			while (context.Match(SqlTokenType.COMMA));

			// Expect closing parenthesis
			context.Expect(SqlTokenType.RIGHT_PAREN);

			return expression;
		}

		/// <summary>
		/// Parses a function call that starts with a keyword (like COALESCE, DATEDIFF)
		/// </summary>
		private SqlExpression ParseKeywordFunctionExpression(ParserContext context)
		{
			var expression = new SqlExpression { Type = ExpressionType.FunctionCall };

			// Get function name from keyword
			SqlToken functionToken = context.Consume();
			expression.FunctionName = functionToken.Lexeme;

			// Expect opening parenthesis
			context.Expect(SqlTokenType.LEFT_PAREN);

			// Parse arguments
			do
			{
				// Handle empty argument list or trailing comma
				if (context.Current().Type == SqlTokenType.RIGHT_PAREN)
					break;

				var arg = ParseExpressionArgument(context);
				expression.Arguments.Add(arg);

				// Collect referenced columns
				foreach (var colRef in arg.ReferencedColumns)
				{
					if (!expression.ReferencedColumns.Any(c =>
						c.TableName == colRef.TableName && c.ColumnName == colRef.ColumnName))
					{
						expression.ReferencedColumns.Add(colRef);
					}
				}

				// Collect parameters
				foreach (var param in arg.Parameters)
				{
					if (!expression.Parameters.Contains(param))
					{
						expression.Parameters.Add(param);
					}
				}
			}
			while (context.Match(SqlTokenType.COMMA));

			// Expect closing parenthesis
			context.Expect(SqlTokenType.RIGHT_PAREN);

			return expression;
		}

		private bool IsLogicalOperator(ParserContext context)
		{
			if (context.Current().Type != SqlTokenType.KEYWORD)
				return false;

			SqlKeyword keyword = (SqlKeyword)context.Current().Literal;
			return keyword == SqlKeyword.AND || keyword == SqlKeyword.OR;
		}

		// [Update the ParseNestedCaseExpression method to handle logical operators]
		private SqlExpression ParseNestedCaseExpression(ParserContext context)
		{
			var caseExpression = new SqlExpression { Type = ExpressionType.Case };

			// Expect CASE keyword
			context.ExpectKeyword(SqlKeyword.CASE);

			// Check if this is a simple CASE expression (with a value to compare)
			if (context.Current().Type != SqlTokenType.KEYWORD ||
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
	}

	/// <summary>
	/// Result class for column path search
	/// </summary>
	public class ColumnPathResult
	{
		public string DataFieldName { get; set; }
		public bool Found { get; set; }
		public bool IsColumn { get; set; }
		public bool IsAlias { get; set; }
		public bool IsParameter { get; set; }
		public string TableName { get; set; }
		public string DataTablePath { get; set; }

		public override string ToString()
		{
			if (!Found)
				return $"Field '{DataFieldName}' not found in SQL statement.";

			if (IsParameter)
				return $"Found parameter '{DataFieldName}'.";

			string type = IsColumn ? "column" : (IsAlias ? "alias" : "field");
			return $"Found {type} '{DataFieldName}' in table '{TableName}'. Path: {DataTablePath}";
		}
	}
}