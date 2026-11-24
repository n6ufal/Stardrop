using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Stardrop.Utilities.Internal
{
    public enum NotificationKind
    {
        Success,
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// A single message shown over the main window. Unlike a dialog nothing waits on it and it clears itself, which
    /// is what keeps a run of installs from leaving a window to dismiss behind each one.
    /// </summary>
    public class Notification : INotifyPropertyChanged
    {
        public const double DEFAULT_DURATION_SECONDS = 6;

        public string Key { get; }
        public NotificationKind Kind { get; }
        public double DurationSeconds { get; }

        /// <summary>How many of the thing this notification stands in for, so an aggregated one can count up</summary>
        public int Amount { get; private set; }

        private string _message;
        public string Message
        {
            get { return _message; }
            private set
            {
                _message = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Message)));
            }
        }

        public bool IsSuccess { get { return Kind is NotificationKind.Success; } }
        public bool IsWarning { get { return Kind is NotificationKind.Warning; } }
        public bool IsError { get { return Kind is NotificationKind.Error; } }

        public string Icon
        {
            get
            {
                switch (Kind)
                {
                    case NotificationKind.Success: return "mdi-check-circle-outline";
                    case NotificationKind.Warning: return "mdi-alert-outline";
                    case NotificationKind.Error: return "mdi-alert-octagon-outline";
                    default: return "mdi-information-outline";
                }
            }
        }

        private DispatcherTimer? _timer;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler? Expired;

        internal Notification(string key, string message, NotificationKind kind, int amount, double durationSeconds)
        {
            Key = key;
            _message = message;
            Kind = kind;
            Amount = amount;
            DurationSeconds = durationSeconds;
        }

        /// <summary>
        /// Folds another result into this one. The caller supplies the text for the new total, since only it knows
        /// how to describe what has been counted.
        /// </summary>
        internal void Merge(string message, int amount)
        {
            Message = message;
            Amount += amount;

            // Restarted rather than left running, so a result that arrives just before this was due to clear still
            // gets read
            StopTimer();
            StartTimer();
        }

        internal void StartTimer()
        {
            if (DurationSeconds <= 0)
            {
                return;
            }

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(DurationSeconds) };
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        internal void StopTimer()
        {
            if (_timer is null)
            {
                return;
            }

            _timer.Stop();
            _timer.Tick -= Timer_Tick;
            _timer = null;
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            StopTimer();
            Expired?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Holds the notifications currently on screen. Kept apart from the view models so the install paths, which are
    /// spread across the MainWindow partials, can report to it without threading a reference through every call.
    /// </summary>
    public class NotificationCenter
    {
        /// <summary>Cards kept on screen at once. Past this the oldest goes, so the newest result is never hidden</summary>
        private const int MAX_VISIBLE = 3;

        public static NotificationCenter Shared { get; } = new NotificationCenter();

        /// <summary>Notifications that can still absorb a matching result, keyed by what they are reporting on</summary>
        private readonly Dictionary<string, Notification> _live = new(StringComparer.OrdinalIgnoreCase);

        public ObservableCollection<Notification> Notifications { get; } = new();

        private NotificationCenter() { }

        /// <summary>
        /// Shows a one-off message. Every call is its own card, so use this for things that are not expected to
        /// repeat in quick succession.
        /// </summary>
        public void Show(string message, NotificationKind kind = NotificationKind.Success, double durationSeconds = Notification.DEFAULT_DURATION_SECONDS)
        {
            if (String.IsNullOrWhiteSpace(message))
            {
                return;
            }

            RunOnUIThread(() => Add(new Notification(message, message, kind, 1, durationSeconds)));
        }

        /// <summary>
        /// Shows a message that stands in for a number of the same thing. A second call under the same key while the
        /// first is still on screen counts up in place rather than stacking another card, which is what turns a run
        /// of installs into a single line.
        /// </summary>
        /// <param name="message">Text for a single one of these</param>
        /// <param name="aggregateFormat">Text for several of these, taking the total as its only argument</param>
        public void ShowAggregated(string key, string message, string aggregateFormat, int amount, NotificationKind kind = NotificationKind.Success, double durationSeconds = Notification.DEFAULT_DURATION_SECONDS)
        {
            if (amount <= 0)
            {
                return;
            }

            RunOnUIThread(() =>
            {
                if (_live.TryGetValue(key, out Notification? existing) && Notifications.Contains(existing))
                {
                    existing.Merge(String.Format(aggregateFormat, existing.Amount + amount), amount);
                    return;
                }

                var text = amount > 1 ? String.Format(aggregateFormat, amount) : message;
                var notification = new Notification(key, text, kind, amount, durationSeconds);
                _live[key] = notification;

                Add(notification);
            });
        }

        public void Dismiss(Notification notification)
        {
            RunOnUIThread(() => Remove(notification));
        }

        private void Add(Notification notification)
        {
            notification.Expired += Notification_Expired;

            while (Notifications.Count >= MAX_VISIBLE)
            {
                Remove(Notifications[0]);
            }

            Notifications.Add(notification);
            notification.StartTimer();
        }

        private void Remove(Notification notification)
        {
            notification.Expired -= Notification_Expired;
            notification.StopTimer();
            Notifications.Remove(notification);

            if (_live.TryGetValue(notification.Key, out Notification? tracked) && ReferenceEquals(tracked, notification))
            {
                _live.Remove(notification.Key);
            }
        }

        private void Notification_Expired(object? sender, EventArgs e)
        {
            if (sender is Notification notification)
            {
                Remove(notification);
            }
        }

        private static void RunOnUIThread(Action action)
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                action();
                return;
            }

            Dispatcher.UIThread.Post(action);
        }
    }
}
