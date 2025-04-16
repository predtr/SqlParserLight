using System;
using System.Collections.Generic;
using System.Text;
using SqlParserLib.AST;

namespace SqlParserLib.Visitors
{
    /// <summary>
    /// Visitor that prints a SQL statement in a formatted way
    /// </summary>
    public class SqlPrinterVisitor : SqlVisitorBase<string>
    {
        private int _indentLevel = 0;
        private readonly StringBuilder _sb = new StringBuilder();

        /// <summary>
        /// Visit a SQL statement and return its formatted string representation
        /// </summary>
        public override string VisitStatement(SqlStatement statement)
        {
            _sb.Clear();
            _indentLevel = 0;

            // SELECT clause
            AppendLine("SELECT");
            _indentLevel++;

            // Columns
            for (int i = 0; i < statement.Columns.Count; i++)
            {
                string columnStr = statement.Columns[i].Accept(this);
                
                if (i < statement.Columns.Count - 1)
                    AppendLine($"{columnStr},");
                else
                    AppendLine(columnStr);
            }
            
            _indentLevel--;
            
            // FROM clause
            AppendLine("FROM");
            _indentLevel++;
            AppendLine(statement.MainTable.Accept(this));
            _indentLevel--;

            // JOIN clauses
            foreach (var join in statement.Joins)
            {
                AppendLine(join.Accept(this));
            }

            // WHERE clause
            if (statement.WhereCondition != null)
            {
                AppendLine("WHERE");
                _indentLevel++;
                AppendLine(statement.WhereCondition.Accept(this));
                _indentLevel--;
            }

            return _sb.ToString();
        }

        /// <summary>
        /// Visit a table source and return its string representation
        /// </summary>
        public override string VisitTableSource(SqlTableSource tableSource)
        {
            string tableStr = string.IsNullOrEmpty(tableSource.SchemaName)
                ? tableSource.TableName
                : $"{tableSource.SchemaName}.{tableSource.TableName}";

            if (!string.IsNullOrEmpty(tableSource.Alias))
                return $"{tableStr} AS {tableSource.Alias}";

            return tableStr;
        }

        /// <summary>
        /// Visit a join and return its string representation
        /// </summary>
        public override string VisitJoin(SqlJoin join)
        {
            string joinType = join.JoinType switch
            {
                JoinType.Inner => "INNER JOIN",
                JoinType.Left => "LEFT JOIN",
                JoinType.Right => "RIGHT JOIN",
                JoinType.Full => "FULL JOIN",
                JoinType.Cross => "CROSS JOIN",
                _ => "JOIN"
            };

            if (join.JoinType == JoinType.Cross)
            {
                return $"{joinType} {join.Table.Accept(this)}";
            }
            else
            {
                return $"{joinType} {join.Table.Accept(this)} ON {join.Condition.Accept(this)}";
            }
        }

        /// <summary>
        /// Visit a column and return its string representation
        /// </summary>
        public override string VisitColumn(SqlColumn column)
        {
            string columnStr;

            if (column.IsExpression)
            {
                columnStr = column.Expression.Accept(this);
            }
            else if (!string.IsNullOrEmpty(column.TableName))
            {
                columnStr = $"{column.TableName}.{column.Name}";
            }
            else
            {
                columnStr = column.Name;
            }

            if (!string.IsNullOrEmpty(column.Alias))
            {
                columnStr += $" AS {column.Alias}";
            }

            return columnStr;
        }

        /// <summary>
        /// Visit a join condition and return its string representation
        /// </summary>
        public override string VisitJoinCondition(SqlJoinCondition joinCondition)
        {
            return $"{joinCondition.LeftColumn.TableName}.{joinCondition.LeftColumn.ColumnName} " +
                   $"{joinCondition.Operator} " +
                   $"{joinCondition.RightColumn.TableName}.{joinCondition.RightColumn.ColumnName}";
        }

