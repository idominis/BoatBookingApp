namespace BoatBookingApp.Frontend
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
#if WINDOWS
            var window = new Window(new MainPage())
            {
                Width = 600,
                Height = 850
            };
            return window;
#else
            return new Window(new AppShell());
#endif
        }
    }
}