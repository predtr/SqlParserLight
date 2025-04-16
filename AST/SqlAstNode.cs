using SqlParserLib.Interfaces;

namespace SqlParserLib.AST
{
    /// <summary>
    /// Base class for all SQL AST nodes
    /// </summary>
    public abstract class SqlAstNode
    {
        /// <summary>
        /// Accepts a visitor for implementing the visitor pattern
        /// </summary>
        /// <typeparam name="T">The return type of the visitor</typeparam>
        /// <param name="visitor">The visitor implementation</param>
        /// <returns>The result of the visit operation</returns>
        public abstract T Accept<T>(ISqlVisitor<T> visitor);
    }
}