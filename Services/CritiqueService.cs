using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CHROMA.Models;

namespace CHROMA.Services;

/* ===FILE SUMMARY===
 * Runs the four Critique tests over a saved palette:
 *   1) Harmony spacing (hue geometry vs scheme)
 *   2) Contrast (WCAG 2.1)
 *   3) Similarity (ΔE in Lab)
 *   4) 60/30/10 balance (proportions)
 * 
 * Input: Palette metadata + its ColorItem rows from the DB.
 * Output: CritiqueReport with per-rule results + flattened summaries.
 */

public class CritiqueService
{
	public CritiqueReport EvaluatePalette(Palette palette, IList<ColorItem> colors)
	{
		if (palette == null) throw new ArgumentNullException(nameof(palette));
		if (colors == null) throw new ArgumentNullException(nameof(colors));

		var report = new CritiqueReport
		{
			PaletteId = palette.Id,
			PaletteName = string.IsNullOrWhiteSpace(palette.Name)
				? $"Palette {palette.Id}"
				: palette.Name,
			Scheme = palette.Scheme,
			CreatedUtc = DateTime.UtcNow
		};

		// Run each test
		var spacing = EvaluateHarmonySpacing(palette, colors);
		var contrast = EvaluateContrast(colors);
		var similarity = EvaluateSimilarity(palette, colors);
		var balance = EvaluateBalance601010(colors);

		report.RuleResults.AddRange(new[]
		{
			spacing, contrast, similarity, balance
		});

		// Overall score = average of rule scores
		report.OverallScore = (int)Math.Round(
			report.RuleResults.Average(r => r.Score));

		// Flatten per-rule headlines into the persisted summary fields.
		report.HarmonySpacing = spacing.Message;
		report.Contrast = contrast.Message;
		report.DeltaE = similarity.Message;
		report.Balance = balance.Message;

		// Compose a short overall summary for history.
		report.Summary = BuildOverallSummary(report);

		return report;
	}


	// -------- 4.1 Harmony spacing ----------------------------------------

	/* ===SUMMARY===
	 * This test checks whether the colors in a palette follow
	 * a pleasing, intentional arrangement on the color wheel
	 * (e.g., they're "spaced out" appropriately).
	 * 
	 * Done by comparing the palette's actual hue angles
	 * and the ideal angles for the scheme it's based on.
	 * Based on this comparison, the test "grades"/scores the palette
	 * on how well it fits its intended harmony.
	 */

	private CritiqueRuleResult EvaluateHarmonySpacing(Palette palette, IList<ColorItem> colors)
	{
		if (colors.Count == 0)
		{
			return new CritiqueRuleResult
			{
				Rule = CritiqueRuleType.HarmonySpacing,
				Score = 0,
				Severity = CritiqueSeverity.Info,
				Message = "No colors to analyze for harmony spacing.",
				Details = "Save a palette with at least one color before running critique."
			};
		}

		// Use stored H (0–360) directly; first color is the "base".
		var actualHues = colors.Select(c => c.H).ToList();
		double baseHue = actualHues[0];

		double[] offsets = GetExpectedOffsetsForScheme(palette.Scheme);
		var expectedHues = offsets
			.Select(off => NormalizeHue(baseHue + off))
			.ToArray();

		// For each expected hue, find the nearest actual hue and measure deviation.
		var deviations = new List<double>();
		foreach (var expected in expectedHues)
		{
			double best = double.MaxValue;
			foreach (var actual in actualHues)
			{
				double d = HueDistanceDegrees(expected, actual);
				if (d < best) best = d;
			}
			deviations.Add(best);
		}

		double maxDev = deviations.Count > 0 ? deviations.Max() : 0;
		double avgDev = deviations.Count > 0 ? deviations.Average() : 0;

		int score;
		CritiqueSeverity sev;
		string msg;

		if (maxDev <= 8)
		{
			score = 100;
			sev = CritiqueSeverity.Info;
			msg = "Hues closely follow the chosen harmony pattern.";
		}
		else if (maxDev <= 15)
		{
			score = 80;
			sev = CritiqueSeverity.Info;
			msg = "Hues roughly match the harmony pattern with minor drift.";
		}
		else if (maxDev <= 30)
		{
			score = 60;
			sev = CritiqueSeverity.Warning;
			msg = "Hues are somewhat uneven for this harmony; consider tightening spacing.";
		}
		else
		{
			score = 30;
			sev = CritiqueSeverity.Error;
			msg = "Hues are far from the ideal pattern for this harmony.";
		}

		var details =
			$"Largest hue deviation: {maxDev:F1}°; average deviation: {avgDev:F1}°.\n" +
			$"Expected hues (approx): {string.Join(", ", expectedHues.Select(h => $"{h:F0}°"))}.";

		return new CritiqueRuleResult
		{
			Rule = CritiqueRuleType.HarmonySpacing,
			Score = score,
			Severity = sev,
			Message = msg,
			Details = details
		};
	}

