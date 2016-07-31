using System;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.IO;
using System.Drawing.Text;
using Mono.Cecil;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Collections.Generic;
using System.Windows.Forms.VisualStyles;

using ContentAlignment = System.Drawing.ContentAlignment;
using System.Diagnostics;
using System.Drawing.Imaging;

namespace ETGModInstaller {
    public class InstallerWindow : Form {

        public static string InstantClearSymbols;

        public static Version Version = Assembly.GetEntryAssembly().GetName().Version;

        public static InstallerWindow Instance;

        public static Font GlobalFont;

        public Thread GUIThread;
        private List<Action> delayed = new List<Action>();

        public OpenFileDialog OpenExeDialog;
        public OpenFileDialog OpenModDialog;
        
        public RichTextBox LogBox;

        public TextBox ExePathBox;
        public Button ExePathButton;
        public Label ExeStatusLabel;
        public TabControl VersionTabs;
        public ListBox APIModsList;
        public Panel AdvancedPanel;
        public List<TextBox> AdvancedPathBoxes = new List<TextBox>();
        public List<Button> AdvancedRemoveButtons = new List<Button>();
        public Button AdvancedAddButton;
        public Label AdvancedLabel;
        public CheckBox AdvancedAutoRunCheckbox;
        public CheckBox AdvancedOfflineCheckbox;
        public CheckBox AdvancedBinaryWrappedCheckbox;
        public Button InstallButton;
        public Button UninstallButton;
        public CustomProgress Progress;
        
        public int AddIndex = 0;
        public int AddOffset = 0;
        
        public List<Tuple<string, string>> APIMods;
        
        public string ModVersion;
        public MonoMod.MonoMod MainMod;
        
