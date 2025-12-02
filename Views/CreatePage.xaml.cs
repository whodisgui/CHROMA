using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CHROMA.ViewModels;

namespace CHROMA.Views;

public partial class CreatePage : ContentPage
{
	public CreatePage()
	{
		InitializeComponent();
		BindingContext = new CreateViewModel();
	}
}