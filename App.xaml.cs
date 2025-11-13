namespace CHROMA
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

			// Sets constant light theme - optimal for colors/graphics
			// If line is deleted, the default is system setting
			UserAppTheme = AppTheme.Light;
        }

		protected override Window CreateWindow(IActivationState? activationState)
		{
			// Use the tabbed page as the root
			return new Window(new Views.HubTabbedPage());
		}
	}
}