        public InstallerWindow() {
            Instance = this;
            HandleCreated += onHandleCreated;

            OpenExeDialog = new OpenFileDialog() {
                Title = "Select " + ETGFinder.MainName,
                AutoUpgradeEnabled = true,
                CheckFileExists = true,
                CheckPathExists = true,
                ValidateNames = true,
                Multiselect = false,
                ShowReadOnly = false,
                Filter = ETGFinder.MainName + "|" + ETGFinder.MainName,
                FilterIndex = 0
            };
            OpenExeDialog.FileOk += (object senderFileOk, CancelEventArgs eFileOk) => Task.Run(() => this.ExeSelected(OpenExeDialog.FileNames[0]));

            OpenModDialog = new OpenFileDialog() {
                Title = "Select ETGMod Backend",
                AutoUpgradeEnabled = true,
                CheckFileExists = true,
                CheckPathExists = true,
                ValidateNames = true,
                Multiselect = true,
                ShowReadOnly = false,
                Filter = "ETGMod DLL|*.mm.dll|ETGMod ZIP|*.zip|All files|*.*",
                FilterIndex = 0
            };
            OpenModDialog.FileOk += (object senderFileOk, CancelEventArgs eFileOk) => AddManualPathRows(OpenModDialog.FileNames);

            Text = "Mod the Gungeon Installer";
            if (ETGFinder.Platform.HasFlag(ETGPlatform.Unix)) {
                FormBorderStyle = FormBorderStyle.Sizable;
            } else {
                FormBorderStyle = FormBorderStyle.FixedDialog;
            }
            ResizeRedraw = false;
            MaximizeBox = false;
            MinimizeBox = true;
            StartPosition = FormStartPosition.CenterScreen;

            //Setting the font doesn't change anything...
            /*PrivateFontCollection pfc = LoadAsset<PrivateFontCollection>("fonts.uni05_53");
            for (int i = 0; i < pfc.Families.Length; i++) {
                Console.WriteLine("Font " + i + ": " + pfc.Families[i]);
            }
            GlobalFont = new Font(pfc.Families[0], 8f);*/

            if (GlobalFont == null) {
                if (ETGFinder.Platform.HasFlag(ETGPlatform.Windows)) {
                    GlobalFont = new Font("Segoe UI", 8f, FontStyle.Regular);
                } else if (ETGFinder.Platform.HasFlag(ETGPlatform.MacOS)) {
                    GlobalFont = new Font("Lucida Grande", 8f, FontStyle.Regular); // With Helvetica Neue: Erter Tne Girgeor
                } else {
                    GlobalFont = new Font("DejaVu Sans", 8f, FontStyle.Regular);
                }
            }

            AllowDrop = true;
            DragDrop += onDragDrop;
            DragEnter += delegate(object sender, DragEventArgs e) {
                if (e.Data.GetDataPresent(DataFormats.FileDrop) && VersionTabs.Enabled) {
                    e.Effect = DragDropEffects.Copy;
                    VersionTabs.SelectedIndex = 2;
                }
            };
            BackgroundImage = LoadAsset<Image>("background");
            BackgroundImageLayout = ImageLayout.Center;

            //ShuffleIconColors();
            Icon = LoadAsset<Icon>("icons.main");

            ResetSize();
            SizeChanged += ResetSize;

            Controls.Add(new Label() {
                Bounds = new Rectangle(448, 338, 308, 16),
                Font = GlobalFont,
                TextAlign = ContentAlignment.BottomRight,
                Text = "v" + Version,
                BackColor = Color.Transparent,
                ForeColor = Color.FromArgb(127, 0, 0, 0)
            });
            
            Controls.Add(LogBox = new RichTextBox() {
                Bounds = new Rectangle(0, 0, 448, 358),
                Font = GlobalFont,
                ReadOnly = true,
                Multiline = true,
                //ScrollBars = System.Windows.Forms.ScrollBars.Vertical,
                DetectUrls = true,
                ShortcutsEnabled = true,
                BackColor = Color.Black,
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                WordWrap = true,
                Text = "ETGMod Installer v" + Version + "\n",
                Visible = false,
            });

            Controls.Add(Progress = new CustomProgress() {
                Bounds = new Rectangle(448, 313, 312, 24),
                Font = GlobalFont,
                Text = "Idle."
            });

            Add(new Label() {
                Font = GlobalFont,
                TextAlign = ContentAlignment.MiddleCenter,
                Text = !ETGFinder.Platform.HasFlag(ETGPlatform.MacOS) ? ("Step 1: Select " + ETGFinder.MainName) : ("Step 1: Drag-n-drop EtG_OSX.app here"),
                BackColor = Color.Transparent,
                ForeColor = Color.Black
            });
            
            Add(ExePathBox = new TextBox() {
                Font = GlobalFont,
                ReadOnly = true
            });
            ExePathBox.Width -= 32;
            Controls.Add(ExePathButton = new Button() {
                Bounds = new Rectangle(ExePathBox.Bounds.X + ExePathBox.Bounds.Width, ExePathBox.Bounds.Y, 32, ExePathBox.Bounds.Height),
                Font = GlobalFont,
                Image = LoadAsset<Image>("icons.open"),
                ImageAlign = ContentAlignment.MiddleCenter
            });
            ExePathButton.Click += (object senderClick, EventArgs eClick) => OpenExeDialog.ShowDialog(this);
            AddOffset += 2;
            
            Add(ExeStatusLabel = new Label() {
                Font = GlobalFont,
                TextAlign = ContentAlignment.MiddleCenter,
                Text = "No " + ETGFinder.MainName + " selected",
                BackColor = Color.FromArgb(127, 255, 63, 63),
                ForeColor = Color.Black
            });
            
            AddOffset += 2;
            
            Add(new Label() {
                Font = GlobalFont,
                TextAlign = ContentAlignment.MiddleCenter,
                Text = "Step 2: Choose your API mods",
                BackColor = Color.Transparent,
                ForeColor = Color.Black
            });
            
            Controls.Add(InstallButton = new Button() {
                Bounds = new Rectangle(448, 313 - 1 - ExePathButton.Size.Height, 312 - 32, ExePathButton.Size.Height),
                Font = GlobalFont,
                TextAlign = ContentAlignment.MiddleCenter,
                Text = "Step 3: Install ETGMod",
                Enabled = false
            });
            InstallButton.Click += (object senderClick, EventArgs eClick) => Task.Run((Action) this.Install);
            Controls.Add(UninstallButton = new Button() {
                Bounds = new Rectangle(InstallButton.Bounds.X + InstallButton.Bounds.Width, InstallButton.Bounds.Y, 32, InstallButton.Bounds.Height),
                Font = GlobalFont,
                Image = LoadAsset<Image>("icons.uninstall"),
                ImageAlign = ContentAlignment.MiddleCenter
            });
            UninstallButton.Click += (object senderClick, EventArgs eClick) => Task.Run(delegate() {
                this.Uninstall();
                this.ClearCache();
                this.ExeSelected(ExePathBox.Text, " [just uninstalled]");
                this.SetMainEnabled(true);
            });
            
            Controls.Add(VersionTabs = new TabControl() {
                Bounds = new Rectangle(448, 4 + 26 * AddIndex + AddOffset, 312, InstallButton.Location.Y - (4 + 26 * AddIndex + AddOffset)),
                Font = GlobalFont,
                BackColor = Color.Transparent
            });

            if (ETGFinder.Platform.HasFlag(ETGPlatform.MacOS)) {
                VersionTabs.BackColor = Color.White;
                // Mono's WinForms implementation on macOS just sucks.
                VersionTabs.SelectedIndexChanged += delegate(object sender, EventArgs e) {
                    for (int i = 0; i < VersionTabs.TabPages.Count; i++) {
                        if (i == VersionTabs.SelectedIndex) {
                            VersionTabs.TabPages[i].ShowDeep();
                            continue;
                        }
                        VersionTabs.TabPages[i].HideDeep();
                    }
                    RefreshManualPanel();
                };
            }
            
            VersionTabs.TabPages.Add(new TabPage("API Mods"));
            VersionTabs.TabPages[0].Controls.Add(APIModsList = new ListBox() {
                Font = GlobalFont,
                Dock = DockStyle.Fill,
                MultiColumn = true,
                SelectionMode = SelectionMode.MultiExtended
            });
            APIModsList.SelectedValueChanged += delegate (object sender, EventArgs e) {
                if (!RepoHelper.IsOffline && !APIModsList.SelectedIndices.Contains(0)) {
                    APIModsList.SelectedIndices.Add(0);
                }
            };

            VersionTabs.TabPages.Add(new TabPage("Advanced"));
            VersionTabs.TabPages[1].Controls.Add(AdvancedPanel = new Panel() {
                Font = GlobalFont,
                Dock = DockStyle.Fill,
                AutoScroll = true
            });
            AdvancedPanel.Controls.Add(AdvancedAddButton = new Button() {
                Font = GlobalFont,
                Image = LoadAsset<Image>("icons.open"),
                ImageAlign = ContentAlignment.MiddleCenter
            });
            AdvancedAddButton.Click += (object senderClick, EventArgs eClick) => OpenModDialog.ShowDialog(this);
            AdvancedPanel.Controls.Add(AdvancedLabel = new Label() {
                Font = GlobalFont,
                Text = "or drag-and-drop a folder / .zip here",
                TextAlign = ContentAlignment.MiddleCenter
            });
            AdvancedPanel.Controls.Add(AdvancedAutoRunCheckbox = new CheckBox() {
                Font = GlobalFont,
                Text = "CLOSE " + ETGFinder.MainName + " && run when mod installed",
                TextAlign = ContentAlignment.MiddleCenter
            });
            if (ETGFinder.Platform.HasFlag(ETGPlatform.MacOS)) {
                AdvancedAutoRunCheckbox.Text = "CLOSE Gungeon && run when mod installed";
            }
            AdvancedPanel.Controls.Add(AdvancedOfflineCheckbox = new CheckBox() {
                Font = GlobalFont,
                Text = "Offline mode - only use the APIs here",
                TextAlign = ContentAlignment.MiddleCenter
            });
            AdvancedOfflineCheckbox.CheckedChanged += delegate (object senderCheck, EventArgs eCheck) {
                ETGModder.IsOffline = RepoHelper.IsOffline = AdvancedOfflineCheckbox.Checked;
                DownloadModsList();
            };
            AdvancedPanel.Controls.Add(AdvancedBinaryWrappedCheckbox = new CheckBox() {
                Font = GlobalFont,
                Text = ETGFinder.MainName + " is a wrapper, use EtG.bin",
                TextAlign = ContentAlignment.MiddleCenter
            });
            AdvancedBinaryWrappedCheckbox.CheckedChanged += delegate (object senderCheck, EventArgs eCheck) {
                ETGFinder.IsBinaryWrapped = AdvancedBinaryWrappedCheckbox.Checked;
            };
            
            RefreshManualPanel();
        }

