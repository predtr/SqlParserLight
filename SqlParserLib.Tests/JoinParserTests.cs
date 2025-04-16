using SqlParserLib.AST;
using SqlParserLib.Lexer;
using SqlParserLib.Parser;

namespace SqlParserLib.Tests
{
	public class JoinParserTests
    {
        private ParserContext CreateContext(string sql)
        {
            var lexer = new SqlLexer();
            var tokens = lexer.Tokenize(sql);
            return new ParserContext(tokens);
        }

        [Fact]
        public void Parse_InnerJoin_ReturnsCorrectJoinInfo()
        {
            // Arrange
            var parser = new JoinParser();
            var context = CreateContext("INNER JOIN orders o ON u.id = o.user_id");
            var tables = new Dictionary<string, SqlTableSource>();
            var mainTable = new SqlTableSource { TableName = "users", Alias = "u" };
            tables["u"] = mainTable;
            
            // Act
            var join = parser.Parse(context, tables);
            
            // Assert
            Assert.NotNull(join);
            Assert.Equal(JoinType.Inner, join.JoinType);
            Assert.Equal("orders", join.Table.TableName);
            Assert.Equal("o", join.Table.Alias);
            Assert.NotNull(join.Condition);
            Assert.Equal("u", join.Condition.LeftColumn.TableName);
            Assert.Equal("id", join.Condition.LeftColumn.ColumnName);
            Assert.Equal("o", join.Condition.RightColumn.TableName);
            Assert.Equal("user_id", join.Condition.RightColumn.ColumnName);
        }

        [Fact]
        public void Parse_LeftJoin_ReturnsCorrectJoinType()
        {
            // Arrange
            var parser = new JoinParser();
            var context = CreateContext("LEFT JOIN orders o ON u.id = o.user_id");
            var tables = new Dictionary<string, SqlTableSource>();
            var mainTable = new SqlTableSource { TableName = "users", Alias = "u" };
            tables["u"] = mainTable;
            
            // Act
            var join = parser.Parse(context, tables);
            
            // Assert
            Assert.NotNull(join);
            Assert.Equal(JoinType.Left, join.JoinType);
        }

        [Fact]
        public void Parse_RightJoin_ReturnsCorrectJoinType()
        {
            // Arrange
            var parser = new JoinParser();
            var context = CreateContext("RIGHT JOIN orders o ON u.id = o.user_id");
            var tables = new Dictionary<string, SqlTableSource>();
            var mainTable = new SqlTableSource { TableName = "users", Alias = "u" };
            tables["u"] = mainTable;
            
            // Act
            var join = parser.Parse(context, tables);
            
            // Assert
            Assert.NotNull(join);
            Assert.Equal(JoinType.Right, join.JoinType);
        }
        
        [Fact]
        public void Parse_ComplexCondition_ParsesCorrectly()
        {
            // Arrange
            var parser = new JoinParser();
            var context = CreateContext("INNER JOIN orders o ON u.id = o.user_id AND o.status = 'active'");
            var tables = new Dictionary<string, SqlTableSource>();
            var mainTable = new SqlTableSource { TableName = "users", Alias = "u" };
            tables["u"] = mainTable;
            
            // Act
            var join = parser.Parse(context, tables);
            
            // Assert
            Assert.NotNull(join);
            Assert.Equal("orders", join.Table.TableName);
            Assert.NotNull(join.Condition);
            // Additional assertions would depend on how your parser handles complex conditions
        }
    }
}