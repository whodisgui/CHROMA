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

	string _baseHex = string.Empty;
	Color _baseColor = Colors.Transparent;
    string _inputMessage = string.Empty;
    string _paletteMessage = string.Empty;

    public string BaseHex
    {
        get => _baseHex;
        set
        {
			if (SetProperty(ref _baseHex, value, nameof(BaseHex)))
			{
				OnPropertyChanged(nameof(BaseHexPreview));
			}
		}
    }

    public string BaseHexPreview => $"Current: {BaseHex}";
    
    public Color BaseColor
    {
        get => _baseColor;
        private set => SetProperty(ref _baseColor, value, nameof(BaseColor));
    }

    public string InputMessage
    {
        get => _inputMessage;
        private set => SetProperty(ref _inputMessage, value, nameof(InputMessage));
    }

    public string PaletteMessage
    {
        get => _paletteMessage;
        private set => SetProperty(ref _paletteMessage, value, nameof(PaletteMessage));
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

                ResetInputs();
			}
        }
    }

    // Convenience bools for XAML visibility
    public bool _hasValidBaseColor = false;
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
    public ICommand ApplyInputCommand => new Command(ApplyInput);
    public ICommand ResetCommand => new Command(ResetInputs);


	// ==== CORE LOGIC ==========================================

    void UpdateBaseFromHex()
    {
		if (!ColorMathService.TryParseHex(BaseHex, out var color))
        {
            InputMessage = "Invalid HEX Color. Expected format = '#RRGGBB'; values = 0-9 and A-F.";
            return;
        }

        InputMessage = string.Empty;
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

        InputMessage = string.Empty;
        BaseColor = color;
        GeneratePalette();
    }

    void GeneratePalette()
    {
        if (!_hasValidBaseColor)
        {
            PaletteMessage = "Please enter and apply a valid base color first.";
            return;
        }

        var color = BaseColor;
        var baseHSL = ColorMathService.ToHSL(color);

        if (IsHexMode)
        {
            if (!ColorMathService.TryParseHex(BaseHex, out color))
            {
                PaletteMessage = "Cannot generate palette - invalid base HEX.";
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

        PaletteMessage = $"Generated {Palette.Count} colors using {SelectedScheme}.";
		ExportJson = string.Empty;
	}

    void Save()
    {
		// Hook point for JSON file save later. For now, just acknowledge the action.
		InputMessage = "Palette saved (stub - wire to JSON file save/load next).";
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

		PaletteMessage = "Export JSON prepared. (Copy from the text box or wire to file-save next.)";
	}

    void ApplyInput()
    {
        // Treat "Enter" as: validate/apply current input and regenerate.
        if (IsHexMode)
        {
            if (!ColorMathService.TryParseHex(BaseHex, out var color))
            {
                InputMessage = "Invalid HEX Color. Expected format = '#RRGGBB'; values = 0-9 and A-F.";
                _hasValidBaseColor = false;
                return;
            }

            BaseColor = color;
            _hasValidBaseColor = true;
            InputMessage = "Base color set from HEX.";
        }
        else  // HSV mode
        {
            var hsv = new HSVColor(
                HSV_Hue,
                HSV_Saturation / 100.0,
                HSV_Value / 100.0);

            // Range checks (e.g. 0-360, 0-100)
            BaseColor = ColorMathService.FromHSV(hsv);
            _hasValidBaseColor = true;
            InputMessage = "Base color set from HSV.";
        }

        // When input changes, palette feedback no longer applies
        PaletteMessage = string.Empty;
    }

    void ResetInputs()
    {
		// Reset to defaults
		BaseHex = string.Empty;
        HSV_Hue = 0;
        HSV_Saturation = 0;
        HSV_Value = 0;

        Palette.Clear();
        ExportJson = string.Empty;
        InputMessage = "Inputs reset. Enter a new color to generate a palette.";
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