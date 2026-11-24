using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace WindowsTrayToys
{
    /// <summary>"Start with Windows": a per-user Run entry, written only when the user asks for it.</summary>
    static class AutoStart
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run", Name = "WindowsTrayToys";

        /// <summary>
        /// The project used to be called Glass Breaker. If the user had ticked Start with Windows
        /// under that name, replace the old entry with one for this exe so sign-in still starts it.
        /// </summary>
        public static void MigrateOldName()
        {
            try
            {
                bool had;
                using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    had = k != null && k.GetValue("Glass" + "Breaker") != null;
                    if (had) k.DeleteValue("Glass" + "Breaker", false);
                }
                if (had) Set(true);
            }
            catch (Exception) { }
        }

        public static bool Get()
        {
            try
            {
                using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey))
                    return k != null && k.GetValue(Name) != null;
            }
            catch (Exception) { return false; }
        }

        public static void Set(bool on)
        {
            try
            {
                using (Microsoft.Win32.RegistryKey k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    // --autostart tells the app it was launched at sign-in, so it starts without the welcome tip
                    if (on) k.SetValue(Name, "\"" + Application.ExecutablePath + "\" --autostart");
                    else k.DeleteValue(Name, false);
                }
            }
            catch (Exception ex) { MessageBox.Show("Could not change the startup setting: " + ex.Message, "Windows Tray Toys"); }
        }
    }

    /// <summary>
    /// The settings window. Every control writes its setting the moment it changes and tells the
    /// app to apply it, so there is no OK or Cancel. Pages are filled with the Add* helpers, which
    /// lay rows out top to bottom.
    /// </summary>
    sealed class SettingsForm : Form
    {
        readonly float scale;
        readonly Action changed;
        readonly TabControl tabs = new TabControl();
        readonly Dictionary<string, TabPage> pages = new Dictionary<string, TabPage>();
        readonly Dictionary<string, int> nextY = new Dictionary<string, int>();
        readonly List<Action> refreshers = new List<Action>();
        bool refreshing;

        public SettingsForm(float scale, Action changed, Icon icon, bool hotkeyOk)
        {
            this.scale = scale; this.changed = changed;
            Text = "Windows Tray Toys settings";
            Icon = icon;
            Font = new Font("Segoe UI", 9f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(S(460), S(480));

            tabs.SetBounds(S(10), S(10), S(440), S(420));
            Controls.Add(tabs);
            Button close = new Button();
            close.Text = "Close";
            close.SetBounds(S(360), S(440), S(90), S(28));
            close.Click += delegate { Close(); };
            Controls.Add(close);
            AcceptButton = CancelButton = close;

            // General
            AddCheck("General", "Enabled" + (hotkeyOk ? "  (" + HotkeyWindow.Display + " pauses and clears)" : ""), "enabled", true);
            AddCheck("General", "Sound", "sound", true);
            AddSlider("General", "Volume", "volume", 100, 0, 100, 10, " %");
            AddCheck("General", "Start with Windows", delegate { return AutoStart.Get(); }, delegate(bool on) { AutoStart.Set(on); });
            AddCheck("General", "Start paused", "startpaused", false);
            AddCheck("General", "Pause while a full-screen app is in front (games, films)", "fullscreen", true);
            AddCheck("General", "Keep marks until repaired", "persist", false);
            AddSlider("General", "Marks stay for", "hold", 6, 1, 60, 1, " s");
            AddCheck("General", "Chain reaction: six glass breaks close together drop the pane", "chain", true);
            AddStatus("General", TrayApp.MarksMade);

            // Clicks
            List<string> keys = new List<string>(), names = new List<string>();
            keys.Add("none"); names.Add("Nothing");
            foreach (EffectDef e in Effects.All) { keys.Add(e.Key); names.Add(e.Name); }
            keys.Add("random"); names.Add("Random");
            AddNote("Clicks", "Choose what each mouse button does. Your click always reaches the app underneath.");
            AddChoice("Clicks", "Left click", "left", "glass", keys.ToArray(), names.ToArray());
            AddChoice("Clicks", "Middle click", "middle", "none", keys.ToArray(), names.ToArray());
            AddChoice("Clicks", "Right click", "right", "none", keys.ToArray(), names.ToArray());
            AddChoice("Clicks", "Mark size", "size", "normal", new[] { "small", "normal", "large", "huge" }, new[] { "Small", "Normal", "Large", "Huge" });
            AddSlider("Clicks", "Most marks at once", "maxmarks", 60, 10, 150, 10, "");

            // Desktop: one row per always-on feature
            AddNote("Desktop", "Things that live on your desktop without needing a click.");
            foreach (AmbientDef a in Ambients.All)
            {
                if (a.Keys == null) AddCheck("Desktop", a.Title, a.Key, false);
                else AddChoice("Desktop", a.Title, a.Key, a.Off, a.Keys, a.Names);
                if (a.Key == "bugs") AddSlider("Desktop", "Number of bugs", "bugcount", 7, 1, 25, 1, "");
            }

            BuildAbout();
        }

        public const string Home = "https://github.com/mahatab/windows-tray-toys";

        void BuildAbout()
        {
            System.Reflection.Assembly asm = System.Reflection.Assembly.GetExecutingAssembly();
            Version v = asm.GetName().Version;
            TabPage p = Page("About");
            Label title = new Label();
            title.Text = Application.ProductName;
            title.Font = new Font("Segoe UI Semibold", 16f);
            title.SetBounds(S(16), nextY["About"], S(400), S(36));
            p.Controls.Add(title);
            nextY["About"] += S(40);
            AddNote("About", "Version " + v.Major + "." + v.Minor + "." + v.Build);
            AddNote("About", "A box of desktop toys. Click anywhere to break your screen, then let creatures and weather loose on it. Nothing is actually harmed, and your clicks still reach whatever is underneath.");
            AddLink("About", "Project page, source code and new releases", Home);
            AddLink("About", "Report a problem or suggest an effect", Home + "/issues");
            AddNote("About", "Free and open source under the MIT license. Every picture and sound is generated by the program itself.");
        }

        /// <summary>A link that opens in the default browser when clicked.</summary>
        public LinkLabel AddLink(string page, string text, string url)
        {
            TabPage p = Page(page);
            LinkLabel l = new LinkLabel();
            l.Text = text;
            l.SetBounds(S(16), nextY[page], S(400), S(22));
            l.LinkClicked += delegate
            {
                try { System.Diagnostics.Process.Start(url); }
                catch (Exception) { MessageBox.Show(url, Text); }
            };
            p.Controls.Add(l);
            nextY[page] += S(28);
            return l;
        }

        int S(int v) { return (int)(v * scale); }

        TabPage Page(string title)
        {
            TabPage p;
            if (pages.TryGetValue(title, out p)) return p;
            p = new TabPage(title);
            p.BackColor = SystemColors.Window;   // a plain colour, so sliders (which cannot be transparent) can match it
            tabs.TabPages.Add(p);
            pages[title] = p;
            nextY[title] = S(14);
            return p;
        }

        void Fire() { if (!refreshing && changed != null) changed(); }

        public Label AddNote(string page, string text)
        {
            TabPage p = Page(page);
            Label l = new Label();
            l.Text = text;
            l.ForeColor = SystemColors.GrayText;
            // tall enough for however many lines the text wraps to
            int tall = TextRenderer.MeasureText(text, Font, new Size(S(400), 0), TextFormatFlags.WordBreak).Height + S(4);
            l.SetBounds(S(16), nextY[page], S(400), tall);
            p.Controls.Add(l);
            nextY[page] += tall + S(8);
            return l;
        }

        /// <summary>A grey line of text that is re-read whenever the window refreshes or comes to the front.</summary>
        public Label AddStatus(string page, Func<string> text)
        {
            Label l = AddNote(page, text());
            refreshers.Add(delegate { l.Text = text(); });
            return l;
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            RefreshAll();
        }

        public CheckBox AddCheck(string page, string text, string key, bool def)
        {
            return AddCheck(page, text, delegate { return Settings.On(key, def); }, delegate(bool on) { Settings.Set(key, on); });
        }

        public CheckBox AddCheck(string page, string text, Func<bool> get, Action<bool> set)
        {
            TabPage p = Page(page);
            CheckBox c = new CheckBox();
            c.Text = text;
            c.SetBounds(S(16), nextY[page], S(400), S(24));
            c.Checked = get();
            c.CheckedChanged += delegate { if (refreshing) return; set(c.Checked); Fire(); };
            refreshers.Add(delegate { c.Checked = get(); });
            p.Controls.Add(c);
            nextY[page] += S(30);
            return c;
        }

        public ComboBox AddChoice(string page, string label, string key, string def, string[] keys, string[] names)
        {
            TabPage p = Page(page);
            Label l = new Label();
            l.Text = label;
            l.SetBounds(S(16), nextY[page] + S(4), S(150), S(22));
            ComboBox c = new ComboBox();
            c.DropDownStyle = ComboBoxStyle.DropDownList;
            c.Items.AddRange(names);
            c.SetBounds(S(170), nextY[page], S(240), S(24));
            Action load = delegate { c.SelectedIndex = Math.Max(0, Array.IndexOf(keys, Settings.Get(key, def))); };
            load();
            c.SelectedIndexChanged += delegate { if (refreshing || c.SelectedIndex < 0) return; Settings.Set(key, keys[c.SelectedIndex]); Fire(); };
            refreshers.Add(load);
            p.Controls.Add(l); p.Controls.Add(c);
            nextY[page] += S(36);
            return c;
        }

        /// <summary>
        /// A slider row. The number beside it follows the thumb, but the setting is only written
        /// when the user lets go, so dragging does not apply a dozen intermediate values.
        /// </summary>
        public TrackBar AddSlider(string page, string label, string key, int def, int min, int max, int step, string unit)
        {
            TabPage p = Page(page);
            Label l = new Label();
            l.Text = label;
            l.SetBounds(S(16), nextY[page] + S(6), S(150), S(22));
            TrackBar t = new TrackBar();
            t.AutoSize = false;
            t.TickStyle = TickStyle.None;
            t.BackColor = SystemColors.Window;
            t.Minimum = min / step; t.Maximum = max / step;
            t.SetBounds(S(164), nextY[page], S(190), S(30));
            Label v = new Label();
            v.SetBounds(S(358), nextY[page] + S(6), S(70), S(22));
            Action show = delegate { v.Text = (t.Value * step) + unit; };
            Action load = delegate
            {
                t.Value = Math.Max(t.Minimum, Math.Min(t.Maximum, Settings.Int(key, def) / step));
                show();
            };
            Action commit = delegate
            {
                int val = t.Value * step;
                if (refreshing || val == Settings.Int(key, def)) return;
                Settings.Set(key, val.ToString());
                Fire();
            };
            load();
            t.ValueChanged += delegate { show(); };
            t.MouseUp += delegate { commit(); };
            t.KeyUp += delegate { commit(); };
            t.LostFocus += delegate { commit(); };
            refreshers.Add(load);
            p.Controls.Add(l); p.Controls.Add(t); p.Controls.Add(v);
            nextY[page] += S(38);
            return t;
        }

        /// <summary>Self-test: a picture of each tab.</summary>
        public List<Bitmap> Snapshots()
        {
            List<Bitmap> shots = new List<Bitmap>();
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-8000, -8000);   // off every screen
            ShowInTaskbar = false;
            Show();
            for (int i = 0; i < tabs.TabPages.Count; i++)
            {
                tabs.SelectedIndex = i;
                Application.DoEvents();
                Bitmap b = new Bitmap(Width, Height);
                DrawToBitmap(b, new Rectangle(0, 0, Width, Height));
                shots.Add(b);
            }
            Hide();
            return shots;
        }

        /// <summary>Re-reads every setting into its control, after something else (the tray menu, the hotkey) changed it.</summary>
        public void RefreshAll()
        {
            refreshing = true;
            try { foreach (Action r in refreshers) r(); }
            finally { refreshing = false; }
        }
    }
}
