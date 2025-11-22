namespace NextUI_Setup_Wizard.Resources
{
    /// <summary>
    /// Application-wide constants for magic strings and common values
    /// </summary>
    public static class Constants
    {
        #region ADB and Platform Tools

        /// <summary>
        /// ADB executable name on Windows
        /// </summary>
        public const string ADB_EXECUTABLE_WINDOWS = "adb.exe";

        /// <summary>
        /// ADB executable name on Unix-based systems (macOS, Linux)
        /// </summary>
        public const string ADB_EXECUTABLE_UNIX = "adb";

        /// <summary>
        /// Fastboot executable name
        /// </summary>
        public const string FASTBOOT_EXECUTABLE = "fastboot";

        /// <summary>
        /// AAPT executable name
        /// </summary>
        public const string AAPT_EXECUTABLE = "aapt";

        /// <summary>
        /// AIDL executable name
        /// </summary>
        public const string AIDL_EXECUTABLE = "aidl";

        /// <summary>
        /// Dexdump executable name
        /// </summary>
        public const string DEXDUMP_EXECUTABLE = "dexdump";

        /// <summary>
        /// Split-select executable name
        /// </summary>
        public const string SPLIT_SELECT_EXECUTABLE = "split-select";

        /// <summary>
        /// Platform-tools directory name
        /// </summary>
        public const string PLATFORM_TOOLS_DIR = "platform-tools";

        /// <summary>
        /// Homebrew installation path for Android Platform Tools on macOS (Apple Silicon)
        /// </summary>
        public const string HOMEBREW_ANDROID_PLATFORM_TOOLS_PATH = "/opt/homebrew/Caskroom/android-platform-tools";

        #endregion

        #region File Extensions

        /// <summary>
        /// ZIP file extension
        /// </summary>
        public const string ZIP_EXTENSION = ".zip";

        /// <summary>
        /// TMP file extension
        /// </summary>
        public const string TMP_EXTENSION = ".tmp";

        #endregion

        #region HTTP Headers

        /// <summary>
        /// User-Agent header value for HTTP requests
        /// </summary>
        public const string USER_AGENT_HEADER = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36";

        #endregion

        #region Log File Names

        /// <summary>
        /// Main log file name
        /// </summary>
        public const string LOG_FILE_NAME = "NextUI-Setup-Wizard.log";

        /// <summary>
        /// Previous log file name (rotated log)
        /// </summary>
        public const string PREVIOUS_LOG_FILE_NAME = "NextUI-Setup-Wizard_previous.log";

        #endregion
    }
}
