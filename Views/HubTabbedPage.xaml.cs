using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CHROMA.Views;

public partial class HubTabbedPage : TabbedPage
{

	Boolean IsMapTabVisible { get; set; }

	public HubTabbedPage()
	{
		InitializeComponent();
		NavigationPage.SetHasNavigationBar(this, false);
		BindingContext = this;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();

	}

}