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

            // Validate behavior and block parameters to prevent injection
            if (!IsValidScrollBehavior(behavior))
                throw new ArgumentException($"Invalid scroll behavior: {behavior}", nameof(behavior));

            if (!IsValidScrollBlock(block))
                throw new ArgumentException($"Invalid scroll block: {block}", nameof(block));

            // Sanitize selector based on type to prevent CSS injection
            var sanitizedSelector = SanitizeSelector(selector, selectorType);

            var cssSelector = selectorType switch
            {
                SelectorType.DataRef => $"[data-ref=\"{sanitizedSelector}\"]",
                SelectorType.CssClass => $".{sanitizedSelector}",
                SelectorType.CssSelector => sanitizedSelector,
                _ => throw new ArgumentException($"Unknown selector type: {selectorType}")
            };

            // Use dedicated JavaScript function instead of eval to prevent injection attacks
            await jsRuntime.InvokeVoidAsync("scrollToElement", cssSelector, behavior, block);
        }

        /// <summary>
        /// Validates that the scroll behavior is one of the allowed values
        /// </summary>
        private static bool IsValidScrollBehavior(string behavior)
        {
            return behavior == "smooth" || behavior == "auto" || behavior == "instant";
        }

        /// <summary>
        /// Validates that the scroll block is one of the allowed values
        /// </summary>
        private static bool IsValidScrollBlock(string block)
        {
            return block == "start" || block == "center" || block == "end" || block == "nearest";
        }

        /// <summary>
        /// Sanitizes a selector string to prevent CSS injection attacks
        /// </summary>
        private static string SanitizeSelector(string selector, SelectorType selectorType)
        {
            if (string.IsNullOrEmpty(selector))
                return selector;

            // For DataRef and CssClass, only allow alphanumeric, hyphens, and underscores
            if (selectorType == SelectorType.DataRef || selectorType == SelectorType.CssClass)
            {
                // Remove any characters that aren't alphanumeric, hyphen, or underscore
                return System.Text.RegularExpressions.Regex.Replace(selector, @"[^a-zA-Z0-9\-_]", "");
            }

            // For CssSelector, we still allow it but the user must be aware of the risks
            // In practice, this is only used internally with known-safe selectors
            return selector;
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