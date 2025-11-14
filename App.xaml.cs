using NextUI_Setup_Wizard.Resources;

namespace NextUI_Setup_Wizard
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            
            // Rotate log files on startup
            Logger.RotateLogFiles();
            
            // Log application startup
            using var logger = new Logger();
            logger.Log("NextUI Setup Wizard started.");
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(new MainPage())
            {
                Title = "NextUI Setup Wizard",
                // Responsive window sizing - adapts to different screen sizes
                Width = 1200,
                Height = 800,
                MinimumWidth = 800,    // Allow smaller minimum for laptop screens
                MinimumHeight = 600    // Allow smaller minimum for laptop screens
                // No maximum constraints - allows user to maximize/resize as needed
            };

            return window;
        }
    }
}