        private void ResetSize(object sender = null, EventArgs e = null) {
            SizeChanged -= ResetSize;

            MinimumSize = Size = MaximumSize = BackgroundImage.Size + (ETGFinder.Platform.HasFlag(ETGPlatform.Windows) ? new Size(8, 8) : new Size());

            SizeChanged += ResetSize;
        }

        public void AddManualPathRows(string[] paths) {
            for (int i = 0; i < paths.Length; i++) {
                AddManualPathRow(paths[i]);
            }
        }

        public void AddManualPathRow(string path) {
            TextBox pathBox;
            AdvancedPanel.Controls.Add(pathBox = new TextBox() {
                Font = GlobalFont,
                ReadOnly = true,
                Text = path
            });
            AdvancedPathBoxes.Add(pathBox);
            Button removeButton;
            AdvancedPanel.Controls.Add(removeButton = new Button() {
                Bounds = new Rectangle(),
                Font = GlobalFont,
                Image = LoadAsset<Image>("icons.uninstall"),
                ImageAlign = ContentAlignment.MiddleCenter
            });
            AdvancedRemoveButtons.Add(removeButton);
            removeButton.Click += delegate (object senderClick, EventArgs eClick) {
                AdvancedPanel.Controls.Remove(pathBox);
                AdvancedPathBoxes.Remove(pathBox);
                AdvancedPanel.Controls.Remove(removeButton);
                AdvancedRemoveButtons.Remove(removeButton);
                RefreshManualPanel();
            };

            RefreshManualPanel();
        }

