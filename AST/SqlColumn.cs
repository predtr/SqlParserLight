using System;
using SqlParserLib.Interfaces;

namespace SqlParserLib.AST
{
    /// <summary>
    /// Represents a column in a SQL SELECT clause
    /// </summary>
    public class SqlColumn : SqlAstNode
    {
        /// <summary>
        /// The column name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The table name (if column is fully qualified)
        /// </summary>
        public string TableName { get; set; }

        /// <summary>
        /// The column alias
        /// </summary>
        public string Alias { get; set; }

        /// <summary>
        /// The table source this column belongs to
        /// </summary>
        public SqlTableSource Table { get; set; }

        /// <summary>
        /// The expression this column represents (for complex expressions)
        /// </summary>
        public SqlExpression Expression { get; set; }

        /// <summary>
        /// Whether this column is an expression rather than a simple column reference
        /// </summary>
        public bool IsExpression => Expression != null;

        /// <summary>
        /// Accept method for implementing the visitor pattern
        /// </summary>
        public override T Accept<T>(ISqlVisitor<T> visitor)
        {
            return visitor.VisitColumn(this);
        }
    }
}