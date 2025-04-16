using System.Collections.Generic;
using SqlParserLib.Interfaces;

namespace SqlParserLib.AST
{
    /// <summary>
    /// Represents a SQL expression (function call, column reference, literal, etc.)
    /// </summary>
    public class SqlExpression : SqlAstNode
    {
        /// <summary>
        /// The type of expression
        /// </summary>
        public ExpressionType Type { get; set; }

        /// <summary>
        /// Referenced columns in this expression
        /// </summary>
        public List<ColumnReference> ReferencedColumns { get; set; } = new List<ColumnReference>();

        /// <summary>
        /// The tokens that make up this expression
        /// </summary>
        public List<Core.SqlToken> Tokens { get; set; } = new List<Core.SqlToken>();

        /// <summary>
        /// SQL parameters referenced in this expression
        /// </summary>
        public HashSet<string> Parameters { get; set; } = new HashSet<string>();

        // Function call properties
        public string FunctionName { get; set; }
        public List<SqlExpression> Arguments { get; set; } = new List<SqlExpression>();

        // Binary expression properties
        public SqlExpression Left { get; set; }
        public string Operator { get; set; }
        public SqlExpression Right { get; set; }

        // Case expression properties
        public SqlExpression CaseValue { get; set; }
        public List<SqlCaseWhen> WhenClauses { get; set; } = new List<SqlCaseWhen>();
        public SqlExpression ElseExpression { get; set; }
        
        // IS NULL expression properties
        public bool IsNullCheck { get; set; }
        public bool IsNull { get; set; }

        // Literal value properties
        public object LiteralValue { get; set; }
        public string LiteralType { get; set; }

        // Parameter properties
        public string ParameterName { get; set; }

        // Properties for expressions that couldn't be parsed
        public bool IsUnparseable => Type == ExpressionType.Unparseable;
        public string UnparseableText { get; set; }

        // Logical expression properties
        public List<SqlExpression> Conditions { get; set; } = new List<SqlExpression>();
        public List<string> LogicalOperators { get; set; } = new List<string>();

        /// <summary>
        /// Accept method for implementing the visitor pattern
        /// </summary>
        public override T Accept<T>(ISqlVisitor<T> visitor)
        {
            return visitor.VisitExpression(this);
        }
    }

    /// <summary>
    /// Represents a WHEN...THEN clause in a CASE expression
    /// </summary>
    public class SqlCaseWhen : SqlAstNode
    {
        /// <summary>
        /// The condition of the WHEN clause
        /// </summary>
        public SqlExpression WhenExpression { get; set; }

        /// <summary>
        /// The result of the THEN clause
        /// </summary>
        public SqlExpression ThenExpression { get; set; }

        /// <summary>
        /// Accept method for implementing the visitor pattern
        /// </summary>
        public override T Accept<T>(ISqlVisitor<T> visitor)
        {
            return visitor.VisitCaseWhen(this);
        }
    }

    /// <summary>
    /// Types of SQL expressions
    /// </summary>
    public enum ExpressionType
    {
        ColumnReference,
        FunctionCall,
        Function = FunctionCall, // Alias for test compatibility
        Literal,
        Case,
        BinaryExpression,
        BinaryOperation = BinaryExpression, // Alias for test compatibility
        LogicalExpression,
        IsNullExpression,
        Parameter,
        Generic,
        Unparseable
    }
}