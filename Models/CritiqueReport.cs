using System;
using SQLite;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CHROMA.Models;

/* ===SUMMARY===
*  Persisted critique result for a palette
*/
public class CritiqueReport
{
	[PrimaryKey, AutoIncrement]
	public int ReportId { get; set; }

	// Foreign key to the criticized palette
	[Indexed]
	public int PaletteId { get; set; }

	public string FeedbackSummary { get; set; } = string.Empty;
	public string FeedbackSpacing { get; set; } = string.Empty;
	public string FeedbackContrast { get; set; } = string.Empty;
	public string FeedbackDeltaE { get; set; } = string.Empty;
	public string FeedbackBalance { get; set; } = string.Empty;

}