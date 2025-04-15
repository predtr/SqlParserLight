using System;
using System.Collections.Generic;

namespace SqlParserLib
{
	/// <summary>
	/// Represents a parsed SQL statement
	/// </summary>
	public class SqlStatement
	{
		public List<SqlColumn> Columns { get; set; } = new List<SqlColumn>();
		public SqlTableSource MainTable { get; set; }
		public List<SqlJoin> Joins { get; set; } = new List<SqlJoin>();
		public SqlExpression WhereCondition { get; set; }
		public List<string> Parameters { get; set; } = new List<string>(); // Track parameters used in the query

		// Join graph for representing the hierarchy
		public Dictionary<string, List<JoinEdge>> JoinGraph { get; set; }

		// Error tracking
		public bool HasError { get; set; }
		public string ErrorMessage { get; set; }

		/// <summary>
		/// Gets the join path from the main table to the specified target table
		/// </summary>
		public List<JoinPath> GetJoinPath(string targetTable)
		{
			var paths = new List<JoinPath>();

			// If the statement has error or no main table, return empty path
			if (HasError || MainTable == null || JoinGraph == null)
				return paths;

			var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var path = new List<JoinEdge>();

			// Start from the main table and find all paths to the target table
			FindPaths(MainTable.GetEffectiveName(), targetTable, visited, path, paths);

			return paths;
		}

		private void FindPaths(string currentTable, string targetTable,
							  HashSet<string> visited, List<JoinEdge> currentPath,
							  List<JoinPath> results)
		{
			// Mark current table as visited
			visited.Add(currentTable);

			// If we reached the target table, add the current path to results
			if (string.Equals(currentTable, targetTable, StringComparison.OrdinalIgnoreCase))
			{
				results.Add(new JoinPath { Edges = new List<JoinEdge>(currentPath) });
				visited.Remove(currentTable);
				return;
			}

			// Explore all connected tables
			if (JoinGraph.TryGetValue(currentTable, out var edges))
			{
				foreach (var edge in edges)
				{
					string nextTable = edge.TargetTable;

					if (!visited.Contains(nextTable))
					{
						currentPath.Add(edge);
						FindPaths(nextTable, targetTable, visited, currentPath, results);
						currentPath.RemoveAt(currentPath.Count - 1);
					}
				}
			}

			// Backtrack
			visited.Remove(currentTable);
		}
	}

	/// <summary>
	/// Represents a column or expression in a SELECT clause
	/// </summary>
	public class SqlColumn
	{
		public string Name { get; set; }
		public string TableName { get; set; } // Optional table qualifier
		public string Alias { get; set; } // Optional alias
		public SqlTableSource Table { get; set; } // The resolved table this column belongs to
		public SqlExpression Expression { get; set; } // For function calls or complex expressions

		// Indicates if this is a simple column reference or a complex expression
		public bool IsExpression => Expression != null;

		public override string ToString()
		{
			if (IsExpression)
			{
				string result = Expression.ToString();
				if (!string.IsNullOrEmpty(Alias))
				{
					result += $" AS {Alias}";
				}
				return result;
			}

			string columnRef = string.IsNullOrEmpty(TableName) ? Name : $"{TableName}.{Name}";
			if (!string.IsNullOrEmpty(Alias))
			{
				columnRef += $" AS {Alias}";
			}
			return columnRef;
		}
	}

	/// <summary>
	/// Represents a table source (in FROM clause or JOIN)
	/// </summary>
	public class SqlTableSource
	{
		public string SchemaName { get; set; } // Schema name (e.g., "dbo")
		public string TableName { get; set; }  // Table name
		public string Alias { get; set; }      // Optional alias

		// Full name combines schema and table if schema is specified
		public string GetFullName()
		{
			return string.IsNullOrEmpty(SchemaName) ? TableName : $"{SchemaName}.{TableName}";
		}

		public string GetEffectiveName()
		{
			return !string.IsNullOrEmpty(Alias) ? Alias : GetFullName();
		}

		public override string ToString()
		{
			string tableName = GetFullName();
			return !string.IsNullOrEmpty(Alias) ? $"{tableName} AS {Alias}" : tableName;
		}
	}

	/// <summary>
	/// Represents a JOIN clause
	/// </summary>
	public class SqlJoin
	{
		public JoinType JoinType { get; set; }
		public SqlTableSource Table { get; set; }
		public SqlJoinCondition Condition { get; set; }

