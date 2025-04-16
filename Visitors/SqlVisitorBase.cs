using SqlParserLib.AST;
using SqlParserLib.Interfaces;

namespace SqlParserLib.Visitors
{
	/// <summary>
	/// Base implementation for SQL visitors with default behavior
	/// </summary>
	/// <typeparam name="T">The return type of the visitor</typeparam>
	public abstract class SqlVisitorBase<T> : ISqlVisitor<T>
	{
		/// <summary>
		/// Default implementation for visiting a SQL statement
		/// </summary>
		public virtual T VisitStatement(SqlStatement statement)
		{
			// Visit all columns
			foreach (var column in statement.Columns)
			{
				column.Accept(this);
			}

			// Visit the main table
			if (statement.MainTable != null)
			{
				statement.MainTable.Accept(this);
			}

			// Visit all joins
			foreach (var join in statement.Joins)
			{
				join.Accept(this);
			}

			// Visit the WHERE condition
			if (statement.WhereCondition != null)
			{
				statement.WhereCondition.Accept(this);
			}

			return default!;
		}

		/// <summary>
		/// Default implementation for visiting a table source
		/// </summary>
		public virtual T VisitTableSource(SqlTableSource tableSource)
		{
			return default!;
		}

		/// <summary>
		/// Default implementation for visiting a join
		/// </summary>
		public virtual T VisitJoin(SqlJoin join)
		{
			if (join.Table != null)
			{
				join.Table.Accept(this);
			}

			if (join.Condition != null)
			{
				join.Condition.Accept(this);
			}

			return default!;
		}

		/// <summary>
		/// Default implementation for visiting a column
		/// </summary>
		public virtual T VisitColumn(SqlColumn column)
		{
			if (column.Expression != null)
			{
				column.Expression.Accept(this);
			}

			return default!;
		}

		/// <summary>
		/// Default implementation for visiting a SQL expression
		/// </summary>
		public virtual T VisitExpression(SqlExpression expression)
		{
			// Visit nested expressions in this expression

			// Visit function arguments
			foreach (var arg in expression.Arguments)
			{
				arg.Accept(this);
			}

			// Visit binary expression components
			if (expression.Left != null)
			{
				expression.Left.Accept(this);
			}

			if (expression.Right != null)
			{
				expression.Right.Accept(this);
			}

			// Visit case expression components
			if (expression.CaseValue != null)
			{
				expression.CaseValue.Accept(this);
			}

			foreach (var whenClause in expression.WhenClauses)
			{
				whenClause.Accept(this);
			}

			if (expression.ElseExpression != null)
			{
				expression.ElseExpression.Accept(this);
			}

			return default!;
		}

		/// <summary>
		/// Default implementation for visiting a case when clause
		/// </summary>
		public virtual T VisitCaseWhen(SqlCaseWhen caseWhen)
		{
			if (caseWhen.WhenExpression != null)
			{
				caseWhen.WhenExpression.Accept(this);
			}

			if (caseWhen.ThenExpression != null)
			{
				caseWhen.ThenExpression.Accept(this);
			}

			return default!;
		}

		/// <summary>
		/// Default implementation for visiting a join condition
		/// </summary>
		public virtual T VisitJoinCondition(SqlJoinCondition joinCondition)
		{
			return default!;
		}
	}
}
