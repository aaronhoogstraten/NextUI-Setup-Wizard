using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace NextUI_Setup_Wizard.Resources
{
    /// <summary>
    /// Service for managing ADB command logging and providing real-time updates to UI components
    /// </summary>
    public class AdbCommandLogger : INotifyPropertyChanged
    {
        private readonly Queue<AdbCommandLogEntry> _commandHistory = new();
        private readonly object _historyLock = new object();
        private const int MaxHistoryEntries = 50;

        private bool _isVisible = false; // Hidden by default
        private bool _isExpanded = true;

        /// <summary>
        /// Event fired when the command history is updated
        /// </summary>
        public event EventHandler<AdbCommandLogEntry>? CommandAdded;

        /// <summary>
        /// PropertyChanged event for data binding
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Gets whether the command window is visible
        /// </summary>
        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                if (_isVisible != value)
                {
                    _isVisible = value;
                    OnPropertyChanged(nameof(IsVisible));
                }
            }
        }

        /// <summary>
        /// Gets whether the command window is expanded
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged(nameof(IsExpanded));
                }
            }
        }

        /// <summary>
        /// Gets the command history
        /// </summary>
        public IEnumerable<AdbCommandLogEntry> CommandHistory
        {
            get
            {
                lock (_historyLock)
                {
                    return _commandHistory.ToList();
                }
            }
        }

        /// <summary>
        /// Gets the latest command entry
        /// </summary>
        public AdbCommandLogEntry? LatestCommand
        {
            get
            {
                lock (_historyLock)
                {
                    return _commandHistory.LastOrDefault();
                }
            }
        }

        /// <summary>
        /// Gets the count of commands currently in history
        /// </summary>
        public int CommandCount
        {
            get
            {
                lock (_historyLock)
                {
                    return _commandHistory.Count;
                }
            }
        }

        /// <summary>
        /// Subscribes to ADB service command events
        /// </summary>
        /// <param name="adbService">The ADB service to monitor</param>
        public void SubscribeToAdbService(AdbService adbService)
        {
            if (adbService != null)
            {
                adbService.CommandExecuted += OnAdbCommandExecuted;
            }
        }

        /// <summary>
        /// Unsubscribes from ADB service command events
        /// </summary>
        /// <param name="adbService">The ADB service to stop monitoring</param>
        public void UnsubscribeFromAdbService(AdbService adbService)
        {
            if (adbService != null)
            {
                adbService.CommandExecuted -= OnAdbCommandExecuted;
            }
        }

        /// <summary>
        /// Handles ADB command execution events
        /// </summary>
        private void OnAdbCommandExecuted(object? sender, AdbCommandLogEventArgs e)
        {
            lock (_historyLock)
            {
                // Find existing entry or create new one
                var existingEntry = _commandHistory.FirstOrDefault(entry =>
                    entry.Command == e.Command && entry.StartTime == e.StartTime);

                if (existingEntry != null)
                {
                    // Update existing entry
                    existingEntry.Status = e.Status;
                    existingEntry.EndTime = e.EndTime;
                    existingEntry.ExecutionTime = e.ExecutionTime;
                    existingEntry.Output = e.Output;
                    existingEntry.Error = e.Error;
                    existingEntry.ExitCode = e.ExitCode;
                }
                else
                {
                    // Create new entry
                    var newEntry = new AdbCommandLogEntry
                    {
                        Command = e.Command,
                        StartTime = e.StartTime,
                        EndTime = e.EndTime,
                        ExecutionTime = e.ExecutionTime,
                        Status = e.Status,
                        Output = e.Output,
                        Error = e.Error,
                        ExitCode = e.ExitCode
                    };

                    _commandHistory.Enqueue(newEntry);

                    // Maintain history size limit
                    while (_commandHistory.Count > MaxHistoryEntries)
                    {
                        _commandHistory.Dequeue();
                    }

                    // Fire event for UI updates
                    CommandAdded?.Invoke(this, newEntry);
                }
            }

            // Notify property changed for data binding
            OnPropertyChanged(nameof(CommandHistory));
            OnPropertyChanged(nameof(LatestCommand));
            OnPropertyChanged(nameof(CommandCount));
        }

        /// <summary>
        /// Clears the command history
        /// </summary>
        public void ClearHistory()
        {
            lock (_historyLock)
            {
                _commandHistory.Clear();
            }

            OnPropertyChanged(nameof(CommandHistory));
            OnPropertyChanged(nameof(LatestCommand));
            OnPropertyChanged(nameof(CommandCount));
        }

        /// <summary>
        /// Toggles the visibility of the command window
        /// </summary>
        public void ToggleVisibility()
        {
            IsVisible = !IsVisible;
        }

        /// <summary>
        /// Toggles the expanded state of the command window
        /// </summary>
        public void ToggleExpanded()
        {
            IsExpanded = !IsExpanded;
        }

        /// <summary>
        /// Shows the command window
        /// </summary>
        public void Show()
        {
            IsVisible = true;
        }

        /// <summary>
        /// Hides the command window
        /// </summary>
        public void Hide()
        {
            IsVisible = false;
        }

        /// <summary>
        /// Raises the PropertyChanged event
        /// </summary>
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Represents a logged ADB command entry
    /// </summary>
    public class AdbCommandLogEntry
    {
        public string Command { get; set; } = "";
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public TimeSpan? ExecutionTime { get; set; }
        public AdbCommandStatus Status { get; set; }
        public string? Output { get; set; }
        public string? Error { get; set; }
        public int? ExitCode { get; set; }

        /// <summary>
        /// Gets a formatted display string for the command
        /// </summary>
        public string DisplayTime => StartTime.ToString("HH:mm:ss.fff");

        /// <summary>
        /// Gets a status display string
        /// </summary>
        public string StatusDisplay => Status switch
        {
            AdbCommandStatus.Starting => "⏳ Starting...",
            AdbCommandStatus.Success => "✅ Success",
            AdbCommandStatus.Failed => "❌ Failed",
            AdbCommandStatus.Timeout => "⏰ Timeout",
            AdbCommandStatus.Exception => "💥 Exception",
            _ => Status.ToString()
        };

        /// <summary>
        /// Gets the CSS class for styling the status
        /// </summary>
        public string StatusCssClass => Status switch
        {
            AdbCommandStatus.Starting => "status-starting",
            AdbCommandStatus.Success => "status-success",
            AdbCommandStatus.Failed => "status-failed",
            AdbCommandStatus.Timeout => "status-timeout",
            AdbCommandStatus.Exception => "status-exception",
            _ => "status-unknown"
        };

        /// <summary>
        /// Gets a formatted execution time string
        /// </summary>
        public string ExecutionTimeDisplay
        {
            get
            {
                if (ExecutionTime.HasValue)
                {
                    return $"({ExecutionTime.Value.TotalSeconds:F2}s)";
                }
                return Status == AdbCommandStatus.Starting ? "(running...)" : "";
            }
        }
    }
}