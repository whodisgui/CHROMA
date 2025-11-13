using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Maui.Graphics;

namespace CHROMA.Services;

/* ===SUMMARY===
* Simple HSL Color Struct for Internal Math
* Hue (H) in [0, 360),
* Saturation (S) and Lightness (L) in [0,1].
*/
public readonly struct HSLColor
{
	public double H { get; }  // H = Hue Value
    public double S { get; }  // S = Saturation Value
	public double L { get; }  // L = Lightness Value

	public HSLColor(double h, double s, double l)
	{
		H = NormalizeHue(h);
		S = Clamp01(s);
		L = Clamp01(l);
	}

	internal static double NormalizeHue(double h)
	{
		h %= 360.0;
		if (h < 0) { h += 360.0; }
		return h;
	}

	internal static double Clamp01(double v)
	{
		if (v < 0) { return 0; }
		else if (v > 1) { return 1; }
		else { return v; }
	}

	public HSLColor WithSL(double s, double l)
	{
		return new HSLColor(H, s, l);
	}

	public override string ToString()
	{
		return $"H:{H:0},{S:0.00},{L:0.00}";
	}
}

public static class ColorMathService
{
	/* ===SUMMARY===
	* Parses a Hex string (RRGGBB or #RRGGBB) to a color.
	* If invalid, returns false.
	*/
	public static bool TryParseHex(string? hex, out Color color)
	{
		color = Colors.Transparent;
		if (string.IsNullOrWhiteSpace(hex)) { return false; }

		hex = hex.Trim();
		if (hex.StartsWith("#")) { hex = hex[1..]; }
		if (hex.Length != 6) { return false; }

		if(!int.TryParse(hex[..2], System.Globalization.NumberStyles.HexNumber, null, out var rByte) ||
		   !int.TryParse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out var gByte) ||
		   !int.TryParse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out var bByte))
		{
			return false;
		}

		color = new Color((float)(rByte / 255.0), (float)(gByte / 255.0), (float)(bByte / 255.0));
		return true;
	}

	//Translates Color code to Hex
	public static string ToHex(Color color)
	{
		var r = (int)Math.Round(color.Red * 255);
		var g = (int)Math.Round(color.Green * 255);
		var b =	(int)Math.Round(color.Blue * 255);
		return $"#{r:X2}{g:X2}{b:X2}";
	}

	/* ===SUMMARY===
	* Converts RGB Color to HSL (Standard Definition) 
	*/
	public static HSLColor ToHSL(Color color)
	{
		double r = color.Red;
        double g = color.Green;
        double b = color.Blue;

		double max = Math.Max(r, Math.Max(g, b));
		double min = Math.Min(r, Math.Min(g, b));
		double c = max - min;
		double l = (max + min) / 2.0;
		double h;
		double s;

		if (c == 0) {  h = 0; }
		else if (max == r) { h = 60.0 * (((g - b) / c) % 6.0); }
		else if (max == g) { h = 60.0 * (((b - r) / c) + 2.0); }
		else /*if (max == b)*/ { h = 60.0 * (((r - g) / c) + 4.0); }

		if (h < 0) { h += 360.0; }

		if (c == 0) {  s = 0; }
		else
		{
			s = c / (1 - Math.Abs(2 * l - 1));
		}

		return new HSLColor(h, s, l);
	}

	/* ===SUMMARY===
    * Converts HSL to RGB Color using standard hexcone math.
    */
	public static Color FromHSL(HSLColor hsl)
	{

		double h = HSLColor.NormalizeHue(hsl.H);
		double s = HSLColor.Clamp01(hsl.S);
		double l = HSLColor.Clamp01(hsl.L);

		double c = (1 - Math.Abs(2 * l - 1)) * s;
		double hPrime = h / 60.0;
        double x = c * (1 - Math.Abs(hPrime % 2 - 1));

		double r1, g1, b1;
		if (hPrime < 1) { (r1, g1, b1) = (c, x, 0); }
		else if (hPrime < 2) { (r1, g1, b1) = (x, c, 0); }
		else if (hPrime < 3) { (r1, g1, b1) = (0, c, x); }
		else if (hPrime < 4) { (r1, g1, b1) = (0, x, c); }
		else if (hPrime < 5) { (r1, g1, b1) = (x, 0, c); }
		else { (r1, g1, b1) = (c, 0, x); }

		double m = 1 - c / 2.0;
		double r = r1 + m;
		double g = g1 + m;
		double b = b1 + m;

		return new Color((float)r, (float)g, (float)b);
	}
}


/* ===SUMMARY===
* The 6 Color Harmony Schemes that can be used on the CreatePage
*/
public enum HarmonyScheme
{
	Monochromatic,
	Complementary,
	SplitComplementary,
	Analogous,
	Triadic,
	Tetradic
}

/* ===SUMMARY===
* Generates Complementing colors based on each Color Harmony Scheme
*/
public static class HarmonyGenerator
{

	public static HSLColor[] Generate(HSLColor baseColor, HarmonyScheme scheme)
	{
		switch (scheme)
		{
			case HarmonyScheme.Monochromatic:
				// Same hue; vary S and L but keep them in a usable range
				return new[]
				{
					baseColor.WithSL(baseColor.S * 0.3, ClampMid(baseColor.L * 0.6)),
					baseColor,
					baseColor.WithSL(baseColor.S * 0.8, ClampMid(baseColor.L * 1.2))
				};

			case HarmonyScheme.Complementary:
				// Base hue and its direct opposite (180° apart) on the color wheel
				return new[]
				{
					baseColor,
					new HSLColor(baseColor.H + 180, baseColor.S, baseColor.L)
				};

			case HarmonyScheme.SplitComplementary:
				// Base hue plus two hues around its complement (±30° from 180°)
				return new[]
				{
					baseColor,
					new HSLColor(baseColor.H + 150, baseColor.S, baseColor.L),
					new HSLColor(baseColor.H + 210, baseColor.S, baseColor.L)
				};

			case HarmonyScheme.Analogous:
				// Neighboring hues around the base (±30°) for smooth transitions
				return new[]
				{
					baseColor,
					new HSLColor(baseColor.H + 30, baseColor.S, baseColor.L),
					new HSLColor(baseColor.H - 30, baseColor.S, baseColor.L)
				};

			case HarmonyScheme.Triadic:
				// Three hues equally spaced (120° apart) for balanced contrast
				return new[]
				{
					baseColor,
					new HSLColor(baseColor.H + 120, baseColor.S, baseColor.L),
					new HSLColor(baseColor.H + 240, baseColor.S, baseColor.L)
				};

			case HarmonyScheme.Tetradic:
				// Simple rectangle: base, base+90, base+180, base+270
				return new[]
				{
					baseColor,
					new HSLColor(baseColor.H + 90, baseColor.S, baseColor.L),
					new HSLColor(baseColor.H + 180, baseColor.S, baseColor.L),
					new HSLColor(baseColor.H + 270, baseColor.S, baseColor.L)
				};

			default:
				return new[] { baseColor };
		}
	}

	static double ClampMid(double v)
	{
		if (v < 0) { v = 0; }
		else if (v > 1) { v = 1; }
		
		return v;
	}
}