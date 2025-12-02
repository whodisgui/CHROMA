using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CHROMA.ViewModels;

namespace CHROMA.Views;

public partial class VisualPage : ContentPage
{
	public VisualPage()
	{
		InitializeComponent();

		// Visual page now uses the VisualViewModel as its binding context.
		BindingContext = new VisualViewModel();
	}
}