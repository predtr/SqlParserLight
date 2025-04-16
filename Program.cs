using System;
using SqlParserLib.AST;
using SqlParserLib.Interfaces;
using SqlParserLib.Parser;
using SqlParserLib.Visitors;

namespace SqlParserLib
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("SQL Parser Light - Refactored Demo");
            Console.WriteLine("================================");

            // Test parsing a simple SQL statement
            string sql1 = @"
                SELECT 
                    e.EmployeeID, 
                    e.FirstName, 
                    e.LastName,
                    DATEDIFF(YEAR, e.HireDate, GETDATE()) AS YearsEmployed,
                    d.DepartmentName
                FROM Employees e
                INNER JOIN Departments d ON e.DepartmentID = d.DepartmentID
                WHERE e.IsActive = 1";

            DemonstrateParser(sql1, "Simple SQL Statement");

            // Test parsing a more complex SQL statement
            string sql2 = @"
                SELECT 
                    c.CustomerID,
                    c.CustomerName,
                    CASE 
                        WHEN SUM(o.TotalAmount) > 10000 THEN 'Premium'
                        WHEN SUM(o.TotalAmount) > 5000 THEN 'Gold'
                        ELSE 'Regular'
                    END AS CustomerCategory,
                    COUNT(o.OrderID) AS TotalOrders,
                    SUM(o.TotalAmount) AS TotalSpent
                FROM Customers c
                LEFT JOIN Orders o ON c.CustomerID = o.CustomerID
                LEFT JOIN OrderDetails od ON o.OrderID = od.OrderID
                LEFT JOIN Products p ON od.ProductID = p.ProductID
                WHERE c.RegisteredDate > @startDate
                    AND (p.CategoryID = @category OR @category IS NULL)";

            DemonstrateParser(sql2, "Complex SQL Statement with CASE and Parameters");


			Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }

        /// <summary>
        /// Demonstrates the SQL parser by parsing a SQL query and showing various outputs
        /// </summary>
        static void DemonstrateParser(string sql, string title)
        {
            Console.WriteLine($"\n\n{title}:");
            Console.WriteLine(new string('-', title.Length + 1));
            
            // Create SQL parser
            ISqlParser parser = new SqlParser();
            
            try
            {
                // Parse the SQL
                SqlStatement statement = parser.Parse(sql);
                
                // Display information about the parsed statement
                Console.WriteLine($"\nParsing Results:");
                Console.WriteLine($"- Main table: {statement.MainTable.GetFullName()}");
                Console.WriteLine($"- Number of columns: {statement.Columns.Count}");
                Console.WriteLine($"- Number of joins: {statement.Joins.Count}");
                
                // If there are any skipped expressions, show them
                var skippedExpressions = parser is SqlParser sqlParser ? sqlParser.SkippedExpressions : null;
                if (skippedExpressions != null && skippedExpressions.Count > 0)
                {
                    Console.WriteLine("\nSkipped expressions during parsing:");
                    foreach (var expr in skippedExpressions)
                    {
                        Console.WriteLine($"- {expr}");
                    }
                }

                // Get any parameters in the query
                if (statement.Parameters.Count > 0)
                {
                    Console.WriteLine("\nParameters found:");
                    foreach (var param in statement.Parameters)
                    {
                        Console.WriteLine($"- {param}");
                    }
                }

                // Format the parsed SQL using our SQL visitor
                Console.WriteLine("\nFormatted SQL:");
                var sqlPrinter = new SqlPrinterVisitor();
                string formattedSql = statement.Accept(sqlPrinter);
                Console.WriteLine(formattedSql);

                // Demonstrate column path functionality
                if (statement.Columns.Count > 0)
                {
                    string columnToFind = statement.Columns[0].Name;
                    Console.WriteLine($"\nFinding path for column: {columnToFind}");
                    var result = parser.GetColumnPath(statement, columnToFind);
                    Console.WriteLine(result);

                    // If there are aliases, demonstrate finding one
                    var columnWithAlias = statement.Columns.Find(c => !string.IsNullOrEmpty(c.Alias));
                    if (columnWithAlias != null)
                    {
                        Console.WriteLine($"\nFinding path for alias: {columnWithAlias.Alias}");
                        result = parser.GetColumnPath(statement, columnWithAlias.Alias);
                        Console.WriteLine(result);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError parsing SQL: {ex.Message}");
            }
        }
    }
}
