using CHROMA.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.Arm;
using System.Text.Json;
using System.Windows.Input;

namespace CHROMA.ViewModels;

public class CreateViewModel : BaseViewModel
{
	// ==== INPUT HERE ==========================================

	string _baseHex = "#E96841"; //Sample Starter Color Code
	Color _baseColor = Colors.Orange;
	string _statusMessage = string.Empty;

    public string BaseHex
    {
        get => _baseHex;
        set
        {
			if (SetProperty(ref _baseHex, value, nameof(BaseHex)))
			{
				OnPropertyChanged(nameof(BaseHexPreview));
                if (IsHexMode) { UpdateBaseFromHex(); }
			}
		}
    }

    public string BaseHexPreview => $"Current: {BaseHex}";
    
    public Color BaseColor
    {
        get => _baseColor;
        private set => SetProperty(ref _baseColor, value, nameof(BaseColor));
    }

	public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value, nameof(StatusMessage));
    }


	// ==== INPUT HERE (Hex or HSV) =============================
	
    public ObservableCollection<string> InputModes { get; } = new(new[] { "HEX", "HSV" });

    string _selectedInputMode = "HEX";
    public string SelectedInputMode
    {
        get => _selectedInputMode;
        set
        {
            if (SetProperty(ref _selectedInputMode, value, nameof(SelectedInputMode)))
            {
				// Notify XAML visibility bindings
				OnPropertyChanged(nameof(IsHexMode));
				OnPropertyChanged(nameof(IsHSVMode));
			}
        }
    }

    // Convenience bools for XAML visibility
    public bool IsHexMode => SelectedInputMode == "HEX";
    public bool IsHSVMode => SelectedInputMode == "HSV";


    // ==== HSV INPUT (when SelectedInputMode == "HSV") =========

    double _hsvHue;
    double _hsvSaturation = 100; // user-facing % 0-100
    double _hsvValue = 100;      // user-facing % 0-100

    public double HSV_Hue
    {
        get => _hsvHue;
        set => SetProperty(ref _hsvHue, value, nameof(HSV_Hue));
    }

	public double HSV_Saturation
	{
		get => _hsvSaturation;
		set => SetProperty(ref _hsvSaturation, value, nameof(HSV_Saturation));
	}

	public double HSV_Value
	{
		get => _hsvValue;
		set => SetProperty(ref _hsvValue, value, nameof(HSV_Value));
	}


	// ==== SCHEME SELECTION ====================================

	public ObservableCollection<string> Schemes { get; } =
        new(new[]{
			"Monochromatic",
            "Complementary",
			"Split-Complementary",
            "Analogous",
            "Triadic",
            "Tetradic",
        });

	string _selectedScheme = "Monochromatic"; //Sample Starter Chosen Scheme

    public string SelectedScheme
    {
		get => _selectedScheme;
        set
        {
			if (SetProperty(ref _selectedScheme, value, nameof(SelectedScheme)))
            {
                GeneratePalette();
            }
		}
	}


	// ==== GENERATED PALETTE + EXPORT ==========================

	public ObservableCollection<ColorSlotViewModel> Palette { get; } = new();

	string _exportJson = string.Empty;
    public string ExportJson
    {
        get => _exportJson;
        private set => SetProperty(ref _exportJson, value, nameof(ExportJson));
    }

    // Simple 60/30/10 suggestion based on palette size (FR3/FR4 hook). :contentReference[oaicite:9]{index=9}
    public double PrimaryRatio => 0.6;
    public double SecondaryRatio => 0.3;
    public double AccentRatio => 0.1;


	// ==== COMMANDS ============================================

	public ICommand GenerateCommand => new Command(GeneratePalette);
	public ICommand SaveCommand => new Command(Save);
	public ICommand ExportJsonCommand => new Command(ExportPaletteJson);


	// ==== CORE LOGIC ==========================================

    void UpdateBaseFromHex()
    {
		if (!ColorMathService.TryParseHex(BaseHex, out var color))
        {
            StatusMessage = "Invalid HEX Color. Expected format = #RRGGBB.";
            return;
        }

        StatusMessage = string.Empty;
        BaseColor = color;
		GeneratePalette();
	}

    void UpdateBaseFromHSV()
    {
        var hsv = new HSVColor(
            HSV_Hue,
            HSV_Saturation / 100.0,
            HSV_Value / 100.0);
        // Interpret Saturation / Value as percentages 0-100

        var color = ColorMathService.FromHSV(hsv);

        StatusMessage = string.Empty;
        BaseColor = color;
        GeneratePalette();
    }

    void GeneratePalette()
    {
        Color color;

        if (IsHexMode)
        {
            if (!ColorMathService.TryParseHex(BaseHex, out color))
            {
                StatusMessage = "Cannot generate palette - invalid base HEX.";
                return;
            }
        }
        else // HSV mode
        {
            var hsv = new HSVColor(
                HSV_Hue,
                HSV_Saturation / 100.0,
				HSV_Value / 100.0);

            color = ColorMathService.FromHSV(hsv);
        }

        var baseHSL = ColorMathService.ToHSL(color);

        var scheme = SelectedScheme switch
        {
            "Monochromatic"       => HarmonyScheme.Monochromatic,
            "Complementary"       => HarmonyScheme.Complementary,
			"Split-Complementary" => HarmonyScheme.SplitComplementary,
            "Analogous"           => HarmonyScheme.Analogous,
            "Triadic"             => HarmonyScheme.Triadic,
            "Tetradic"            => HarmonyScheme.Tetradic,
			_                     => HarmonyScheme.Monochromatic
		};

		var generated = HarmonyGenerator.Generate(baseHSL, scheme);

		Palette.Clear();

        for (int i = 0; i < generated.Length; i++)
        {
            string label = generated.Length == 2 && i == 1
                ? "Complement"
                : $"Color {i + 1}";

            Palette.Add(new ColorSlotViewModel(label, generated[i]));
		}

        StatusMessage = $"Generated {Palette.Count} colors using {SelectedScheme}.";
		ExportJson = string.Empty; // reset previous export
	}

    void Save()
    {
		// Hook point for JSON file save later. For now, just acknowledge the action.
		StatusMessage = "Palette saved (stub - wire to JSON file save/load next).";
	}

    void ExportPaletteJson()
    {
		var hexList = Palette
			.Select(p => ColorMathService.ToHex(p.Color))
            .ToArray();

        ExportJson = JsonSerializer.Serialize(hexList, new JsonSerializerOptions
        {
            WriteIndented = true
        });

		StatusMessage = "Export JSON prepared. (Copy from the text box or wire to file-save next.)";
	}
}

// Minimal base classes
public class BaseViewModel : ObservableObject { }
public class ObservableObject : System.ComponentModel.INotifyPropertyChanged
{
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
	protected bool SetProperty<T>(ref T storage, T value, string? propertyName = null)
	{
		if (EqualityComparer<T>.Default.Equals(storage, value)) return false;
		storage = value;
		OnPropertyChanged(propertyName);
		return true;
    }

	protected void OnPropertyChanged(string? propertyName)
    {
		PropertyChanged?.Invoke(
            this,
			new System.ComponentModel.PropertyChangedEventArgs(propertyName ?? string.Empty));
	}
}