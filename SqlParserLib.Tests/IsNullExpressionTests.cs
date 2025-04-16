using SqlParserLib.Interfaces;
using SqlParserLib.Parser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SqlParserLib.Tests
{
	public class IsNullExpressionTests
	{
		[Fact]
		public void ExpressionMustParse()
		{
			string expression = @"
                SELECT ISNULL(
                                 (CASE
                                      WHEN toto.test = 'same' THEN
                                          CONVERT(DECIMAL(28, 10), ISNULL(toto.tata, toto.titi))
                                      WHEN toto.test = 'src_ref' THEN
                                          CONVERT(DECIMAL(28, 10), CONVERT(DECIMAL(28, 10), ISNULL(toto.tutu, toto.tyty)) * toto.caca)
                                      WHEN toto.test = 'dest_ref' THEN
                                          CONVERT(DECIMAL(28, 10), CONVERT(DECIMAL(28, 10), ISNULL(toto.tutu, toto.tyty)) / toto.cucu)
                                      WHEN toto.test = 'none_ref' THEN
                                          CONVERT(
                                                     DECIMAL(28, 10),
                                                     CONVERT(DECIMAL(28, 10), ISNULL(toto.tutu, toto.tyty))
                                                     * CONVERT(DECIMAL(28, 10), toto.caca / toto.cucu)
                                                 )
                                      ELSE
                                          NULL
                                  END
                                 ),
                                 0
                             ) AS price
                FROM TotoTable toto";
			ISqlParser parser = new SqlParser();
            parser.Parse(expression);
		}
	}
}
