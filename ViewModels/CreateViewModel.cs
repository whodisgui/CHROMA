using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Maui.Graphics;

namespace CHROMA.ViewModels;

public class CreateViewModel : BaseViewModel
{
    public string BaseHex { get => _baseHex; set { SetProperty(ref _baseHex, value); UpdateBase(); } }
    string _baseHex = "#E96841";
    public string BaseHexPreview => $"Current: {BaseHex}";
    public Color BaseColor { get => _baseColor; set => SetProperty(ref _baseColor, value); }
    Color _baseColor = Colors.Orange;

    public ObservableCollection<string> Schemes { get; } = new(new []{ "Complementary","Analogous","Triadic","Split-Complementary","Tetradic","Monochromatic"});
    public string SelectedScheme { get => _selectedScheme; set { SetProperty(ref _selectedScheme, value); Generate(); } }
    string _selectedScheme = "Complementary";

    public ObservableCollection<PaletteVM> GeneratedPalettes { get; } = new();
    public PaletteVM? ActivePalette { get => _active; set => SetProperty(ref _active, value); }
    PaletteVM? _active;

    public DistributionVM Dist { get; } = new(){ MainPct="6*", SecondaryPct="3*", AccentPct="1*"};

    public ICommand ClearCommand => new Command(()=> BaseHex = "");
    public ICommand UsePaletteCommand => new Command<PaletteVM>(p => ActivePalette = p);
    public ICommand SaveCommand => new Command(() => {});
    public ICommand ExportJsonCommand => new Command(() => {});

    void UpdateBase()
    {
        try { BaseColor = Color.FromArgb(BaseHex); }
        catch { /* ignore */ }
        Generate();
    }

    void Generate()
    {
        GeneratedPalettes.Clear();
        // Stub: generate a simple 3-color palette using shifts; replace with real service later
        var baseC = BaseColor;
        var p = new PaletteVM("Sample " + SelectedScheme, new []{ baseC, Colors.SkyBlue, Colors.Coral });
        GeneratedPalettes.Add(p);
    }
}

public class PaletteVM : ObservableObject
{
    public string Name { get; }
    public List<Color> Colors { get; }
    public double Saturation { get => _s; set => SetProperty(ref _s, value); }
    public double Lightness { get => _l; set => SetProperty(ref _l, value); }
    double _s = 1.0, _l = 0.5;
    public PaletteVM(string name, IEnumerable<Color> colors) { Name = name; Colors = colors.ToList(); }
}

public class DistributionVM : ObservableObject
{
    public string MainPct { get => _m; set => SetProperty(ref _m, value); }
    public string SecondaryPct { get => _s; set => SetProperty(ref _s, value); }
    public string AccentPct { get => _a; set => SetProperty(ref _a, value); }
    string _m="6*", _s="3*", _a="1*";
}

// Minimal base classes
public class BaseViewModel : ObservableObject { }
public class ObservableObject : System.ComponentModel.INotifyPropertyChanged
{
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    protected bool SetProperty<T>(ref T storage, T value, string? propName=null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value)) return false;
        storage = value;
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propName ?? string.Empty));
        return true;
    }
}
