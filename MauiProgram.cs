using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;

namespace NextUI_Setup_Wizard
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.Services.AddMauiBlazorWebView();

            // Use HttpClient factory pattern for better resource management
            builder.Services.AddHttpClient();

            // Register ADB services
            builder.Services.AddSingleton<NextUI_Setup_Wizard.Resources.PlatformToolsExtractor>();
            builder.Services.AddTransient<NextUI_Setup_Wizard.Resources.AdbService>(provider =>
            {
                var extractor = provider.GetRequiredService<NextUI_Setup_Wizard.Resources.PlatformToolsExtractor>();
                return new NextUI_Setup_Wizard.Resources.AdbService(extractor.AdbExecutablePath);
            });
            builder.Services.AddTransient<NextUI_Setup_Wizard.Resources.AdbFileOperations>();

#if DEBUG
    		builder.Services.AddBlazorWebViewDeveloperTools();
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
