using System.Text;

namespace SqlParserLib
{
	internal class Program
	{
		static void Main(string[] args)
		{
			Console.WriteLine("SQL Parser Demo");
			Console.WriteLine("---------------");

			/*string sql = @"
					SELECT pitem.item_id, pitem.item_code, pitem.pdt_id, pitem.item_label_en AS item_label, pitem.sup_id, sup_name_en AS sup_name,
						pitem.item_sup_ref, pitem.unit_code, pitem.item_packaging, pitem.item_packaging_qty, pitem.item_public_price,
						pitem.item_nego_price, pitem.item_public_price_entry, pitem.item_nego_price_entry, pitem.item_weight,
						pitem.item_option, pitem.item_delivery_delay, pitem.ship_code, pitem.inco_code,
						pitem.tva_code, pitem.item_id_replace, pitem.item_volume, pitem.item_sup_link, pitem.contact_id_supplier,
						pitem.ctr_id, COALESCE(ctr.ctr_label_fr,ctr.ctr_label_en,ctr.ctr_label_de,ctr.ctr_label_it,ctr.ctr_label_pl,ctr.ctr_label_es,ctr.ctr_label_pt,ctr.ctr_label_zh,ctr.ctr_label_ja,ctr.ctr_label_ar) AS ctr_label, ctr.status_code AS ctr_status_code, pitem.item_comment_en AS item_comment, pitem.item_inco_place, pitem.unit_code_currency,
						pitem.status_code, 
						COALESCE(ctr.ctr_effective_date, ctr.ctr_signature_date) AS ctr_begin_date,
						pitem.item_begin_date,
						
					CASE WHEN ctr.status_code = 'del' AND DATEDIFF(DAY, ctr.deleted, COALESCE(ctr.ctr_cancel_date, ctr.ctr_updated_end_date, ctr.ctr_original_end_date, ctr.deleted)) >= 0
						THEN ctr.deleted
						ELSE COALESCE(ctr.ctr_cancel_date, ctr.ctr_updated_end_date, ctr.ctr_original_end_date)
					END AS ctr_end_date,
						pitem.item_end_date,
						COALESCE(pitem.item_nego_price_entry, pitem.item_public_price_entry, 0) AS price, COALESCE(unit_short_label_fr,unit_short_label_en,unit_short_label_de,unit_short_label_it,unit_short_label_pl,unit_short_label_es,unit_short_label_pt,unit_short_label_zh,unit_short_label_ja,unit_short_label_ar) AS unit_short_label,
						replace_item.item_label_en AS item_replace_label, COALESCE(pitem.item_quantity, 0) AS item_quantity, pitem.sold_by_quantity,
						punchout.preq_id, punchout.preq_control,
						review.review_rate, review.nb_review
						, pitem.item_preq_id
						,CASE WHEN 
			((
CASE WHEN ctr.ctr_id IS NULL OR DATEDIFF(DAY, COALESCE(COALESCE(ctr.ctr_effective_date, ctr.ctr_signature_date),pitem.item_begin_date), pitem.item_begin_date) >= 0
		THEN pitem.item_begin_date
		ELSE COALESCE(ctr.ctr_effective_date, ctr.ctr_signature_date)
END IS NULL OR DATEDIFF(DAY, 
CASE WHEN ctr.ctr_id IS NULL OR DATEDIFF(DAY, COALESCE(COALESCE(ctr.ctr_effective_date, ctr.ctr_signature_date),pitem.item_begin_date), pitem.item_begin_date) >= 0
		THEN pitem.item_begin_date
		ELSE COALESCE(ctr.ctr_effective_date, ctr.ctr_signature_date)
END, @timestamp) >= 0) AND 
			(
CASE WHEN ctr.ctr_id IS NULL OR DATEDIFF(DAY, pitem.item_end_date, COALESCE(
					CASE WHEN ctr.status_code = 'del' AND DATEDIFF(DAY, ctr.deleted, COALESCE(ctr.ctr_cancel_date, ctr.ctr_updated_end_date, ctr.ctr_original_end_date, ctr.deleted)) >= 0
						THEN ctr.deleted
						ELSE COALESCE(ctr.ctr_cancel_date, ctr.ctr_updated_end_date, ctr.ctr_original_end_date)
					END,pitem.item_end_date)) >= 0
		THEN pitem.item_end_date
		ELSE 
					CASE WHEN ctr.status_code = 'del' AND DATEDIFF(DAY, ctr.deleted, COALESCE(ctr.ctr_cancel_date, ctr.ctr_updated_end_date, ctr.ctr_original_end_date, ctr.deleted)) >= 0
						THEN ctr.deleted
						ELSE COALESCE(ctr.ctr_cancel_date, ctr.ctr_updated_end_date, ctr.ctr_original_end_date)
					END
END IS NULL OR DATEDIFF(DAY, 
CASE WHEN ctr.ctr_id IS NULL OR DATEDIFF(DAY, pitem.item_end_date, COALESCE(
					CASE WHEN ctr.status_code = 'del' AND DATEDIFF(DAY, ctr.deleted, COALESCE(ctr.ctr_cancel_date, ctr.ctr_updated_end_date, ctr.ctr_original_end_date, ctr.deleted)) >= 0
						THEN ctr.deleted
						ELSE COALESCE(ctr.ctr_cancel_date, ctr.ctr_updated_end_date, ctr.ctr_original_end_date)
					END,pitem.item_end_date)) >= 0
		THEN pitem.item_end_date
		ELSE 
					CASE WHEN ctr.status_code = 'del' AND DATEDIFF(DAY, ctr.deleted, COALESCE(ctr.ctr_cancel_date, ctr.ctr_updated_end_date, ctr.ctr_original_end_date, ctr.deleted)) >= 0
						THEN ctr.deleted
						ELSE COALESCE(ctr.ctr_cancel_date, ctr.ctr_updated_end_date, ctr.ctr_original_end_date)
					END
END, @timestamp) < 0)) THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS is_ctr_valid, 
pl.min_quantity
					FROM dbo.t_pdt_item pitem
					LEFT JOIN dbo.t_pdt_item replace_item ON replace_item.item_id=pitem.item_id_replace
					LEFT JOIN dbo.t_sup_supplier s ON s.sup_id=pitem.sup_id
					LEFT JOIN dbo.t_ctr_contract ctr ON ctr.ctr_id=pitem.ctr_id
					LEFT JOIN dbo.t_bas_unit u ON u.unit_code=pitem.unit_code_currency
					LEFT JOIN dbo.t_pun_requisition AS punchout ON punchout.preq_id = pitem.preq_id
					OUTER APPLY (
							SELECT CAST(SUM(CAST(review.blog_rate as decimal(18,2))) / COUNT(1) as decimal(18,1)) as review_rate, COUNT(1) as nb_review
							FROM dbo.t_ctn_blog review
							INNER JOIN dbo.t_pdt_item item ON item.item_id = CAST(review.x_id AS int)
							INNER JOIN dbo.t_pdt_item item2 ON item2.pdt_id = item.pdt_id AND item2.sup_id = item.sup_id
									AND ((item2.item_code IS NOT NULL AND item2.item_code = item.item_code) OR (item2.item_code IS NULL AND item.item_code IS NULL))
							WHERE review.otype_code = 'item_review' 
							AND item2.item_id = @itemId
							AND review.status_code = 'val'
							AND review.sup_id = 0
						) as review
					OUTER APPLY (SELECT MIN(iprice_quantity) AS min_quantity FROM dbo.t_pdt_item_price AS pprice WHERE pprice.item_id = pitem.item_id) AS pl
					WHERE pitem.item_id=@itemId";*/

			string sql = @"SELECT pitem.item_id,
pitem.item_option,
pitem.status_code,
ctr.status_code AS ctr_status_code,
pitem.ctr_id,
pitem.item_id_replace,
pitem.contact_id_supplier,
contact.contact_guid as contact_guid_supplier,
pitem.sup_id,
acc.acc_code + ' - ' + COALESCE(acc.acc_label_fr,acc.acc_label_en,acc.acc_label_de,acc.acc_label_it,acc.acc_label_pl,acc.acc_label_es,acc.acc_label_pt,acc.acc_label_zh,acc.acc_label_ja,acc.acc_label_ar) AS acc_label,
pitem.item_inco_place,
COALESCE(inco.inco_label_fr,inco.inco_label_en,inco.inco_label_de,inco.inco_label_it,inco.inco_label_pl,inco.inco_label_es,inco.inco_label_pt,inco.inco_label_zh,inco.inco_label_ja,inco.inco_label_ar) AS inco_label,
COALESCE(ship.ship_label_fr,ship.ship_label_en,ship.ship_label_de,ship.ship_label_it,ship.ship_label_pl,ship.ship_label_es,ship.ship_label_pt,ship.ship_label_zh,ship.ship_label_ja,ship.ship_label_ar) AS ship_label,
pitem.item_delivery_delay,
pitem.item_packaging,

CASE WHEN ctr.ctr_id IS NULL OR DATEDIFF(DAY, pitem.item_end_date, COALESCE(
					CASE WHEN ctr.status_code = 'del' AND DATEDIFF(DAY, ctr.deleted, COALESCE(ctr.ctr_cancel_date, ctr.ctr_updated_end_date, ctr.ctr_original_end_date, ctr.deleted)) >= 0
						THEN ctr.deleted
						ELSE COALESCE(ctr.ctr_cancel_date, ctr.ctr_updated_end_date, ctr.ctr_original_end_date)
					END,pitem.item_end_date)) >= 0
		THEN pitem.item_end_date
		ELSE 
					CASE WHEN ctr.status_code = 'del' AND DATEDIFF(DAY, ctr.deleted, COALESCE(ctr.ctr_cancel_date, ctr.ctr_updated_end_date, ctr.ctr_original_end_date, ctr.deleted)) >= 0
						THEN ctr.deleted
						ELSE COALESCE(ctr.ctr_cancel_date, ctr.ctr_updated_end_date, ctr.ctr_original_end_date)
					END
END AS item_end_date,

CASE WHEN ctr.ctr_id IS NULL OR DATEDIFF(DAY, COALESCE(COALESCE(ctr.ctr_effective_date, ctr.ctr_signature_date),pitem.item_begin_date), pitem.item_begin_date) >= 0
		THEN pitem.item_begin_date
		ELSE COALESCE(ctr.ctr_effective_date, ctr.ctr_signature_date)
END AS item_begin_date,
pitem.item_sup_link,
pdt.pdt_code,
pdt.pdt_label_en AS pdt_label,
pitem.item_comment_en AS item_comment,
pdt.pdt_desc_en AS pdt_desc,
pitem.item_sup_ref,
pitem.item_label_en AS item_label,
pdt.pdt_summary_en AS pdt_summary,
sup.sup_name_en AS sup_name,
ISNULL(
				(
					
				CASE 
					WHEN conv.conv_type = 'same' THEN CONVERT(DECIMAL(28,10),ISNULL(pitem.item_nego_price_entry, pitem.item_public_price_entry))
					WHEN conv.conv_type = 'src_ref' THEN CONVERT(DECIMAL(28,10),CONVERT(DECIMAL(28,10),ISNULL(pitem.item_nego_price_entry, pitem.item_public_price_entry)) * conv.ctconv_coeff)
					WHEN conv.conv_type = 'dest_ref' THEN CONVERT(DECIMAL(28,10),CONVERT(DECIMAL(28,10),ISNULL(pitem.item_nego_price_entry, pitem.item_public_price_entry)) / conv.cfconv_coeff)
					WHEN conv.conv_type = 'none_ref' THEN CONVERT(DECIMAL(28,10),CONVERT(DECIMAL(28,10),ISNULL(pitem.item_nego_price_entry, pitem.item_public_price_entry)) * CONVERT(DECIMAL(28,10),conv.ctconv_coeff / conv.cfconv_coeff))
					ELSE NULL
				 END
				), 0) AS price,
ISNULL(pitem.item_nego_price_entry, pitem.item_public_price_entry) AS price_entry,
COALESCE(unit.unit_short_label_fr,unit.unit_short_label_en,unit.unit_short_label_de,unit.unit_short_label_it,unit.unit_short_label_pl,unit.unit_short_label_es,unit.unit_short_label_pt,unit.unit_short_label_zh,unit.unit_short_label_ja,unit.unit_short_label_ar) AS unit_short_label,
pitem.unit_code_currency,
COALESCE(unit.unit_short_label_fr,unit.unit_short_label_en,unit.unit_short_label_de,unit.unit_short_label_it,unit.unit_short_label_pl,unit.unit_short_label_es,unit.unit_short_label_pt,unit.unit_short_label_zh,unit.unit_short_label_ja,unit.unit_short_label_ar) AS unit_short_label,
pitem.item_tags,
ISNULL(pl.nb_price_levels, 0) as nb_price_levels,
[qst_review].[rev_id],
[review].[avg_score] AS [review_rate],
[review].[rev_count] AS [nb_review],
[pitem].[sup_id],
punchout.preq_id,
punchout.preq_control,
CASE WHEN 
			((
CASE WHEN ctr.ctr_id IS NULL OR DATEDIFF(DAY, COALESCE(COALESCE(ctr.ctr_effective_date, ctr.ctr_signature_date),pitem.item_begin_date), pitem.item_begin_date) >= 0
		THEN pitem.item_begin_date
		ELSE COALESCE(ctr.ctr_effective_date, ctr.ctr_signature_date)
END IS NULL OR DATEDIFF(DAY, 
CASE WHEN ctr.ctr_id IS NULL OR DATEDIFF(DAY, COALESCE(COALESCE(ctr.ctr_effective_date, ctr.ctr_signature_date),pitem.item_begin_date), pitem.item_begin_date) >= 0
		THEN pitem.item_begin_date
		ELSE COALESCE(ctr.ctr_effective_date, ctr.ctr_signature_date)
END, @timestamp) >= 0) AND 
			(
CASE WHEN ctr.ctr_id IS NULL OR DATEDIFF(DAY, pitem.item_end_date, COALESCE(
					CASE WHEN ctr.status_code = 'del' AND DATEDIFF(DAY, ctr.deleted, COALESCE(ctr.ctr_cancel_date, ctr.ctr_updated_end_date, ctr.ctr_original_end_date, ctr.deleted)) >= 0
						THEN ctr.deleted
						ELSE COALESCE(ctr.ctr_cancel_date, ctr.ctr_updated_end_date, ctr.ctr_original_end_date)
					END,pitem.item_end_date)) >= 0
		THEN pitem.item_end_date
		ELSE 
					CASE WHEN ctr.status_code = 'del' AND DATEDIFF(DAY, ctr.deleted, COALESCE(ctr.ctr_cancel_date, ctr.ctr_updated_end_date, ctr.ctr_original_end_date, ctr.deleted)) >= 0
						THEN ctr.deleted
						ELSE COALESCE(ctr.ctr_cancel_date, ctr.ctr_updated_end_date, ctr.ctr_original_end_date)
					END
END IS NULL OR DATEDIFF(DAY, 
CASE WHEN ctr.ctr_id IS NULL OR DATEDIFF(DAY, pitem.item_end_date, COALESCE(
					CASE WHEN ctr.status_code = 'del' AND DATEDIFF(DAY, ctr.deleted, COALESCE(ctr.ctr_cancel_date, ctr.ctr_updated_end_date, ctr.ctr_original_end_date, ctr.deleted)) >= 0
						THEN ctr.deleted
						ELSE COALESCE(ctr.ctr_cancel_date, ctr.ctr_updated_end_date, ctr.ctr_original_end_date)
					END,pitem.item_end_date)) >= 0
		THEN pitem.item_end_date
		ELSE 
					CASE WHEN ctr.status_code = 'del' AND DATEDIFF(DAY, ctr.deleted, COALESCE(ctr.ctr_cancel_date, ctr.ctr_updated_end_date, ctr.ctr_original_end_date, ctr.deleted)) >= 0
						THEN ctr.deleted
						ELSE COALESCE(ctr.ctr_cancel_date, ctr.ctr_updated_end_date, ctr.ctr_original_end_date)
					END
END, @timestamp) < 0)) THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS is_ctr_valid
FROM t_pdt_item pitem
/* Join ID: pl */
OUTER APPLY ( SELECT COUNT(*) AS nb_price_levels FROM dbo.t_pdt_item_price AS pprice WHERE pprice.item_id = pitem.item_id) AS pl
/* Join ID: pdt */
INNER JOIN dbo.t_pdt_product AS pdt ON pdt.pdt_id=pitem.pdt_id
/* Join ID: sup */
LEFT JOIN dbo.t_sup_supplier AS sup ON sup.sup_id=pitem.sup_id
/* Join ID: contact */
LEFT JOIN dbo.t_usr_contact AS contact ON contact.contact_id=pitem.contact_id_supplier
/* Join ID: unit */
LEFT JOIN dbo.t_bas_unit as unit ON unit.unit_code=pitem.unit_code_currency
/* Join ID: conv */
OUTER APPLY (
				(
					
				SELECT 
						NULLIF(MAX(ISNULL(t.ctconv_coeff,-1)),-1) AS ctconv_coeff,
						NULLIF(MAX(ISNULL(t.cfconv_coeff,-1)),-1) AS cfconv_coeff,
						MAX(t.conv_type) AS conv_type
					FROM (
							SELECT	NULL AS ctconv_coeff, NULL AS cfconv_coeff, 
									CASE 
										WHEN pitem.unit_code_currency = @currency THEN 'same'
										WHEN pitem.unit_code_currency IS NULL THEN NULL
										WHEN @currency IS NULL THEN NULL
										WHEN pitem.unit_code_currency = @ref_currency THEN 'src_ref'
										WHEN @currency = @ref_currency THEN 'dest_ref'
										ELSE 'none_ref'
									END AS conv_type
							UNION ALL
							
						SELECT TOP 1 NULL as ctconv_coeff, xconv.conv_coeff AS cfconv_coeff, 
									CASE 
										WHEN pitem.unit_code_currency = @currency THEN 'same'
										WHEN pitem.unit_code_currency IS NULL THEN NULL
										WHEN @currency IS NULL THEN NULL
										WHEN pitem.unit_code_currency = @ref_currency THEN 'src_ref'
										WHEN @currency = @ref_currency THEN 'dest_ref'
										ELSE 'none_ref'
									END AS conv_type
						  FROM x_conversion_all xconv
						 WHERE xconv.unit_code_from = @ref_currency
						   AND xconv.unit_code_to = pitem.unit_code_currency
						 ORDER BY xconv.is_inherited ASC, xconv.year_id DESC, xconv.perio_weight ASC, xconv.perio_node DESC
							UNION ALL
							
						SELECT TOP 1 xconv.conv_coeff AS ctconv_coeff, NULL AS cfconv_coeff, 
									CASE 
										WHEN pitem.unit_code_currency = @currency THEN 'same'
										WHEN pitem.unit_code_currency IS NULL THEN NULL
										WHEN @currency IS NULL THEN NULL
										WHEN pitem.unit_code_currency = @ref_currency THEN 'src_ref'
										WHEN @currency = @ref_currency THEN 'dest_ref'
										ELSE 'none_ref'
									END AS conv_type
						  FROM x_conversion_all xconv
						 WHERE xconv.unit_code_from = @ref_currency
						   AND xconv.unit_code_to = @currency
						 ORDER BY xconv.is_inherited ASC, xconv.year_id DESC, xconv.perio_weight ASC, xconv.perio_node DESC
						) AS t
				)) AS conv
LEFT JOIN (
	SELECT [rev].[item_id],
	ISNULL(AVG(rev_score), 0) as avg_score,
	COUNT(rev_id) AS rev_count
	FROM [dbo].[t_qst_review] [rev]
	WHERE [rev].[status_code] <> @status_code_review_rev
	GROUP BY item_id) AS [review] ON pitem.item_id = review.item_id
/* Join ID: qst_review */
LEFT JOIN dbo.t_qst_review qst_review ON qst_review.item_id=pitem.item_id AND qst_review.login_name_created=@login AND qst_review.status_code !='del'
/* Join ID: inco */
LEFT JOIN dbo.t_buy_incoterm inco ON inco.inco_code = pitem.inco_code
/* Join ID: ship */
LEFT JOIN dbo.t_buy_shipping ship ON ship.ship_code = pitem.ship_code
/* Join ID: ai */
LEFT JOIN dbo.t_ord_account_product_item ai ON pitem.item_id = ai.item_id
/* Join ID: acc */
LEFT JOIN dbo.t_ord_account acc ON acc.acc_code = ai.acc_code AND acc.lcomp_id = ai.lcomp_id
/* Join ID: ctr */
LEFT JOIN dbo.t_ctr_contract ctr ON pitem.ctr_id = ctr.ctr_id
/* Join ID: punchout */
LEFT JOIN dbo.t_pun_requisition AS punchout ON punchout.preq_id = pitem.preq_id
WHERE pitem.item_id=@itemId";

			/*string sql = @"SELECT pitem.item_id, pitem.item_code, pitem.pdt_id, pitem.item_label_en AS item_label, pitem.sup_id, sup_name_en AS sup_name,
						pitem.item_sup_ref, pitem.unit_code, pitem.item_packaging, pitem.item_packaging_qty, pitem.item_public_price,
						pitem.item_nego_price, pitem.item_public_price_entry, pitem.item_nego_price_entry, pitem.item_weight,
						pitem.item_option, pitem.item_delivery_delay, pitem.ship_code, pitem.inco_code, 
					COALESCE(ctr.ctr_label_fr,ctr.ctr_label_en,ctr.ctr_label_de,ctr.ctr_label_it,ctr.ctr_label_pl,ctr.ctr_label_es,ctr.ctr_label_pt,
ctr.ctr_label_zh,ctr.ctr_label_ja,ctr.ctr_label_ar) AS ctr_label, ctr.status_code AS ctr_status_code, pitem.item_comment_en AS item_comment, pitem.item_inco_place, pitem.unit_code_currency,
						pitem.status_code, test.toto,
						COALESCE(ctr.ctr_effective_date, ctr.ctr_signature_date) AS ctr_begin_date
						FROM dbo.t_pdt_item pitem
					LEFT JOIN dbo.t_pdt_item replace_item ON replace_item.item_id=pitem.item_id_replace
					LEFT JOIN dbo.t_sup_supplier s ON s.sup_id=pitem.sup_id
					LEFT JOIN dbo.t_ctr_contract ctr ON ctr.ctr_id=pitem.ctr_id
					LEFT JOIN dbo.t_bas_unit u ON u.unit_code=pitem.unit_code_currency
					LEFT JOIN dbo.t_pun_requisition AS punchout ON punchout.preq_id = pitem.preq_id
					LEFT JOIN dbo.t_pun_requisition AS test ON test.preq_id = s.preq_id";*/

			SqlParser parser = new SqlParser();
			try
			{
				SqlStatement statement = parser.Parse(sql);
				ColumnPathResult test = parser.GetColumnPath(statement, "toto", "t_sup_supplier");
				test = parser.GetColumnPath(statement, "unit_short_label");
			}
			catch (ParseException ex)
			{
				Console.WriteLine($"Parse error: {ex.Message}");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error: {ex.Message}");
				Console.WriteLine(ex.StackTrace);
			}
		}
	}
}
