using System.Collections.Generic;
using Xunit;
using SqlParserLib.AST;
using SqlParserLib.Lexer;
using SqlParserLib.Parser;

namespace SqlParserLib.Tests
{
    public class TableSourceParserTests
    {
        private ParserContext CreateContext(string sql)
        {
            var lexer = new SqlLexer();
            var tokens = lexer.Tokenize(sql);
            return new ParserContext(tokens);
        }

        [Fact]
        public void Parse_SimpleTableName_ReturnsCorrectTableSource()
        {
            // Arrange
            var parser = new TableSourceParser();
            var context = CreateContext("users");
            
            // Act
            var tableSource = parser.Parse(context);
            
            // Assert
            Assert.NotNull(tableSource);
            Assert.Equal("users", tableSource.TableName);
            Assert.Null(tableSource.Alias);
        }
        
        [Fact]
        public void Parse_TableNameWithAlias_ReturnsCorrectTableSource()
        {
            // Arrange
            var parser = new TableSourceParser();
            var context = CreateContext("users u");
            
            // Act
            var tableSource = parser.Parse(context);
            
            // Assert
            Assert.NotNull(tableSource);
            Assert.Equal("users", tableSource.TableName);
            Assert.Equal("u", tableSource.Alias);
        }
        
        [Fact]
        public void Parse_TableNameWithAliasAndAs_ReturnsCorrectTableSource()
        {
            // Arrange
            var parser = new TableSourceParser();
            var context = CreateContext("users AS u");
            
            // Act
            var tableSource = parser.Parse(context);
            
            // Assert
            Assert.NotNull(tableSource);
            Assert.Equal("users", tableSource.TableName);
            Assert.Equal("u", tableSource.Alias);
        }
        
        [Fact]
        public void Parse_SchemaQualifiedTableName_ParsesCorrectly()
        {
            // Arrange
            var parser = new TableSourceParser();
            var context = CreateContext("dbo.users");
            
            // Act
            var tableSource = parser.Parse(context);
            
            // Assert
            Assert.NotNull(tableSource);
            Assert.Equal("dbo.users", tableSource.TableName);
            Assert.Null(tableSource.Alias);
        }
        
        [Fact]
        public void Parse_DatabaseQualifiedTableName_ParsesCorrectly()
        {
            // Arrange
            var parser = new TableSourceParser();
            var context = CreateContext("mydb.dbo.users u");
            
            // Act
            var tableSource = parser.Parse(context);
            
            // Assert
            Assert.NotNull(tableSource);
            Assert.Equal("mydb.dbo.users", tableSource.TableName);
            Assert.Equal("u", tableSource.Alias);
        }
    }
}