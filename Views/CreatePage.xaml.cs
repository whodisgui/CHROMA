using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CHROMA.ViewModels;
using Microsoft.Maui.Controls;

namespace CHROMA.Views;

public partial class CreatePage : ContentPage
{
	public CreatePage()
	{
		InitializeComponent();
		if (BindingContext == null)
		{
			BindingContext = new CritiqueViewModel();
		}
	}
}