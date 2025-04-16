using System;
using System.Collections.Generic;
using SqlParserLib.Interfaces;

namespace SqlParserLib.AST
{
    /// <summary>
    /// Represents a JOIN clause in a SQL statement
    /// </summary>
    public class SqlJoin : SqlAstNode
    {
        /// <summary>
        /// The type of join (INNER, LEFT, etc.)
        /// </summary>
        public JoinType JoinType { get; set; }

        /// <summary>
        /// The table being joined
        /// </summary>
        public SqlTableSource Table { get; set; }

        /// <summary>
        /// The join condition (e.g., ON table1.column = table2.column)
        /// </summary>
        public SqlJoinCondition Condition { get; set; }

        /// <summary>
        /// Accept method for implementing the visitor pattern
        /// </summary>
        public override T Accept<T>(ISqlVisitor<T> visitor)
        {
            return visitor.VisitJoin(this);
        }

        /// <summary>
        /// Returns a string representation of the join.
        /// </summary>
        public override string ToString()
        {
            string joinTypeStr = JoinType switch
            {
                JoinType.Inner => "INNER JOIN",
                JoinType.Left => "LEFT JOIN",
                JoinType.Right => "RIGHT JOIN",
                JoinType.Full => "FULL JOIN",
                JoinType.Cross => "CROSS JOIN",
                _ => JoinType.ToString()
            };

            string result = $"{joinTypeStr} {Table}";

            if (Condition != null)
            {
                result += $" ON {Condition}";
            }

            return result;
        }
    }

    /// <summary>
    /// Represents a join condition in a JOIN clause
    /// </summary>
    public class SqlJoinCondition : SqlAstNode
    {
        /// <summary>
        /// The left side column reference
        /// </summary>
        public ColumnReference LeftColumn { get; set; }

        /// <summary>
        /// The right side column reference
        /// </summary>
        public ColumnReference RightColumn { get; set; }

        /// <summary>
        /// The operator used in the condition (usually =)
        /// </summary>
        public string Operator { get; set; }

        /// <summary>
        /// Accept method for implementing the visitor pattern
        /// </summary>
        public override T Accept<T>(ISqlVisitor<T> visitor)
        {
            return visitor.VisitJoinCondition(this);
        }

        /// <summary>
        /// Returns a string representation of the join condition.
        /// </summary>
        public override string ToString()
        {
            return $"{LeftColumn.TableName}.{LeftColumn.ColumnName} {Operator} {RightColumn.TableName}.{RightColumn.ColumnName}";
        }
    }

    /// <summary>
    /// Represents a column reference (table.column)
    /// </summary>
    public class ColumnReference
    {
        /// <summary>
        /// The table name or alias
        /// </summary>
        public string TableName { get; set; }

        /// <summary>
        /// The column name
        /// </summary>
        public string ColumnName { get; set; }

        /// <summary>
        /// Returns a string representation of the column reference
        /// </summary>
        public override string ToString()
        {
            if (string.IsNullOrEmpty(TableName))
                return ColumnName;
            
            return $"{TableName}.{ColumnName}";
        }
    }

    /// <summary>
    /// Represents a join edge in the join graph.
    /// </summary>
    public class JoinEdge
    {
        /// <summary>
        /// Gets or sets the source table name.
        /// </summary>
        public string SourceTable { get; set; }
        
        /// <summary>
        /// Gets or sets the target table name.
        /// </summary>
        public string TargetTable { get; set; }
        
        /// <summary>
        /// Gets or sets the join associated with this edge.
        /// </summary>
        public SqlJoin Join { get; set; }

        /// <summary>
        /// Returns a string representation of the join edge.
        /// </summary>
        public override string ToString()
        {
            return $"{SourceTable} -> {TargetTable}";
        }
    }

    /// <summary>
    /// Represents a path of joins between tables.
    /// </summary>
    public class JoinPath
    {
        /// <summary>
        /// Gets or sets the list of tables in this join path.
        /// </summary>
        public List<string> Tables { get; set; } = new List<string>();
        
        /// <summary>
        /// Gets or sets the edges in the join path.
        /// </summary>
        public List<JoinEdge> Edges { get; set; } = new List<JoinEdge>();

        /// <summary>
        /// Returns a string representation of the join path.
        /// </summary>
        public override string ToString()
        {
            return string.Join(" -> ", Edges);
        }
    }

    /// <summary>
    /// Enum defining the types of JOIN operations
    /// </summary>
    public enum JoinType
    {
        Inner,
        Left,
        Right,
        Full,
        Cross
    }
}