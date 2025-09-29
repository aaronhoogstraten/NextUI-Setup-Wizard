using System;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace NextUI_Setup_Wizard.Resources
{
    public enum OSType
    {
        Windows,
        Mac,
        Unsupported
    }

    public static class Utils
    {
        public static OSType CurrentOS
        {
            get
            {
#if WINDOWS
                return OSType.Windows;
#elif MACCATALYST
                return OSType.Mac;
#else
                return OSType.Unsupported;
#endif
            }
        }

        /// <summary>
        /// Opens a directory in the system's default file manager.
        /// Attempts multiple methods to ensure cross-platform compatibility.
        /// </summary>
        /// <param name="directoryPath">The path to the directory to open</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public static async Task OpenDirectoryAsync(string directoryPath)
        {
            try
            {
                bool didOpen = await Launcher.OpenAsync(new Uri($"file://{directoryPath}"));
                if (!didOpen)
                    await Launcher.OpenAsync(directoryPath);
            }
            catch
            {
            }
        }

        #region Auto-Scroll Helpers

        /// <summary>
        /// Scrolls to an element using various selector types with customizable behavior
        /// </summary>
        /// <param name="jsRuntime">JSRuntime instance</param>
        /// <param name="selector">Element selector - can be a data-ref value, CSS class, or full CSS selector</param>
        /// <param name="selectorType">Type of selector: DataRef, CssClass, or CssSelector</param>
        /// <param name="behavior">Scroll behavior (smooth or auto)</param>
        /// <param name="block">Block position (start, center, end, nearest)</param>
        /// <param name="delay">Delay before scrolling to allow DOM rendering (0 to disable)</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public static async Task ScrollToElementAsync(IJSRuntime jsRuntime, string selector,
            SelectorType selectorType = SelectorType.DataRef,
            string behavior = "smooth",
            string block = "center",
            int delay = 50)
        {
            if (delay > 0)
                await Task.Delay(delay);

            var cssSelector = selectorType switch
            {
                SelectorType.DataRef => $"[data-ref=\"{selector}\"]",
                SelectorType.CssClass => $".{selector}",
                SelectorType.CssSelector => selector,
                _ => throw new ArgumentException($"Unknown selector type: {selectorType}")
            };

            await jsRuntime.InvokeVoidAsync("eval",
                $"document.querySelector('{cssSelector}').scrollIntoView({{behavior: '{behavior}', block: '{block}'}});");
        }

        // Convenience methods for common scroll patterns
        public static Task ScrollToPageHeaderAsync(IJSRuntime jsRuntime) =>
            ScrollToElementAsync(jsRuntime, "page-header", SelectorType.DataRef, "auto", "start", 0);

        public static Task ScrollToProgressContainerAsync(IJSRuntime jsRuntime) =>
            ScrollToElementAsync(jsRuntime, "progress-container", SelectorType.DataRef, "smooth", "end", 50);

        public static Task ScrollToStatusMessageAsync(IJSRuntime jsRuntime) =>
            ScrollToElementAsync(jsRuntime, "status-message", SelectorType.CssClass, "smooth", "center", 50);

        #endregion

        /// <summary>
        /// Defines the type of selector used for element selection
        /// </summary>
        public enum SelectorType
        {
            /// <summary>
            /// Selector is a data-ref attribute value (e.g., "page-header" becomes "[data-ref='page-header']")
            /// </summary>
            DataRef,
            /// <summary>
            /// Selector is a CSS class name (e.g., "status-message" becomes ".status-message")
            /// </summary>
            CssClass,
            /// <summary>
            /// Selector is a full CSS selector (e.g., "#myId", ".class > div", etc.)
            /// </summary>
            CssSelector
        }
    }
}