		public override string ToString()
		{
			string joinTypeStr = JoinType.ToString();

			if (JoinType == JoinType.INNER)
			{
				joinTypeStr = "INNER JOIN";
			}
			else if (JoinType == JoinType.LEFT)
			{
				joinTypeStr = "LEFT JOIN";
			}
			else if (JoinType == JoinType.RIGHT)
			{
				joinTypeStr = "RIGHT JOIN";
			}
			else if (JoinType == JoinType.FULL)
			{
				joinTypeStr = "FULL JOIN";
			}
			else if (JoinType == JoinType.CROSS)
			{
				joinTypeStr = "CROSS JOIN";
			}

			string result = $"{joinTypeStr} {Table}";

			if (Condition != null)
			{
				result += $" ON {Condition}";
			}

			return result;
		}
	}

	/// <summary>
	/// Represents join condition in the ON clause
	/// </summary>
	public class SqlJoinCondition
	{
		public ColumnReference LeftColumn { get; set; }
		public string Operator { get; set; } // Usually "="
		public ColumnReference RightColumn { get; set; }

		public override string ToString()
		{
			return $"{LeftColumn.TableName}.{LeftColumn.ColumnName} {Operator} {RightColumn.TableName}.{RightColumn.ColumnName}";
		}
	}

	/// <summary>
	/// Represents an expression (e.g., in WHERE clause or as part of SELECT)
	/// </summary>
	public class SqlExpression
	{
		public List<SqlToken> Tokens { get; set; } = new List<SqlToken>();
		public ExpressionType Type { get; set; } = ExpressionType.Generic;
		public string FunctionName { get; set; } // For function calls
		public List<SqlExpression> Arguments { get; set; } = new List<SqlExpression>(); // For function arguments
		public List<ColumnReference> ReferencedColumns { get; set; } = new List<ColumnReference>(); // For tracking column references
		public List<string> Parameters { get; set; } = new List<string>(); // For tracking parameters

		// For literals
		public object LiteralValue { get; set; } // The actual literal value
		public string LiteralType { get; set; } // Type of the literal (string, number, null, etc.)

		// For CASE expressions
		public SqlExpression CaseValue { get; set; } // For simple CASE expressions (optional)
		public List<SqlCaseWhen> WhenClauses { get; set; } = new List<SqlCaseWhen>(); // WHEN/THEN pairs
		public SqlExpression ElseExpression { get; set; } // ELSE result (optional)

		// For binary expressions (e.g., column = value)
		public SqlExpression Left { get; set; } // Left operand
		public string Operator { get; set; } // Operator (=, <>, >, etc.)
		public SqlExpression Right { get; set; } // Right operand

		// For logical expressions (AND/OR)
		public List<SqlExpression> Conditions { get; set; } = new List<SqlExpression>(); // Conditions joined by AND/OR
		public List<string> LogicalOperators { get; set; } = new List<string>(); // The operators (AND/OR)

		// For IS NULL / IS NOT NULL expressions
		public bool IsNull { get; set; } // True for IS NULL, false for IS NOT NULL
		public bool IsNullCheck { get; set; } // Indicates if this is an IS NULL / IS NOT NULL check

		// For parameter expressions
		public string ParameterName { get; set; } // For parameter references (e.g., @param)

		// For unparseable expressions
		public string UnparseableText { get; set; } // Original text of unparseable expression

