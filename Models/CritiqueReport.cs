using System;
using SQLite;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CHROMA.Models;

/* ===SUMMARY===
 * Persisted critique result for a palette
 * 
 * Has 2 roles: Database row (Id, summary fields, etc.)
 * and In-memory result object with per-RuleResults.
 */
public class CritiqueReport
{
	[PrimaryKey, AutoIncrement]
	public int Id { get; set; }

	// Foreign key to the criticized palette
	[Indexed]
	public int PaletteId { get; set; }

	// Optional name + scheme for easier history display
	public string PaletteName { get; set; } = string.Empty;
	public string Scheme { get; set; } = string.Empty;

	// Overall numeric score (0-100), e.g. average rule scores
	public int OverallScore { get; set; }

	// Short overall summary line
	public string Summary { get; set; } = string.Empty;

	// Per-test headline summaries (flattened from RuleResults)
	public string HarmonySpacing { get; set; } = string.Empty;
	public string Contrast { get; set; } = string.Empty;
	public string DeltaE { get; set; } = string.Empty;
	public string Balance { get; set; } = string.Empty;

	public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

	// Detailed per-rule results (not persisted; UI-only).
	[Ignore]
	public List<CritiqueRuleResult> RuleResults { get; set; } = new();
}

// === Critique rule metadata (non-persisted) ==========================

public enum CritiqueRuleType
{
	HarmonySpacing,
	Contrast,
	Similarity,
	Balance601010
}

public enum CritiqueSeverity
{
	Info,
	Warning,
	Error
}

public class CritiqueRuleResult
{
	public CritiqueRuleType Rule { get; set; }

	// 0–100 “score” (higher is better).
	public int Score { get; set; }

	public CritiqueSeverity Severity { get; set; }

	// Short headline used in the UI.
	public string Message { get; set; } = string.Empty;

	// Optional extra explanation; you can surface this later in expandable panels.
	public string Details { get; set; } = string.Empty;

	// Optional structured payload (e.g., list of problem pairs). Not used yet.
	public object? Payload { get; set; }
}