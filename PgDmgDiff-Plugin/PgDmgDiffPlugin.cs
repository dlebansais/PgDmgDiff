namespace PgDmgDiff
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Windows;
    using System.Windows.Input;
    using System.Windows.Threading;
    using RegistryTools;
    using ResourceTools;
    using TaskbarIconHost;
    using Tracing;

    /// <summary>
    /// Represents a plugin that.
    /// </summary>
    public class PgDmgDiffPlugin : IPluginClient, IDisposable
    {
        #region Plugin
        /// <summary>
        /// Gets the plugin name.
        /// </summary>
        public string Name
        {
            get { return "PgDmgDiff"; }
        }

        /// <summary>
        /// Gets the plugin unique ID.
        /// </summary>
        public Guid Guid
        {
            get { return new Guid("{5A6B69E3-A09C-4BCE-BD37-AC6F0D1C30E4}"); }
        }

        /// <summary>
        /// Gets the plugin assembly name.
        /// </summary>
        public string AssemblyName { get; } = "PgDmgDiff-Plugin";

        /// <summary>
        ///  Gets a value indicating whether the plugin require elevated (administrator) mode to operate.
        /// </summary>
        public bool RequireElevated
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether the plugin want to handle clicks on the taskbar icon.
        /// </summary>
        public bool HasClickHandler
        {
            get { return false; }
        }

        /// <summary>
        /// Called once at startup, to initialize the plugin.
        /// </summary>
        /// <param name="isElevated">True if the caller is executing in administrator mode.</param>
        /// <param name="dispatcher">A dispatcher that can be used to synchronize with the UI.</param>
        /// <param name="settings">An interface to read and write settings in the registry.</param>
        /// <param name="logger">An interface to log events asynchronously.</param>
        public void Initialize(bool isElevated, Dispatcher dispatcher, Settings settings, ITracer logger)
        {
            IsElevated = isElevated;
            Dispatcher = dispatcher;
            Settings = settings;
            Logger = logger;

            // Create a new FileSystemWatcher and set its properties.
            Watcher = new FileSystemWatcher();
            string LocalLowPath = NativeMethods.GetKnownFolderPath(NativeMethods.LocalLowId);
            Watcher.Path = @$"{LocalLowPath}\Elder Game\Project Gorgon\Books\";

            // Watch for changes in LastAccess and LastWrite times, and
            // the renaming of files or directories.
            Watcher.NotifyFilter = NotifyFilters.LastAccess
                                    | NotifyFilters.LastWrite
                                    | NotifyFilters.FileName
                                    | NotifyFilters.DirectoryName;

            // Only watch some files.
            Watcher.Filter = FilePattern;

            // Add event handlers.
            Watcher.Changed += OnChanged;
            Watcher.Created += OnChanged;
            Watcher.Deleted += OnChanged;

            if (ParseLatestFile(out string LatestFile, out int TotalDamage, out int Killed))
            {
                CurrentLatestFile = LatestFile;
                LastTotalDamage = TotalDamage;
                LastKilled = Killed;
                LastDamageDiff = 0;
                LastKilledDiff = 0;
            }

            // Begin watching.
            Watcher.EnableRaisingEvents = true;
        }

        /// <summary>
        /// Gets the list of commands that the plugin can receive when an item is clicked in the context menu.
        /// </summary>
        public List<ICommand> CommandList { get; private set; } = new List<ICommand>();

        /// <summary>
        /// Reads a flag indicating if the state of a menu item has changed. The flag should be reset upon return until another change occurs.
        /// </summary>
        /// <param name="beforeMenuOpening">True if this function is called right before the context menu is opened by the user; otherwise, false.</param>
        /// <returns>True if a menu item state has changed since the last call; otherwise, false.</returns>
        public bool GetIsMenuChanged(bool beforeMenuOpening)
        {
            bool Result = IsMenuChanged;
            IsMenuChanged = false;

            return Result;
        }

        /// <summary>
        /// Reads the text of a menu item associated to command.
        /// </summary>
        /// <param name="command">The command associated to the menu item.</param>
        /// <returns>The menu text.</returns>
        public string GetMenuHeader(ICommand command)
        {
            return MenuHeaderTable[command];
        }

        /// <summary>
        /// Reads the state of a menu item associated to command.
        /// </summary>
        /// <param name="command">The command associated to the menu item.</param>
        /// <returns>True if the menu item should be visible to the user, false if it should be hidden.</returns>
        public bool GetMenuIsVisible(ICommand command)
        {
            return MenuIsVisibleTable[command]();
        }

        /// <summary>
        /// Reads the state of a menu item associated to command.
        /// </summary>
        /// <param name="command">The command associated to the menu item.</param>
        /// <returns>True if the menu item should appear enabled, false if it should be disabled.</returns>
        public bool GetMenuIsEnabled(ICommand command)
        {
            return MenuIsEnabledTable[command]();
        }

        /// <summary>
        /// Reads the state of a menu item associated to command.
        /// </summary>
        /// <param name="command">The command associated to the menu item.</param>
        /// <returns>True if the menu item is checked, false otherwise.</returns>
        public bool GetMenuIsChecked(ICommand command)
        {
            return MenuIsCheckedTable[command]();
        }

        /// <summary>
        /// Reads the icon of a menu item associated to command.
        /// </summary>
        /// <param name="command">The command associated to the menu item.</param>
        /// <returns>The icon to display with the menu text, null if none.</returns>
        public Bitmap? GetMenuIcon(ICommand command)
        {
            return null;
        }

        /// <summary>
        /// This method is called before the menu is displayed, but after changes in the menu have been evaluated.
        /// </summary>
        public void OnMenuOpening()
        {
        }

        /// <summary>
        /// Requests for command to be executed.
        /// </summary>
        /// <param name="command">The command to execute.</param>
        public void OnExecuteCommand(ICommand command)
        {
            MenuHandlerTable[command]();
        }

        /// <summary>
        /// Reads a flag indicating if the plugin icon, that might reflect the state of the plugin, has changed.
        /// </summary>
        /// <returns>True if the icon has changed since the last call, false otherwise.</returns>
        public bool GetIsIconChanged()
        {
            bool Result = IsIconChanged;
            IsIconChanged = false;

            return Result;
        }

        /// <summary>
        /// Gets the icon displayed in the taskbar.
        /// </summary>
        public Icon Icon
        {
            get
            {
                ResourceLoader.LoadIcon("Taskbar.ico", string.Empty, out Icon Result);
                return Result;
            }
        }

        /// <summary>
        /// Gets the bitmap displayed in the preferred plugin menu.
        /// </summary>
        public Bitmap SelectionBitmap
        {
            get
            {
                ResourceLoader.LoadBitmap("PgDmgDiff.png", string.Empty, out Bitmap Result);
                return Result;
            }
        }

        /// <summary>
        /// Requests for the main plugin operation to be executed.
        /// </summary>
        public void OnIconClicked()
        {
        }

        /// <summary>
        /// Reads a flag indicating if the plugin tooltip, that might reflect the state of the plugin, has changed.
        /// </summary>
        /// <returns>True if the tooltip has changed since the last call, false otherwise.</returns>
        public bool GetIsToolTipChanged()
        {
            bool Result = false;

            return Result;
        }

        /// <summary>
        /// Gets the free text that indicate the state of the plugin.
        /// </summary>
        public string ToolTip
        {
            get
            {
                string Result = string.Empty;

                return Result;
            }
        }

        /// <summary>
        /// Called when the taskbar is getting the application focus.
        /// </summary>
        public void OnActivated()
        {
        }

        /// <summary>
        /// Called when the taskbar is loosing the application focus.
        /// </summary>
        public void OnDeactivated()
        {
        }

        /// <summary>
        /// Requests to close and terminate a plugin.
        /// </summary>
        /// <param name="canClose">True if no plugin called before this one has returned false, false if one of them has.</param>
        /// <returns>True if the plugin can be safely terminated, false if the request is denied.</returns>
        public bool CanClose(bool canClose)
        {
            return true;
        }

        /// <summary>
        /// Requests to begin closing the plugin.
        /// </summary>
        public void BeginClose()
        {
        }

        /// <summary>
        /// Gets a value indicating whether the plugin is closed.
        /// </summary>
        public bool IsClosed
        {
            get { return true; }
        }

        /// <summary>
        /// Gets a value indicating whether the caller is executing in administrator mode.
        /// </summary>
        public bool IsElevated { get; private set; }

        /// <summary>
        /// Gets a dispatcher that can be used to synchronize with the UI.
        /// </summary>
        public Dispatcher Dispatcher { get; private set; } = null!;

        /// <summary>
        /// Gets an interface to read and write settings in the registry.
        /// </summary>
        public Settings Settings { get; private set; } = null!;

        /// <summary>
        /// Gets an interface to log events asynchronously.
        /// </summary>
        public ITracer Logger { get; private set; } = null!;

        private void AddLog(string message)
        {
            Logger.Write(Category.Information, message);
        }

        private Dictionary<ICommand, string> MenuHeaderTable = new Dictionary<ICommand, string>();
        private Dictionary<ICommand, Func<bool>> MenuIsVisibleTable = new Dictionary<ICommand, Func<bool>>();
        private Dictionary<ICommand, Func<bool>> MenuIsEnabledTable = new Dictionary<ICommand, Func<bool>>();
        private Dictionary<ICommand, Func<bool>> MenuIsCheckedTable = new Dictionary<ICommand, Func<bool>>();
        private Dictionary<ICommand, Action> MenuHandlerTable = new Dictionary<ICommand, Action>();
        private bool IsIconChanged;
        private bool IsMenuChanged;
        #endregion

        #region File System
        // Define the event handlers.
        private void OnChanged(object source, FileSystemEventArgs e)
        {
            if (e.ChangeType == WatcherChangeTypes.Created)
                Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(OnNewFile));
        }

        private void OnNewFile()
        {
            if (ParseLatestFile(out string NewLatestFile, out int TotalDamage, out int Killed) && NewLatestFile != CurrentLatestFile)
            {
                CurrentLatestFile = NewLatestFile;

                if (LastTotalDamage > 0 && LastKilled > 0)
                {
                    if (LastTotalDamage < TotalDamage && LastKilled < Killed)
                    {
                        LastDamageDiff = TotalDamage - LastTotalDamage;
                        LastTotalDamage = TotalDamage;
                        LastKilledDiff = Killed - LastKilled;
                        LastKilled = Killed;
                    }

                    int Dpm = LastDamageDiff / LastKilledDiff;
                    string Summary = $"Total: {LastDamageDiff}, Kills: {LastKilledDiff}, Dpm: {Dpm}";

                    Clipboard.SetText(Summary);
                }
            }
        }

        private bool ParseLatestFile(out string latestFile, out int totalDamage, out int killed)
        {
            latestFile = string.Empty;
            totalDamage = 0;
            killed = 0;

            string[] FileNames = Directory.GetFiles(Watcher.Path, FilePattern);
            bool Result = false;

            DateTime MostRecentTime = DateTime.MinValue;
            foreach (string FileName in FileNames)
            {
                DateTime FileTime = File.GetLastWriteTimeUtc(FileName);
                if (MostRecentTime < FileTime)
                {
                    try
                    {
                        using FileStream stream = new FileStream(FileName, FileMode.Open, FileAccess.Read);
                        using StreamReader reader = new StreamReader(stream, Encoding.UTF8);
                        string Content = reader.ReadToEnd();

                        if (ReadIntValue(Content, "You have dealt <b>", out totalDamage) && ReadIntValue(Content, "You have killed <b>", out killed))
                        {
                            MostRecentTime = FileTime;
                            latestFile = FileName;
                            Result = true;
                        }
                    }
                    catch
                    {
                    }
                }
            }

            return Result;
        }

        private static bool ReadIntValue(string content, string pattern, out int value)
        {
            value = -1;

            int StartIndex = content.IndexOf(pattern);
            if (StartIndex >= 0)
            {
                StartIndex += pattern.Length;
                int EndIndex = StartIndex + 1;

                while (EndIndex < content.Length)
                {
                    string StringValue = content.Substring(StartIndex, EndIndex - StartIndex);
                    EndIndex++;

                    StringValue = StringValue.Replace(CultureInfo.CurrentCulture.NumberFormat.NumberGroupSeparator, string.Empty);
                    StringValue = StringValue.Replace(CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator, string.Empty);

                    if (int.TryParse(StringValue, NumberStyles.Integer, CultureInfo.CurrentCulture, out int LastValue))
                    {
                        value = LastValue;
                    }
                    else if (value >= 0)
                        break;
                }
            }

            return value >= 0;
        }

        private const string FilePattern = "PlayerAge_*_*.txt";
        private string BookFolder = string.Empty;

        private FileSystemWatcher Watcher = new FileSystemWatcher();
        private string CurrentLatestFile = string.Empty;
        private int LastTotalDamage;
        private int LastKilled;
        private int LastDamageDiff;
        private int LastKilledDiff;
        #endregion

        #region Implementation of IDisposable
        /// <summary>
        /// Called when an object should release its resources.
        /// </summary>
        /// <param name="isDisposing">Indicates if resources must be disposed now.</param>
        protected virtual void Dispose(bool isDisposing)
        {
            if (!IsDisposed)
            {
                IsDisposed = true;

                if (isDisposing)
                    DisposeNow();
            }
        }

        /// <summary>
        /// Called when an object should release its resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="PgDmgDiffPlugin"/> class.
        /// </summary>
        ~PgDmgDiffPlugin()
        {
            Dispose(false);
        }

        /// <summary>
        /// True after <see cref="Dispose(bool)"/> has been invoked.
        /// </summary>
        private bool IsDisposed;

        /// <summary>
        /// Disposes of every reference that must be cleaned up.
        /// </summary>
        private void DisposeNow()
        {
            using (Settings)
            {
            }

            using (Watcher)
            {
            }
        }
        #endregion
    }
}