	static double[] GetExpectedOffsetsForScheme(string scheme)
	{
		// Match the harmony math used when generating palettes. 
		return scheme switch
		{
			"Monochromatic" => new[] { 0.0 },
			"Analogous" => new[] { -30.0, 0.0, 30.0 },
			"Complementary" => new[] { 0.0, 180.0 },
			"Split-Complementary" => new[] { 0.0, 150.0, 210.0 }, // 180° ± 30°
			"Triadic" => new[] { 0.0, 120.0, 240.0 },
			"Tetradic" => new[] { 0.0, 90.0, 180.0, 270.0 },
			_ => new[] { 0.0 }
		};
	}

	static double NormalizeHue(double h)
	{
		h %= 360.0;
		if (h < 0) h += 360.0;
		return h;
	}

	static double HueDistanceDegrees(double a, double b)
	{
		double diff = Math.Abs(a - b) % 360.0;
		return diff > 180.0 ? 360.0 - diff : diff;
	}


	// -------- 4.2 Contrast (WCAG) ----------------------------------------

	/* ===SUMMARY===
	 * Following Web Content Accessibility Guidelines, this test determines
	 * whether text placed over a given color background would be readable.
	 * 
	 * Key contrast ratios between text and background are the following:
	 *	- 4.5:1 for normal text
	 *	- 3:1 for for large text
	 *	- 7:1 for enhanced accessibility
	 *	These ratios are computed from how bright/dark the colors are
	 *	after converting them to a standardized luminance scale.
	 */

	private CritiqueRuleResult EvaluateContrast(IList<ColorItem> colors)
	{
		var unsafePairs = new List<(ColorItem fg, ColorItem bg, double ratio)>();
		var largeTextOnlyPairs = new List<(ColorItem fg, ColorItem bg, double ratio)>();
		var okPairs = new List<(ColorItem fg, ColorItem bg, double ratio)>();

		double worst = double.MaxValue;

		for (int i = 0; i < colors.Count; i++)
		{
			for (int j = 0; j < colors.Count; j++)
			{
				if (i == j) continue;

				var fg = colors[i];
				var bg = colors[j];

				double ratio = ColorMathService.GetContrastRatio(fg.Hex, bg.Hex);
				if (ratio < worst) worst = ratio;

				if (ratio < 3.0)
					unsafePairs.Add((fg, bg, ratio));
				else if (ratio < 4.5)
					largeTextOnlyPairs.Add((fg, bg, ratio));
				else
					okPairs.Add((fg, bg, ratio));
			}
		}

		int score;
		CritiqueSeverity sev;
		string msg;

		if (unsafePairs.Count == 0 && largeTextOnlyPairs.Count == 0)
		{
			score = 100;
			sev = CritiqueSeverity.Info;
			msg = "All color pairs meet WCAG contrast for normal text.";
		}
		else if (unsafePairs.Count == 0 && largeTextOnlyPairs.Count > 0)
		{
			score = 80;
			sev = CritiqueSeverity.Info;
			msg = "Some pairs are only suitable for large text; others are fully accessible.";
		}
		else
		{
			score = 40;
			sev = CritiqueSeverity.Warning;
			msg = "Several color pairs have poor contrast and should not be used for text.";
		}

		var details =
			$"Worst contrast ratio in this palette: {worst:F2}:1.\n" +
			$"Safe pairs (normal text): {okPairs.Count}, " +
			$"large-text-only pairs: {largeTextOnlyPairs.Count}, " +
			$"unsafe pairs: {unsafePairs.Count}.";

		return new CritiqueRuleResult
		{
			Rule = CritiqueRuleType.Contrast,
			Score = score,
			Severity = sev,
			Message = msg,
			Details = details,
			Payload = new { unsafePairs, largeTextOnlyPairs, okPairs }
		};
	}


	// -------- 4.3 Similarity (ΔE) ----------------------------------------

	/* ===SUMMARY===
	 * This test determines measures how different two colors look to the human eye.
	 * 
	 * CIELAB is utilized here; it's a color space designed to separate lightness
	 * from color informationn, based on how color is perceived.
	 * It has 3 components:
     *		L = Perceptual lightness (0 = black, 100 ≈ diffuse white)
	 *		A = Green–red axis (negative = green, positive = red)
	 *		B = Blue–yellow axis (negative = blue, positive = yellow)
	 *
	 * ΔE (“Delta E”) is essentially the distance between two colors in Lab space
	 * (i.e., how different they appear).
	 * ΔE = 
	 *		ΔE ≈ 1    → barely distinguishable
	 *		ΔE ≈ 5    → noticeable difference
	 *		ΔE  > 20  → very different
	 *
	 * If colors in the palette appear too similar based on this test, they will be flagged.
	 */

