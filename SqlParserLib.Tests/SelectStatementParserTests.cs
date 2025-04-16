using System;
using System.Collections.Generic;
using Xunit;
using SqlParserLib.AST;
using SqlParserLib.Core;
using SqlParserLib.Lexer;
using SqlParserLib.Parser;

namespace SqlParserLib.Tests
{
    public class SelectStatementParserTests
    {
        private ParserContext CreateContext(string sql)
        {
            var lexer = new SqlLexer();
            var tokens = lexer.Tokenize(sql);
            return new ParserContext(tokens);
        }

        [Fact]
        public void Parse_SimpleSelect_ParsesColumnsCorrectly()
        {
            // Arrange
            var parser = new SelectStatementParser(new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
            {
                "COUNT", "SUM", "AVG"
            });
            var context = CreateContext("SELECT id, name, age FROM users");
            var skippedExpressions = new List<string>();
            
            // Act
            var statement = parser.Parse(context, skippedExpressions);
            
            // Assert
            Assert.NotNull(statement);
            Assert.Equal(3, statement.Columns.Count);
            Assert.Equal("id", statement.Columns[0].Name);
            Assert.Equal("name", statement.Columns[1].Name);
            Assert.Equal("age", statement.Columns[2].Name);
            Assert.Empty(skippedExpressions);
        }
        
        [Fact]
        public void Parse_WithTableAliases_ParsesTableSourcesCorrectly()
        {
            // Arrange
            var parser = new SelectStatementParser(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            var context = CreateContext("SELECT u.id, u.name FROM users u");
            var skippedExpressions = new List<string>();
            
            // Act
            var statement = parser.Parse(context, skippedExpressions);
            
            // Assert
            Assert.NotNull(statement);
            Assert.Equal("users", statement.MainTable.TableName);
            Assert.Equal("u", statement.MainTable.Alias);
            
            Assert.Equal(2, statement.Columns.Count);
            Assert.Equal("u", statement.Columns[0].Table.GetEffectiveName());
            Assert.Equal("id", statement.Columns[0].Name);
            Assert.Equal("u", statement.Columns[1].Table.GetEffectiveName());
            Assert.Equal("name", statement.Columns[1].Name);
        }
        
        [Fact]
        public void Parse_WithColumnAlias_SetsAliasCorrectly()
        {
            // Arrange
            var parser = new SelectStatementParser(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            var context = CreateContext("SELECT id AS user_id, name AS user_name FROM users");
            var skippedExpressions = new List<string>();
            
            // Act
            var statement = parser.Parse(context, skippedExpressions);
            
            // Assert
            Assert.NotNull(statement);
            Assert.Equal(2, statement.Columns.Count);
            Assert.Equal("id", statement.Columns[0].Name);
            Assert.Equal("user_id", statement.Columns[0].Alias);
            Assert.Equal("name", statement.Columns[1].Name);
            Assert.Equal("user_name", statement.Columns[1].Alias);
        }
        
        [Fact]
        public void Parse_WithFunctions_ParsesExpressionsCorrectly()
        {
            // Arrange
            var parser = new SelectStatementParser(new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
            { 
                "COUNT", "SUM", "AVG" 
            });
            var context = CreateContext("SELECT COUNT(*) AS total_count FROM users");
            var skippedExpressions = new List<string>();
            
            // Act
            var statement = parser.Parse(context, skippedExpressions);
            
            // Assert
            Assert.NotNull(statement);
            Assert.Single(statement.Columns);
            Assert.True(statement.Columns[0].IsExpression);
            Assert.Equal("total_count", statement.Columns[0].Alias);
        }
    }
}