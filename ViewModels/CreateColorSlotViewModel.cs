using CHROMA.Services;
using Microsoft.Maui.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CHROMA.ViewModels;

public class ColorSlotViewModel : ObservableObject
{
	readonly double _hue;

	double _saturation;
	double _lightness;
	Color _color;
	string _label;

	public ColorSlotViewModel(string label, HSLColor hsl)
	{
		_label = label;
		_hue = hsl.H;
		_saturation = hsl.S;
		_lightness = hsl.L;
		_color = ColorMathService.FromHSL(hsl);
	}

	public string Label
	{
		get => _label;
		set => SetProperty(ref _label, value, nameof(Label));
	}

	/* ===SUMMARY===
    * Bound to a BoxView or Border.
    */
	public Color Color
	{
		get => _color;
		private set => SetProperty(ref _color, value, nameof(Color));
	}

	/* ===SUMMARY===
    * S ∈ [0,1]. Only this + Lightness are tweakable by user.
    */
	public double Saturation
	{
		get => _saturation;
		set
		{
			if (SetProperty(ref _saturation, Clamp01(value), nameof(Saturation)))
				UpdateColor();
		}
	}

	/* ===SUMMARY===
    * L ∈ [0,1]. Only this + Saturation are tweakable by user.
    */
	public double Lightness
	{
		get => _lightness;
		set
		{
			if (SetProperty(ref _lightness, Clamp01(value), nameof(Lightness)))
				UpdateColor();
		}
	}

	public HSLColor ToHSL()
	{
		return new HSLColor(_hue, _saturation, _lightness);
	}

	void UpdateColor()
	{
		// NOTE: hue is intentionally not exposed or changed here.
		Color = ColorMathService.FromHSL(new HSLColor(_hue, _saturation, _lightness));
	}

	static double Clamp01(double v) {
	    if (v < 0) {  return 0; }
		if (v > 1) { return 1; }
		return v;
	}
}