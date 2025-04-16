using SqlParserLib.Parser;
using SqlParserLib.Visitors;

namespace SqlParserLib.Tests
{
	public class SqlPrinterVisitorTests
    {
        [Fact]
        public void Accept_SimpleSelect_GeneratesCorrectSql()
        {
            // Arrange
            var parser = new SqlParser();
            var sql = "SELECT id, name FROM users";
            var statement = parser.Parse(sql);
            var visitor = new SqlPrinterVisitor();
            
            // Act
            string result = statement.Accept(visitor);
            
            // Assert
            Assert.NotNull(result);
            Assert.Contains("SELECT", result);
            Assert.Contains("id", result);
            Assert.Contains("name", result);
            Assert.Contains("FROM", result);
            Assert.Contains("users", result);
        }
        
        [Fact]
        public void Accept_WithJoin_GeneratesCorrectSql()
        {
            // Arrange
            var parser = new SqlParser();
            var sql = "SELECT u.id, u.name FROM users u INNER JOIN orders o ON u.id = o.user_id";
            var statement = parser.Parse(sql);
            var visitor = new SqlPrinterVisitor();
            
            // Act
            string result = statement.Accept(visitor);
            
            // Assert
            Assert.NotNull(result);
            Assert.Contains("INNER JOIN", result);
            Assert.Contains("orders", result);
            Assert.Contains("ON", result);
        }
        
        [Fact]
        public void Accept_WithWhereClause_GeneratesCorrectSql()
        {
            // Arrange
            var parser = new SqlParser();
            var sql = "SELECT id, name FROM users WHERE active = 1";
            var statement = parser.Parse(sql);
            var visitor = new SqlPrinterVisitor();
            
            // Act
            string result = statement.Accept(visitor);
            
            // Assert
            Assert.NotNull(result);
            Assert.Contains("WHERE", result);
            Assert.Contains("active = 1", result);
        }
        
        [Fact]
        public void Accept_WithExpression_FormatsExpressionsCorrectly()
        {
            // Arrange
            var parser = new SqlParser();
            var sql = "SELECT COUNT(*) AS total, SUM(amount) AS sum_amount FROM orders";
            var statement = parser.Parse(sql);
            var visitor = new SqlPrinterVisitor();
            
            // Act
            string result = statement.Accept(visitor);
            
            // Assert
            Assert.NotNull(result);
            Assert.Contains("COUNT(*)", result);
            Assert.Contains("SUM(orders.amount)", result);
            Assert.Contains("AS total", result);
            Assert.Contains("AS sum_amount", result);
        }
    }
}