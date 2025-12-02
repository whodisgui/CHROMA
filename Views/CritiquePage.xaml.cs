using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CHROMA.ViewModels;

namespace CHROMA.Views;

public partial class CritiquePage : ContentPage
{
	public CritiquePage()
	{
		InitializeComponent();
		BindingContext = new CritiqueViewModel();
	}
}