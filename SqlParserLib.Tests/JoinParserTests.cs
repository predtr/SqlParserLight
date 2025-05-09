using SqlParserLib.AST;
using SqlParserLib.Lexer;
using SqlParserLib.Parser;
using SqlParserLib.Interfaces;

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

	[Fact]
	public void Parse_ComplexConditionWithLiteral_ParsesCorrectly()
	{
		string expression = @"
	        SELECT toto.toto
	        FROM TotoTable toto
	            INNER JOIN dbo.TataTable tata
	                ON toto.field1 = tata.field1
	            INNER JOIN dbo.TutuTable tutu
	                ON tutu.field1 = tata.field1
	            LEFT JOIN dbo.TitiTable titi
	                ON titi.field1 = tutu.field1
	                   AND titi.field2 = 1
	            LEFT JOIN dbo.TeteTable tete
	                ON tete.field1 = titi.field1
	                   AND tete.field2 = titi.field2
	        WHERE toto.status = 'val';";
		ISqlParser parser = new SqlParser();
		SqlStatement statement = parser.Parse(expression);
		Assert.Equal(4, statement.Joins.Count);
	}

	[Fact]
	public void Parse_ComplexConditionWithLiteralInFirstCondition_ParsesCorrectly()
	{
		string expression = @"
	    SELECT toto.toto
	    FROM TotoTable toto
	        INNER JOIN dbo.TataTable tata
	            ON toto.field1 = tata.field1
	        INNER JOIN dbo.TutuTable tutu
	            ON tutu.field1 = tata.field1
	        LEFT JOIN dbo.TitiTable titi
	            ON titi.field1 = 'tutu'
	               AND titi.field2 = 1
	        LEFT JOIN dbo.TeteTable tete
	            ON tete.field1 = titi.field1
	               AND tete.field2 = titi.field2
	    WHERE toto.status = 'val';";
		ISqlParser parser = new SqlParser();
		SqlStatement statement = parser.Parse(expression);
		Assert.Equal(4, statement.Joins.Count);
	}

	[Fact]
	public void Parse_ComplexConditionWithLeftSelect_ParsesCorrectly()
	{
		//We must ignore Left/inner Select as we don't need them
		string expression = @"
	        SELECT toto.toto
	        FROM TotoTable toto
	            LEFT JOIN
				(
					SELECT tata.field1
					FROM [dbo].TataTable tata
					GROUP BY field1
				) ns
					ON ns.field1 = toto.field1
	        WHERE toto.status = 'val';";
		ISqlParser parser = new SqlParser();
		SqlStatement statement = parser.Parse(expression);
		Assert.Equal(1, statement.Joins.Count);
	}
    }
}