		public override string ToString()
		{
			if (Type == ExpressionType.Unparseable)
			{
				return $"{{UNPARSEABLE: {UnparseableText}}}";
			}
			else if (Type == ExpressionType.Parameter)
			{
				return ParameterName;
			}
			else if (Type == ExpressionType.Literal)
			{
				if (LiteralType == "string")
				{
					return $"'{LiteralValue}'"; // Return string literals in quotes
				}
				else if (LiteralValue == null)
				{
					return "NULL";
				}
				return LiteralValue?.ToString() ?? "NULL";
			}
			else if (Type == ExpressionType.FunctionCall)
			{
				string args = string.Join(", ", Arguments.ConvertAll(arg => arg.ToString()));
				return $"{FunctionName}({args})";
			}
			else if (Type == ExpressionType.Case)
			{
				var result = new System.Text.StringBuilder();
				result.Append("CASE ");

				// Add case value for simple CASE
				if (CaseValue != null)
				{
					result.Append(CaseValue.ToString());
					result.Append(" ");
				}

				// Add all WHEN/THEN clauses
				foreach (var whenClause in WhenClauses)
				{
					result.Append("WHEN ");
					result.Append(whenClause.WhenExpression.ToString());
					result.Append(" THEN ");
					result.Append(whenClause.ThenExpression.ToString());
					result.Append(" ");
				}

				// Add ELSE clause if present
				if (ElseExpression != null)
				{
					result.Append("ELSE ");
					result.Append(ElseExpression.ToString());
					result.Append(" ");
				}

				result.Append("END");
				return result.ToString();
			}
			else if (Type == ExpressionType.BinaryExpression)
			{
				// For string concatenation, format it specially
				if (Operator == "+")
				{
					// Check if either operand is a string literal
					bool isStringConcat = false;

					// If left is a string literal or right is a string literal
					if ((Left?.Type == ExpressionType.Literal && Left?.LiteralType == "string") ||
						(Right?.Type == ExpressionType.Literal && Right?.LiteralType == "string"))
					{
						isStringConcat = true;
					}

					// Or if left or right is a concatenation with string literals
					if (Left?.Type == ExpressionType.BinaryExpression && Left?.Operator == "+" &&
						IsStringConcatenation(Left))
					{
						isStringConcat = true;
					}

					if (Right?.Type == ExpressionType.BinaryExpression && Right?.Operator == "+" &&
						IsStringConcatenation(Right))
					{
						isStringConcat = true;
					}

					if (isStringConcat)
					{
						return $"{Left} + {Right}";
					}
				}

				// For other binary expressions
				return $"{Left} {Operator} {Right}";
			}
			else if (Type == ExpressionType.LogicalExpression)
			{
				// Format the logical expression with AND/OR operators
				var result = new System.Text.StringBuilder();
				for (int i = 0; i < Conditions.Count; i++)
				{
					if (i > 0)
					{
						result.Append(" ");
						result.Append(LogicalOperators[i - 1]);
						result.Append(" ");
					}

					// Add parentheses around binary expressions for clarity
					if (Conditions[i].Type == ExpressionType.BinaryExpression ||
						Conditions[i].Type == ExpressionType.LogicalExpression)
					{
						result.Append("(");
						result.Append(Conditions[i].ToString());
						result.Append(")");
					}
					else
					{
						result.Append(Conditions[i].ToString());
					}
				}

				return result.ToString();
			}
			else if (Type == ExpressionType.IsNullExpression)
			{
				return $"{Left} IS {(IsNull ? "NULL" : "NOT NULL")}";
			}
			else if (Type == ExpressionType.ColumnReference && ReferencedColumns.Count > 0)
			{
				var colRef = ReferencedColumns[0];
				if (!string.IsNullOrEmpty(colRef.TableName))
				{
					return $"{colRef.TableName}.{colRef.ColumnName}";
				}
				else
				{
					return colRef.ColumnName;
				}
			}

			return string.Join(" ", Tokens.ConvertAll(t => t.Lexeme));
		}

		/// <summary>
		/// Determines if the expression is part of a string concatenation
		/// </summary>
		private bool IsStringConcatenation(SqlExpression expr)
		{
			if (expr == null) return false;

			if (expr.Type == ExpressionType.Literal && expr.LiteralType == "string")
			{
				return true;
			}

			if (expr.Type == ExpressionType.BinaryExpression && expr.Operator == "+")
			{
				return IsStringConcatenation(expr.Left) || IsStringConcatenation(expr.Right);
			}

			return false;
		}
	}

	/// <summary>
	/// Represents a WHEN/THEN pair in a CASE expression
	/// </summary>
	public class SqlCaseWhen
	{
		public SqlExpression WhenExpression { get; set; } // The WHEN condition
		public SqlExpression ThenExpression { get; set; } // The THEN result

		public override string ToString()
		{
			return $"WHEN {WhenExpression} THEN {ThenExpression}";
		}
	}

	/// <summary>
	/// Types of expressions
	/// </summary>
	public enum ExpressionType
	{
		Generic,
		ColumnReference,
		Literal,
		FunctionCall,
		Operator,
		Case,
		BinaryExpression,
		LogicalExpression,
		IsNullExpression,
		Unparseable,
		Parameter
	}

	/// <summary>
	/// Represents a reference to a column in a table
	/// </summary>
	public class ColumnReference
	{
		public string TableName { get; set; }
		public string ColumnName { get; set; }

		public override string ToString()
		{
			return $"{TableName}.{ColumnName}";
		}
	}

	/// <summary>
	/// Represents a join edge in the join graph
	/// </summary>
	public class JoinEdge
	{
		public string SourceTable { get; set; }
		public string TargetTable { get; set; }
		public SqlJoin Join { get; set; }

		public override string ToString()
		{
			return $"{SourceTable} -> {TargetTable}";
		}
	}

	/// <summary>
	/// Represents a path of joins between tables
	/// </summary>
	public class JoinPath
	{
		public List<JoinEdge> Edges { get; set; } = new List<JoinEdge>();

		public override string ToString()
		{
			return string.Join(" -> ", Edges);
		}
	}

	/// <summary>
	/// Enum representing SQL join types
	/// </summary>
	public enum JoinType
	{
		INNER,
		LEFT,
		RIGHT,
		FULL,
		CROSS
	}
}