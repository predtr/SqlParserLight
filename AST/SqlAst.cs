using System;
using System.Collections.Generic;
using System.Linq;
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
	}
}