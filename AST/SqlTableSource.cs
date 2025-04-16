using System;
using SqlParserLib.Interfaces;

namespace SqlParserLib.AST
{
    /// <summary>
    /// Represents a table source in a SQL statement (table or subquery in FROM clause)
    /// </summary>
    public class SqlTableSource : SqlAstNode
    {
        /// <summary>
        /// The schema name of the table (used for backwards compatibility)
        /// </summary>
        public string SchemaName { get; set; }

        /// <summary>
        /// The table name (may include schema qualification)
        /// </summary>
        public string TableName { get; set; }

        /// <summary>
        /// The alias given to the table
        /// </summary>
        public string Alias { get; set; }

        /// <summary>
        /// Gets the effective name of this table source (alias if available, otherwise table name)
        /// </summary>
        /// <returns>The effective name to use when referencing this table</returns>
        public string GetEffectiveName()
        {
            return !string.IsNullOrEmpty(Alias) ? Alias : TableName;
        }

        /// <summary>
        /// Gets the full qualified name of the table (schema.table)
        /// </summary>
        /// <returns>The full qualified table name</returns>
        public string GetFullName()
        {
            // With the new approach, TableName already contains the full qualification
            return TableName;
        }

        /// <summary>
        /// Accept method for implementing the visitor pattern
        /// </summary>
        public override T Accept<T>(ISqlVisitor<T> visitor)
        {
            return visitor.VisitTableSource(this);
        }
    }
}