	private CritiqueRuleResult EvaluateSimilarity(Palette palette, IList<ColorItem> colors)
	{
		var tooSimilar = new List<(ColorItem c1, ColorItem c2, double deltaE)>();
		double minDelta = double.MaxValue;

		for (int i = 0; i < colors.Count; i++)
		{
			for (int j = i + 1; j < colors.Count; j++)
			{
				var c1 = colors[i];
				var c2 = colors[j];

				double dE = ColorMathService.DeltaE76(c1.Hex, c2.Hex);
				if (dE < minDelta) minDelta = dE;

				// Threshold depends on harmony type: monochromatic is allowed to be closer.
				double threshold = palette.Scheme == "Monochromatic" ? 5.0 : 10.0;

				if (dE < threshold)
				{
					tooSimilar.Add((c1, c2, dE));
				}
			}
		}

		if (double.IsPositiveInfinity(minDelta) || double.IsNaN(minDelta))
			minDelta = 0;

		int score;
		CritiqueSeverity sev;
		string msg;

		if (tooSimilar.Count == 0)
		{
			score = 100;
			sev = CritiqueSeverity.Info;
			msg = "All palette colors are perceptually distinct.";
		}
		else if (tooSimilar.Count <= 2)
		{
			score = 70;
			sev = CritiqueSeverity.Warning;
			msg = "A few colors are very similar and could be merged or repurposed.";
		}
		else
		{
			score = 40;
			sev = CritiqueSeverity.Error;
			msg = "Several colors are so similar that they may be visually redundant.";
		}

		var details =
			$"Minimum ΔE between any pair: {minDelta:F1}.\n" +
			$"Pairs flagged as very similar: {tooSimilar.Count}.";

		return new CritiqueRuleResult
		{
			Rule = CritiqueRuleType.Similarity,
			Score = score,
			Severity = sev,
			Message = msg,
			Details = details,
			Payload = tooSimilar
		};
	}


	// -------- 4.4 60/30/10 balance ---------------------------------------

	/* ===SUMMARY===
	 * This test checks whether a palette supports a classic proportion rule
	 * used more commonly in character design and interior decoration,
	 * known as the 60/30/10 Rule.
     *     60% = dominant color
     *     30% = secondary color
     *     10% = accent color
	 * 
	 * This section evaluates the following:
	 *   - If the palette suggests any natural dominant/secondary/accent hierarchy
	 *   - If the colors have the right contrast or weight to fulfill that
	 *   - If the palette aligns with the heuristic if proportions are assigned by user
	 */

	private CritiqueRuleResult EvaluateBalance601010(IList<ColorItem> colors)
	{
		// Sum of proportions; Visual tab will eventually populate these.
		double total = colors.Sum(c => c.Proportion);

		if (total <= 0.0)
		{
			return new CritiqueRuleResult
			{
				Rule = CritiqueRuleType.Balance601010,
				Score = 50,
				Severity = CritiqueSeverity.Info,
				Message = "Color usage proportions are not set yet.",
				Details = "Once you assign usage percentages (e.g., in the Visual tab), " +
						  "this 60/30/10 check will give more specific guidance."
			};
		}

		// Normalized proportions, sorted descending.
		var normalized = colors
			.Select(c => c.Proportion / total)
			.OrderByDescending(p => p)
			.ToList();

		// Ideal 60/30/10
		double[] target = { 0.6, 0.3, 0.1 };

		double[] actual = new double[3];
		for (int i = 0; i < 3; i++)
		{
			actual[i] = i < normalized.Count ? normalized[i] : 0.0;
		}

		double avgDiff = (
			Math.Abs(actual[0] - target[0]) +
			Math.Abs(actual[1] - target[1]) +
			Math.Abs(actual[2] - target[2])) / 3.0;

		// If avg deviation is 0 → score 100; if 0.3 → ~10, etc. 
		int score = (int)Math.Round(100 * Math.Max(0, 1 - avgDiff / 0.3));

		CritiqueSeverity sev;
		string msg;

		if (avgDiff <= 0.07)
		{
			sev = CritiqueSeverity.Info;
			msg = "Palette proportions closely follow the 60/30/10 guideline.";
		}
		else if (avgDiff <= 0.15)
		{
			sev = CritiqueSeverity.Warning;
			msg = "Palette proportions are somewhat unbalanced compared to 60/30/10.";
		}
		else
		{
			sev = CritiqueSeverity.Error;
			msg = "Palette is heavily skewed; consider adjusting towards 60/30/10.";
		}

		var details =
			$"Current top three proportions: {actual[0]:P0}, {actual[1]:P0}, {actual[2]:P0}.\n" +
			$"Ideal: 60% / 30% / 10%.";

		return new CritiqueRuleResult
		{
			Rule = CritiqueRuleType.Balance601010,
			Score = score,
			Severity = sev,
			Message = msg,
			Details = details,
			Payload = new { actual, target }
		};
	}


	// -------- 4.5 Summary builder ----------------------------------------

	/* ===SUMMARY===
	 * Takes all the tests above and condenses it for UI visibility
	 */

	private static string BuildOverallSummary(CritiqueReport report)
	{
		if (report.RuleResults.Count == 0)
			return "No critique rules were run.";

		var worstRule = report.RuleResults
			.OrderBy(r => r.Score)
			.First();

		string headline = worstRule.Severity switch
		{
			CritiqueSeverity.Info => "Strong palette overall with only minor tweaks suggested.",
			CritiqueSeverity.Warning => "Palette is usable but has some areas to refine.",
			CritiqueSeverity.Error => "Palette has significant issues that should be fixed.",
			_ => "Palette critique results available."
		};

		return $"{headline} (Overall score: {report.OverallScore}/100.)";
	}
}