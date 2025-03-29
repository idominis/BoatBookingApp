namespace BoatBookingApp.Frontend
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

#if WINDOWS
        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(new MainPage());
            window.Width = 1000;
            window.Height = 800;
            return window;
        }
#endif
    }
}