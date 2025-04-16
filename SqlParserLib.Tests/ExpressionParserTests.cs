using SqlParserLib.AST;
using SqlParserLib.Lexer;
using SqlParserLib.Parser;

namespace SqlParserLib.Tests
{
	public class ExpressionParserTests
    {
        private ParserContext CreateContext(string sql)
        {
            var lexer = new SqlLexer();
            var tokens = lexer.Tokenize(sql);
            return new ParserContext(tokens);
        }
        
        [Fact]
        public void Parse_SimpleFunction_ReturnsCorrectExpression()
        {
            // Arrange
            var functionKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "COUNT", "SUM", "AVG", "MAX", "MIN"
            };
            var parser = new ExpressionParser(functionKeywords);
            var context = CreateContext("COUNT(*)");
            
            // Act
            var expression = parser.Parse(context);
            
            // Assert
            Assert.NotNull(expression);
            Assert.Equal(ExpressionType.Function, expression.Type);
            Assert.Equal("COUNT", expression.FunctionName);
        }
        
        [Fact]
        public void Parse_CaseExpression_ReturnsCorrectStructure()
        {
            // Arrange
            var functionKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "CASE", "WHEN", "THEN", "ELSE", "END"
            };
            var parser = new ExpressionParser(functionKeywords);
            var context = CreateContext("CASE WHEN status = 'active' THEN 1 ELSE 0 END");
            
            // Act
            var expression = parser.Parse(context);
            
            // Assert
            Assert.NotNull(expression);
            Assert.Equal(ExpressionType.Case, expression.Type);
        }
        
        [Fact]
        public void Parse_ArithmeticExpression_ReturnsCorrectStructure()
        {
            // Arrange
            var functionKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var parser = new ExpressionParser(functionKeywords);
            var context = CreateContext("price * quantity");
            
            // Act
            var expression = parser.Parse(context);
            
            // Assert
            Assert.NotNull(expression);
            Assert.Equal(ExpressionType.BinaryOperation, expression.Type);
            Assert.Equal("*", expression.Operator);
        }
        
        [Fact]
        public void Parse_ComplexExpression_TracksReferencedColumns()
        {
            // Arrange
            var functionKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "ROUND", "CASE", "WHEN", "THEN", "ELSE", "END"
            };
            var parser = new ExpressionParser(functionKeywords);
            var context = CreateContext("ROUND(p.price * od.quantity, 2)");
            
            // Act
            var expression = parser.Parse(context);
            
            // Assert
            Assert.NotNull(expression);
            Assert.Equal(ExpressionType.Function, expression.Type);
            Assert.Equal("ROUND", expression.FunctionName);
            
            // Check that referenced columns are tracked
            Assert.Contains(expression.ReferencedColumns, c => c.TableName == "p" && c.ColumnName == "price");
            Assert.Contains(expression.ReferencedColumns, c => c.TableName == "od" && c.ColumnName == "quantity");
        }
    }
}