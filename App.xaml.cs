namespace CHROMA
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

		protected override Window CreateWindow(IActivationState? activationState)
		{
			// Use the tabbed page as the root
			return new Window(new Views.HubTabbedPage());
		}
	}
}