using System.Windows.Input;

namespace CHROMA.ViewModels;

public class CritiqueViewModel : BaseViewModel
{
    public string PaletteText { get => _txt; set => SetProperty(ref _txt, value); }
    string _txt = "#E96841, #41C2E9, #F5D76E";

    public ReportVM Report { get; } = new();

    public ICommand ImportCommand => new Command(() => {});
    public ICommand RunCritiqueCommand => new Command(Run);
    public ICommand SaveCommand => new Command(() => {});
    public ICommand ExportCommand => new Command(() => {});

    void Run()
    {
        // Stub: populate simple text; later replace with real service calls
        Report.Summary = "Automated feedback based on color-theory rules.";
        Report.HarmonySpacing = "Triadic spacing ~120° OK.";
        Report.Contrast = "Pairs with contrast ratio ≥ 4.5: #E96841 on #ffffff.";
        Report.DeltaE = "No pair below ΔE 8 detected.";
        Report.Balance = "Close to 60/30/10.";
    }
}

public class ReportVM : ObservableObject
{
    string _summary="", _hs="", _cr="", _de="", _bal="";
    public string Summary { get => _summary; set => SetProperty(ref _summary, value, nameof(Summary)); }
    public string HarmonySpacing { get => _hs; set => SetProperty(ref _hs, value, nameof(HarmonySpacing)); }
    public string Contrast { get => _cr; set => SetProperty(ref _cr, value, nameof(Contrast)); }
    public string DeltaE { get => _de; set => SetProperty(ref _de, value, nameof(DeltaE)); }
    public string Balance { get => _bal; set => SetProperty(ref _bal, value, nameof(Balance)); }
}
