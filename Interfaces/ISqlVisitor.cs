using SqlParserLib.AST;

namespace SqlParserLib.Interfaces
{
    /// <summary>
    /// Interface for visitor pattern implementation to traverse the SQL AST
    /// </summary>
    public interface ISqlVisitor<T>
    {
        T VisitStatement(SqlStatement statement);
        T VisitTableSource(SqlTableSource tableSource);
        T VisitJoin(SqlJoin join);
        T VisitColumn(SqlColumn column);
        T VisitExpression(SqlExpression expression);
        T VisitCaseWhen(SqlCaseWhen caseWhen);
        T VisitJoinCondition(SqlJoinCondition joinCondition);
    }
}