        /// <summary>
        /// Visit an expression and return its string representation
        /// </summary>
        public override string VisitExpression(SqlExpression expression)
        {
            switch (expression.Type)
            {
                case ExpressionType.ColumnReference:
                    var column = expression.ReferencedColumns[0];
                    return string.IsNullOrEmpty(column.TableName) 
                        ? column.ColumnName 
                        : $"{column.TableName}.{column.ColumnName}";
                
                case ExpressionType.FunctionCall:
                    return FormatFunctionCall(expression);
                
                case ExpressionType.Literal:
                    return FormatLiteral(expression);
                
                case ExpressionType.Parameter:
                    return expression.ParameterName;
                
                case ExpressionType.BinaryExpression:
                    return $"{expression.Left.Accept(this)} {expression.Operator} {expression.Right.Accept(this)}";
                
                case ExpressionType.LogicalExpression:
                    return FormatLogicalExpression(expression);
                
                case ExpressionType.IsNullExpression:
                    return FormatIsNullExpression(expression);
                
                case ExpressionType.Case:
                    return FormatCaseExpression(expression);
                
                case ExpressionType.Unparseable:
                    return $"/* Unparseable: {expression.UnparseableText} */";
                
                default:
                    if (expression.Tokens.Count > 0)
                    {
                        var tokensBuilder = new StringBuilder();
                        foreach (var token in expression.Tokens)
                        {
                            tokensBuilder.Append(token.Lexeme).Append(" ");
                        }
                        return tokensBuilder.ToString().Trim();
                    }
                    return "/* Unknown expression */";
            }
        }

        /// <summary>
        /// Visit a CASE-WHEN clause and return its string representation
        /// </summary>
        public override string VisitCaseWhen(SqlCaseWhen caseWhen)
        {
            return $"WHEN {caseWhen.WhenExpression.Accept(this)} THEN {caseWhen.ThenExpression.Accept(this)}";
        }

        #region Helper Methods

        private string FormatFunctionCall(SqlExpression expression)
        {
            var args = new List<string>();
            foreach (var arg in expression.Arguments)
            {
                args.Add(arg.Accept(this));
            }
            
            return $"{expression.FunctionName}({string.Join(", ", args)})";
        }

        private string FormatLiteral(SqlExpression expression)
        {
            if (expression.LiteralType == "string")
                return $"'{expression.LiteralValue}'";
            else if (expression.LiteralType == "null")
                return "NULL";
            else
                return expression.LiteralValue?.ToString();
        }

        private string FormatLogicalExpression(SqlExpression expression)
        {
            var result = new StringBuilder();
            
            for (int i = 0; i < expression.Conditions.Count; i++)
            {
                result.Append(expression.Conditions[i].Accept(this));
                
                if (i < expression.LogicalOperators.Count)
                {
                    result.Append($" {expression.LogicalOperators[i]} ");
                }
            }
            
            return result.ToString();
        }

        private string FormatIsNullExpression(SqlExpression expression)
        {
            string left = expression.Left.Accept(this);
            return expression.IsNull 
                ? $"{left} IS NULL" 
                : $"{left} IS NOT NULL";
        }

        private string FormatCaseExpression(SqlExpression expression)
        {
            var caseBuilder = new StringBuilder();
            caseBuilder.Append("CASE ");
            
            if (expression.CaseValue != null)
            {
                caseBuilder.Append(expression.CaseValue.Accept(this)).Append(" ");
            }
            
            foreach (var whenClause in expression.WhenClauses)
            {
                caseBuilder.Append(whenClause.Accept(this)).Append(" ");
            }
            
            if (expression.ElseExpression != null)
            {
                caseBuilder.Append("ELSE ").Append(expression.ElseExpression.Accept(this)).Append(" ");
            }
            
            caseBuilder.Append("END");
            
            return caseBuilder.ToString();
        }

        private void AppendLine(string line)
        {
            for (int i = 0; i < _indentLevel; i++)
            {
                _sb.Append("  ");
            }
            _sb.AppendLine(line);
        }

        #endregion
    }
}