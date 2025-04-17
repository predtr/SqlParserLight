using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SqlParserLib.Core;
using SqlParserLib.Interfaces;

namespace SqlParserLib.AST
{
	/// <summary>
	/// Represents a parsed SQL statement and its components.
	/// </summary>
	public class SqlStatement : SqlAstNode
	{
		/// <summary>
		/// Gets or sets the columns in the SELECT clause.
		/// </summary>
		public List<SqlColumn> Columns { get; set; } = new List<SqlColumn>();
		
		/// <summary>
		/// Gets or sets the main table in the FROM clause.
		/// </summary>
		public SqlTableSource MainTable { get; set; }
		
		/// <summary>
		/// Gets or sets the joins in the query.
		/// </summary>
		public List<SqlJoin> Joins { get; set; } = new List<SqlJoin>();
		
		/// <summary>
		/// Gets or sets the WHERE condition.
		/// </summary>
		public SqlExpression WhereCondition { get; set; }
		
		/// <summary>
		/// Gets or sets the WHERE condition (alias for WhereCondition for backward compatibility)
		/// </summary>
		public SqlExpression WhereClause 
		{ 
			get => WhereCondition;
			set => WhereCondition = value;
		}
		
		/// <summary>
		/// Gets or sets the parameters used in the query.
		/// </summary>
		public List<string> Parameters { get; set; } = new List<string>();

		/// <summary>
		/// Gets or sets the join graph for representing the table relationships.
		/// </summary>
		public Dictionary<string, List<JoinEdge>> JoinGraph { get; set; }

		/// <summary>
		/// Gets or sets a value indicating whether the statement has a parsing error.
		/// </summary>
		public bool HasError { get; set; }
		
		/// <summary>
		/// Gets or sets the error message if parsing failed.
		/// </summary>
		public string ErrorMessage { get; set; }

		/// <summary>
		/// Accept method for implementing the visitor pattern
		/// </summary>
		public override T Accept<T>(ISqlVisitor<T> visitor)
		{
			return visitor.VisitStatement(this);
		}

		/// <summary>
		/// Gets a path from the main table to a target table using BFS algorithm
		/// </summary>
		/// <param name="targetTable">The target table name</param>
		/// <returns>A list of available paths</returns>
		public List<JoinPath> FindJoinPathsBfs(string targetTable)
		{
			if (JoinGraph == null || JoinGraph.Count == 0)
				return new List<JoinPath>();

			if (string.Equals(MainTable.GetEffectiveName(), targetTable, StringComparison.OrdinalIgnoreCase))
			{
				// Target is the main table, no path needed
				return new List<JoinPath> { new JoinPath() };
			}

			// Use breadth-first search to find paths from main table to target table
			var queue = new Queue<JoinPath>();
			var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var result = new List<JoinPath>();

			// Start from main table
			string startTable = MainTable.GetEffectiveName();
			visited.Add(startTable);

			// Initialize the queue with an empty path
			var initialPath = new JoinPath();
			initialPath.Tables.Add(startTable);
			queue.Enqueue(initialPath);

			while (queue.Count > 0)
			{
				var currentPath = queue.Dequeue();
				string currentTable = currentPath.Tables[currentPath.Tables.Count - 1];

				if (string.Equals(currentTable, targetTable, StringComparison.OrdinalIgnoreCase))
				{
					// Found a path to the target
					result.Add(currentPath);
					continue; // Continue searching for other paths
				}

				// Check all adjacent tables
				if (JoinGraph.TryGetValue(currentTable, out var edges))
				{
					foreach (var edge in edges)
					{
						string nextTable = edge.TargetTable;

						// Skip if already visited in this path
						if (currentPath.Tables.Any(t => string.Equals(t, nextTable, StringComparison.OrdinalIgnoreCase)))
							continue;

						// Create a new path with this edge
						var newPath = new JoinPath();
						newPath.Tables.AddRange(currentPath.Tables);
						newPath.Tables.Add(nextTable);
						newPath.Edges.AddRange(currentPath.Edges);
						newPath.Edges.Add(edge);

						queue.Enqueue(newPath);
					}
				}
			}

			return result;
		}

		/// <summary>
		/// Gets the join path from the main table to the specified target table
		/// </summary>
		/// <param name="targetTable">The target table to find paths to</param>
		/// <returns>A list of possible join paths</returns>
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

		/// <summary>
        /// Finds a column or alias in the SQL statement and generates a path from its table to the main table
        /// </summary>
        public ColumnPathResult GetColumnPath(string dataFieldName, string mainTable = null)
        {
            // Check if main table matches
            if (mainTable != null && mainTable != MainTable.TableName)
                return null;

            var result = new ColumnPathResult
            {
                DataFieldName = dataFieldName,
                Found = false
            };

            // Check if it's a parameter
            if (dataFieldName.StartsWith("@"))
            {
                if (Parameters.Contains(dataFieldName))
                {
                    result.Found = true;
                    result.IsParameter = true;
                    return result;
                }
            }

            // First, check for direct column matches
            foreach (var column in Columns)
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
                foreach (var column in Columns)
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
                foreach (var join in Joins)
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
                if (string.Equals(result.TableName, MainTable.GetEffectiveName(), StringComparison.OrdinalIgnoreCase))
                {
                    tableSource = MainTable;
                }
                else
                {
                    // Check joined tables
                    foreach (var join in Joins)
                    {
                        if (string.Equals(result.TableName, join.Table.GetEffectiveName(), StringComparison.OrdinalIgnoreCase))
                        {
                            tableSource = join.Table;
                            break;
                        }
                    }
                }

                // If it's not the main table, build path from main table to this table
                if (tableSource != null && !string.Equals(result.TableName, MainTable.GetEffectiveName(), StringComparison.OrdinalIgnoreCase))
                {
                    var paths = GetJoinPath(result.TableName);

                    if (paths.Count > 0)
                    {
                        // Use first available path
                        var path = paths[0];
                        var pathBuilder = new StringBuilder();

                        // Start with main table
                        pathBuilder.Append(MainTable.TableName);

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
                                    foreach (var joinEdge in Joins)
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
                    result.DataTablePath = MainTable.TableName;
                }
            }

            return result;
        }
	}
}