        public void RefreshManualPanel() {
            int width = VersionTabs.Width - SystemInformation.VerticalScrollBarWidth;

            int y = 0;
            for (int i = 0; i < AdvancedPathBoxes.Count; i++) {
                TextBox pathBox = AdvancedPathBoxes[i];
                Button removeButton = AdvancedRemoveButtons[i];
                pathBox.Bounds = new Rectangle(0, y, width - 32 - 8, 24);
                removeButton.Bounds = new Rectangle(pathBox.Bounds.X + pathBox.Bounds.Width, pathBox.Bounds.Y, 32, pathBox.Bounds.Height);
                y += pathBox.Bounds.Height;
            }

            if (ETGFinder.Platform.HasFlag(ETGPlatform.MacOS)) {
                if (VersionTabs.SelectedIndex != 1) {
                    AdvancedPanel.HideDeep();
                } else {
                    AdvancedPanel.ShowDeep();
                }
            }

            AdvancedAddButton.Bounds = new Rectangle(0, y, width - 8, 24); y += 24;
            AdvancedLabel.Bounds = new Rectangle(0, y, width - 8, 24); y += 24;
            AdvancedAutoRunCheckbox.Bounds = new Rectangle(0, y, width - 8, 24); y += 24;
            AdvancedOfflineCheckbox.Bounds = new Rectangle(0, y, width - 8, 24); y += 24;
            AdvancedBinaryWrappedCheckbox.Bounds = new Rectangle(0, y, width - 8, 24); y += 24;
        }
        public InstallerWindow SetMainEnabled(bool enabled) {
            return Invoke(delegate() {
                ExePathBox.Enabled = enabled;
                ExePathButton.Enabled = enabled;
                VersionTabs.Enabled = enabled;
                APIModsList.Enabled = enabled;
                InstallButton.Enabled = enabled && MainMod != null;
                UninstallButton.Enabled = enabled;
            });
        }

        private bool downloadingModsList = false;
        public void DownloadModsList() {
            if (downloadingModsList) {
                return;
            }
            downloadingModsList = true;

            Invoke(delegate() {
                APIModsList.BeginUpdate();
                APIModsList.Items.Add("Downloading list...");
                APIModsList.EndUpdate();
            });
            
            try {
                APIMods = RepoHelper.GetAPIMods();
            } catch (Exception e) {
                APIMods = null;
                LogLine("Something went horribly wrong:");
                LogLine(e.ToString());
                Invoke(delegate() {
                    APIModsList.BeginUpdate();
                    APIModsList.Items.Clear();
                    APIModsList.Items.Add("Something went wrong - see the log.");
                    APIModsList.EndUpdate();
                });
                return;
            }
            
            Invoke(delegate() {
                APIModsList.BeginUpdate();
                APIModsList.Items.Clear();
                for (int i = 0; i < APIMods.Count; i++) {
                    APIModsList.Items.Add(APIMods[i].Item1);
                }
                APIModsList.SelectedIndices.Clear();

                if (RepoHelper.IsOffline) {
                    APIModsList.SelectionMode = SelectionMode.None;
                } else {
                    APIModsList.SelectionMode = SelectionMode.MultiExtended;
                    APIModsList.SelectedIndices.Add(0);
                }
 
                APIModsList.EndUpdate();
            });

            downloadingModsList = false;
        }
        
        public void Add(Control c) {
            c.Bounds = new Rectangle(448, 4 + 26 * AddIndex + AddOffset, 312, 24);
            Controls.Add(c);
            AddIndex++;
        }
        
