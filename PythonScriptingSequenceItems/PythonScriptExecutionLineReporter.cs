using System;
using System.Windows;
using System.Windows.Threading;

namespace NINA.Plugin.Python.PythonScriptingTestCategory {

    internal sealed class PythonScriptExecutionLineReporter : IDisposable {
        private const int UpdateIntervalMilliseconds = 50;

        private readonly object syncRoot = new object();
        private readonly Action<int> setCurrentLine;
        private readonly Dispatcher dispatcher;
        private int lastReportedLine;
        private int pendingLine;
        private long lastUpdateMilliseconds;
        private bool updateScheduled;
        private bool disposed;

        public PythonScriptExecutionLineReporter(Action<int> setCurrentLine) {
            this.setCurrentLine = setCurrentLine ?? throw new ArgumentNullException(nameof(setCurrentLine));
            dispatcher = Application.Current?.Dispatcher;

            SetCurrentLine(0, force: true);
        }

        public void Report(int lineNumber) {
            if (lineNumber <= 0) {
                return;
            }

            lock (syncRoot) {
                if (disposed || lineNumber == lastReportedLine) {
                    return;
                }

                lastReportedLine = lineNumber;
                pendingLine = lineNumber;

                if (updateScheduled) {
                    return;
                }

                updateScheduled = true;
            }

            ScheduleUpdate();
        }

        public void Dispose() {
            lock (syncRoot) {
                disposed = true;
                pendingLine = 0;
                updateScheduled = false;
            }

            SetCurrentLine(0, force: true);
        }

        private void ScheduleUpdate() {
            int delay = GetUpdateDelayMilliseconds();

            if (dispatcher == null) {
                FlushPendingLine();
                return;
            }

            if (delay <= 0) {
                dispatcher.BeginInvoke(new Action(FlushPendingLine), DispatcherPriority.Background);
                return;
            }

            dispatcher.BeginInvoke(new Action(() => {
                var timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher) {
                    Interval = TimeSpan.FromMilliseconds(delay)
                };

                timer.Tick += (_, _) => {
                    timer.Stop();
                    FlushPendingLine();
                };

                timer.Start();
            }), DispatcherPriority.Background);
        }

        private int GetUpdateDelayMilliseconds() {
            lock (syncRoot) {
                if (lastUpdateMilliseconds == 0) {
                    return 0;
                }

                long elapsed = Environment.TickCount64 - lastUpdateMilliseconds;
                return elapsed >= UpdateIntervalMilliseconds ? 0 : (int)(UpdateIntervalMilliseconds - elapsed);
            }
        }

        private void FlushPendingLine() {
            int lineNumber;

            lock (syncRoot) {
                if (disposed) {
                    updateScheduled = false;
                    return;
                }

                lineNumber = pendingLine;
                pendingLine = 0;
                updateScheduled = false;
                lastUpdateMilliseconds = Environment.TickCount64;
            }

            if (lineNumber > 0) {
                SetCurrentLine(lineNumber, force: false);
            }
        }

        private void SetCurrentLine(int lineNumber, bool force) {
            if (dispatcher == null || dispatcher.CheckAccess()) {
                if (force || !disposed) {
                    setCurrentLine(lineNumber);
                }

                return;
            }

            dispatcher.BeginInvoke(new Action(() => {
                if (force || !disposed) {
                    setCurrentLine(lineNumber);
                }
            }), DispatcherPriority.Background);
        }
    }
}
