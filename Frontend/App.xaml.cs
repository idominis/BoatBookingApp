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
            return window;
        }
#endif
    }
}
