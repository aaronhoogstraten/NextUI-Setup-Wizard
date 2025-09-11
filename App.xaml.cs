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
                Width = 1200,
                Height = 800,
                MinimumWidth = 1200,
                MinimumHeight = 800,
                MaximumWidth = 1200,
                MaximumHeight = 800
            };
            
            return window;
        }
    }
}