        public InstallerWindow Invoke(Action d) {
            if (GUIThread == null) {
                delayed.Add(d);
                return this;
            }

            if (GUIThread == Thread.CurrentThread) {
                d();
                return this;
            }

            if (IsDisposed) {
                return this;
            }

            base.Invoke(d);
            return this;
        }
        public void Wait(int sleep = 100) {
            bool done = false;
            Invoke(delegate () {
                done = true;
            });
            while (!done) {
                Thread.Sleep(sleep);
            }
        }
        
        public InstallerWindow InitProgress(string str, int max) {
            return Invoke(delegate() {
                Progress.Value = 0;
                Progress.Maximum = max;
                Progress.Text = str;
                Progress.Invalidate();
                OnActivity(ActivityOnProgress);
            });
        }
        public InstallerWindow SetProgress(int val) {
            return Invoke(delegate() {
                Progress.Value = val;
                Progress.Invalidate();
                OnActivity(ActivityOnProgress);
            });
        }
        public InstallerWindow SetProgress(string str, int val) {
            return Invoke(delegate() {
                Progress.Value = val;
                Progress.Text = str;
                Progress.Invalidate();
                OnActivity(ActivityOnProgress);
            });
        }
        public InstallerWindow EndProgress() {
            return Invoke(delegate() {
                Progress.Value = Progress.Maximum;
                Progress.Invalidate();
                OnActivity(ActivityOnProgress);
            });
        }
        public InstallerWindow EndProgress(string str) {
            return Invoke(delegate() {
                Progress.Value = Progress.Maximum;
                Progress.Text = str;
                Progress.Invalidate();
                OnActivity(ActivityOnProgress);
            });
        }
        
        private List<string> logScheduled = new List<string>();
        private Task logUpdateTask;
        public InstallerWindow Log(string s) {
            logScheduled.Add(s);
            if (logUpdateTask == null) {
                logUpdateTask = Task.Run((Action) LogFlush);
            }
            return this;
        }
        public InstallerWindow LogLine() {
            Log("\n");
            return this;
        }
        public InstallerWindow LogLine(string s) {
            Log(s);
            LogLine();
            return this;
        }
        public void LogFlush() {
            Thread.Sleep(100);
            string added = "";
            while (0 < logScheduled.Count) {
                added += logScheduled[0];
                logScheduled.RemoveAt(0);
            }
            Invoke(delegate() {
                LogBox.Visible = true;
                LogBox.Text += added;
                LogBox.SelectionStart = LogBox.Text.Length;
                LogBox.SelectionLength = 0;
                LogBox.ScrollToCaret();
                OnActivity(ActivityOnLog);
            });
            logUpdateTask = null;
        }
        
        private void onHandleCreated(object sender, EventArgs e) {
            HandleCreated -= onHandleCreated;
            GUIThread = Thread.CurrentThread;

            ETGInstallerSettings.Load();
            ETGInstallerSettings.Save();

            if (string.IsNullOrWhiteSpace(ExePathBox.Text)) {
                Task.Run((Action) ETGFinder.FindETG);
            }
            Task.Run((Action) DownloadModsList);
        }

        private void onDragDrop(object sender, DragEventArgs e) {
            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null || files.Length == 0) {
                return;
            }

            if (ETGFinder.Platform.HasFlag(ETGPlatform.MacOS) &&
                Directory.Exists(files[0]) &&
                files[0].ToLower().EndsWith(".app/")) { // Is that trailing slash existing on every platform?
                this.ExeSelected(Path.Combine(files[0], "Contents", "MacOS", ETGFinder.MainName), " [app]");
                return;
            }

            if (Directory.Exists(files[0]) ||
                files[0].ToLower().EndsWith(".zip") ||
                files[0].ToLower().EndsWith(".mm.dll")) {
                AddManualPathRow(files[0]);
                return;
            }
        }

