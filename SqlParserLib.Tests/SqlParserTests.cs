using SqlParserLib.AST;
using SqlParserLib.Parser;

namespace SqlParserLib.Tests
{
	public class SqlParserTests
    {
        [Fact]
        public void Parse_SimpleSelect_ReturnsValidStatement()
        {
            // Arrange
            var parser = new SqlParser();
            var sql = "SELECT id, name FROM users";
            
            // Act
            SqlStatement statement = parser.Parse(sql);
            
            // Assert
            Assert.NotNull(statement);
            Assert.Equal("users", statement.MainTable.TableName);
            Assert.Equal(2, statement.Columns.Count);
            Assert.Equal("id", statement.Columns[0].Name);
            Assert.Equal("name", statement.Columns[1].Name);
            Assert.Empty(statement.Joins);
            Assert.Null(statement.WhereClause);
        }
        
        [Fact]
        public void Parse_WithJoin_ReturnsCorrectJoinInfo()
        {
            // Arrange
            var parser = new SqlParser();
            var sql = "SELECT u.id, u.name FROM users u INNER JOIN orders o ON u.id = o.user_id";
            
            // Act
            SqlStatement statement = parser.Parse(sql);
            
            // Assert
            Assert.NotNull(statement);
            Assert.Equal("users", statement.MainTable.TableName);
            Assert.Equal("u", statement.MainTable.Alias);
            Assert.Single(statement.Joins);
            
            var join = statement.Joins[0];
            Assert.Equal("orders", join.Table.TableName);
            Assert.Equal("o", join.Table.Alias);
            Assert.Equal(JoinType.Inner, join.JoinType);
            
            // Check join condition
            Assert.NotNull(join.Condition);
            Assert.Equal("u", join.Condition.LeftColumn.TableName);
            Assert.Equal("id", join.Condition.LeftColumn.ColumnName);
            Assert.Equal("o", join.Condition.RightColumn.TableName);
            Assert.Equal("user_id", join.Condition.RightColumn.ColumnName);
        }
        
        [Fact]
        public void Parse_WithWhereClause_ParsesConditions()
        {
            // Arrange
            var parser = new SqlParser();
            var sql = "SELECT id, name FROM users WHERE active = 1 AND age > 18";
            
            // Act
            SqlStatement statement = parser.Parse(sql);
            
            // Assert
            Assert.NotNull(statement);
            Assert.NotNull(statement.WhereClause);
            // Further assertions on the where clause would depend on how your parser 
            // structures conditions in the AST
        }
        
        [Fact]
        public void GetColumnPath_WithDirectColumn_ReturnsCorrectPath()
        {
            // Arrange
            var parser = new SqlParser();
            var sql = "SELECT u.id, u.name FROM users u INNER JOIN orders o ON u.id = o.user_id";
            SqlStatement statement = parser.Parse(sql);

            // Act
            var result = statement.GetColumnPath("id");

            // Assert
            Assert.NotNull(result);
            Assert.True(result.Found);
            Assert.True(result.IsColumn);
            Assert.Equal("u", result.TableName);
            Assert.Equal("users", result.DataTablePath);
        }
    }
}