        [STAThread]
        public static void Main(string[] args) {
            if (args.Length == 2 && (args[0] == "--clearsymbols" || args[0] == "-cs")) {
                InstantClearSymbols = args[1];
                Thread.Sleep(2000);
            }

            if (args.Length == 2 && (args[0] == "--postcompile" || args[0] == "-pc")) {
                Console.WriteLine("Running ETGMod.Installer as post compilation task.");
                Console.WriteLine("Version: " + Version);

                args[1] = Path.GetFullPath(args[1]);

                ETGModder.OverridePaths = new List<string>();
                Console.WriteLine("Scanning for builds in " + args[1]);
                string[] files = Directory.GetFiles(args[1]);
                for (int i = 0; i < files.Length; i++) {
                    string file = files[i];
                    if (!file.EndsWith(".mm.dll")) {
                        continue;
                    }
                    ETGModder.OverridePaths.Add(file);
                    Console.WriteLine("Added " + file);
                }

                ETGInstallerSettings.SaveEnabled = false;
                ETGFinder.OnExeSelected += delegate (bool selected) {
                    if (!selected) {
                        throw new InvalidOperationException("ETGMod.Installer not prepared to run as post compilation task! Set up the .exe path first!");
                    }

                    ETGModder.OnInstalled += delegate (bool installed) {
                        if (installed) {
                            Environment.Exit(0);
                        }
                    };
                    Task.Run((Action) Instance.Install);
                };
                new InstallerWindow();

                Instance.ShowDialog();

                return;
            }

            Console.WriteLine("Entering the holy realm of ETGMod.");
            Application.EnableVisualStyles();

            Assembly assembly = Assembly.GetExecutingAssembly();
            string[] manifestResourceNames = assembly.GetManifestResourceNames();
            for (int i = 0; i < manifestResourceNames.Length; i++) {
                Console.WriteLine("Asset " + i + ": " + manifestResourceNames[i]);
            }

            Application.VisualStyleState = VisualStyleState.ClientAndNonClientAreasEnabled;
            new InstallerWindow();

#if DEBUG
            Instance.ShowDialog();
#else
            ShowDialog:
            try {
                Instance.ShowDialog();
            } catch (Exception e) {
                //Gonna blame X11.
                Console.WriteLine(e.ToString());
                MessageBox.Show("Your window manager has left the building!\nThis simply means the installer crashed,\nbut your window manager caused it.", "ETGMod Installer");
                goto ShowDialog;
            }
#endif
        }

        public void ShuffleIconColors() {
            Icon origIcon = LoadAsset<Icon>("icons.main");
            Bitmap bitmap = origIcon.ToBitmap();

            Dictionary<int, Color> map = new Dictionary<int, Color>();
            Random rand = new Random();
            for (int y = 0; y < bitmap.Height; y++) {
                for (int x = 0; x < bitmap.Width; x++) {
                    Color co = bitmap.GetPixel(x, y);
                    Color cn;
                    if (!map.TryGetValue(co.ToArgb(), out cn)) {
                        cn = Color.FromArgb(co.A, rand.Next(255), rand.Next(255), rand.Next(255));
                        map[co.ToArgb()] = cn;
                    }
                    bitmap.SetPixel(x, y, cn);
                }
            }

            Icon newIcon = Icon.FromHandle(bitmap.GetHicon());
            Icon = newIcon;
            bitmap.Dispose();
            origIcon.Dispose();
            newIcon.Dispose();
        }

        private int activity = 0;

        public int ActivityOnProgress = 64;
        public int ActivityOnLog = 24;

        public int ActivityForIconSwap = 128;

        public void OnActivity(int add = 1) {
            activity += add;

            if (activity % ActivityForIconSwap == 0) {
                Invoke(ShuffleIconColors);
            }
        }

        public static T LoadAsset<T>(string name, bool fullPath = false) where T : class {
            Assembly assembly = Assembly.GetExecutingAssembly();
            Type t = typeof(T);

            if (t == typeof(Image) || t == typeof(Bitmap)) {
                using (Stream s = assembly.GetManifestResourceStream(fullPath ? name : "ETGMod.Installer.Assets." + name + ".png")) {
                    return Image.FromStream(s) as T;
                }
            }

            if (t == typeof(Icon)) {
                return Icon.FromHandle(LoadAsset<Bitmap>(name, fullPath).GetHicon()) as T;
            }

            if (t == typeof(PrivateFontCollection)) {
                PrivateFontCollection pfc = new PrivateFontCollection();
                byte[] data;
                using (Stream s = assembly.GetManifestResourceStream(fullPath ? name : "ETGMod.Installer.Assets." + name + ".ttf")) {
                    data = new byte[s.Length];
                    s.Read(data, 0, (int) s.Length);
                }
                //yeeey, unsafe!
                unsafe {
                    fixed (byte* pData = data) {
                        pfc.AddMemoryFont((IntPtr) pData, data.Length);
                    }
                }
                return pfc as T;
            }

            return default(T);
        }

    }
}
