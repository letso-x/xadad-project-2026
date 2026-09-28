using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using MzuApplication.Controls;
using MzuApplication.Data;
using MzuApplication.Models;
using MzuApplication.Views;

namespace MzuApplication.Forms
{
    /// <summary>
    /// Application shell. The sidebar docks Left on the form; everything else lives
    /// inside a Fill panel to its right, so the two never overlap. The sidebar
    /// collapses to an icon rail via the toggle in the top bar.
    /// </summary>
    public class MainForm : Form
    {
        /// <summary>Width of the sidebar when collapsed to icons only.</summary>
        private const int RailWidth = 60;

        private Panel _sidebar;
        private Panel _mainArea;
        private Panel _topBar;
        private Panel _contentHost;
        private TextInput _search;
        private IconButton _toggle;
        private ToolTip _tips;

        private readonly List<NavLink> _navLinks = new List<NavLink>();
        private readonly List<Label> _groupLabels = new List<Label>();
        private readonly List<Control> _expandedOnly = new List<Control>();

        /// <summary>Number of links in each nav group, in declaration order.</summary>
        private readonly List<int> _groupSizes = new List<int>();

        private ViewBase _currentView;
        private bool _collapsed;

        public MainForm()
        {
            Text = "Mzukulu QMS — Quality Management System";

            // Size the window to the design dimensions scaled for DPI, but never
            // larger than the working area. Without the clamp the scaled default
            // (e.g. 1280x820 -> 1920x1230 at 1.5x) can exceed a 1080p screen, so
            // Windows pushes the top of the window — sidebar and search bar — off
            // screen, which is exactly the "missing chrome" symptom.
            Rectangle workArea = Screen.PrimaryScreen.WorkingArea;
            int desiredW = Math.Min(Dpi.S(1280), workArea.Width);
            int desiredH = Math.Min(Dpi.S(820), workArea.Height);
            ClientSize = new Size(desiredW, desiredH);

            // Minimum must also fit the screen, or Windows silently enlarges the
            // window past the work area again.
            MinimumSize = new Size(
                Math.Min(Dpi.S(900), workArea.Width),
                Math.Min(Dpi.S(640), workArea.Height));

            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = true;
            BackColor = Theme.Slate25;
            DoubleBuffered = true;
            Font = Theme.Body;

            // Final guard: once the frame exists, clamp the whole window (including
            // its border) to the work area so no chrome lands off-screen.
            Load += ClampToScreen;

            _tips = new ToolTip { InitialDelay = 350, ReshowDelay = 120 };

            // Order matters: the Fill panel is added first, then the Left sidebar, so
            // the sidebar reserves its strip and the main area fills what remains.
            BuildMainArea();
            BuildSidebar();

            Navigate(new DashboardView(this));
        }

        /// <summary>Shows a transient notification in the bottom-right corner.</summary>
        public void Notify(string message, bool isError = false)
        {
            Toast.Show(this, message, isError);
        }

        /// <summary>
        /// Keeps the full window within the screen's work area, so the DPI-scaled
        /// window can never push the sidebar or top bar off the top of the display.
        /// </summary>
        private void ClampToScreen(object sender, EventArgs e)
        {
            Rectangle work = Screen.FromControl(this).WorkingArea;

            int w = Math.Min(Width, work.Width);
            int h = Math.Min(Height, work.Height);
            if (w != Width || h != Height)
            {
                Size = new Size(w, h);
            }

            int x = Math.Max(work.Left, Math.Min(Left, work.Right - Width));
            int y = Math.Max(work.Top, Math.Min(Top, work.Bottom - Height));
            Location = new Point(x, y);
        }

        #region Navigation

        /// <summary>Replaces the current view and highlights the matching nav link.</summary>
        public void Navigate(ViewBase view)
        {
            if (view == null) return;

            if (_currentView != null)
            {
                _contentHost.Controls.Remove(_currentView);
                _currentView.Dispose();
            }

            _currentView = view;
            _currentView.Dock = DockStyle.Top;
            _contentHost.Controls.Add(_currentView);

            _contentHost.AutoScrollPosition = new Point(Dpi.S(0), Dpi.S(0));

            HighlightRoute(view.RouteKey);
            _currentView.Build();
        }

        /// <summary>Rebuilds the current view in place, e.g. after saving a record.</summary>
        public void RefreshCurrentView()
        {
            if (_currentView == null) return;
            _currentView.Rebuild();
        }

        private void HighlightRoute(string routeKey)
        {
            foreach (NavLink link in _navLinks)
            {
                link.Active = link.Route == routeKey;
            }
        }

        /// <summary>Maps a sidebar route key to its view.</summary>
        public void NavigateRoute(string route)
        {
            switch (route)
            {
                case "dashboard": Navigate(new DashboardView(this)); break;
                case "clients": Navigate(new ClientsView(this)); break;
                case "projects": Navigate(new ProjectsView(this)); break;
                case "library": Navigate(new LibraryView(this)); break;
                case "search": Navigate(new SearchView(this, string.Empty)); break;
                case "ncr": Navigate(new NcrView(this)); break;
                case "reports": Navigate(new ReportsView(this)); break;
                case "templates": Navigate(new TemplatesView(this)); break;
                case "audit": Navigate(new AuditView(this)); break;
                case "users": Navigate(new UsersView(this)); break;
                case "settings": Navigate(new SettingsView(this)); break;
            }
        }

        #endregion

        #region Main area (top bar + content)

        private void BuildMainArea()
        {
            _mainArea = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Slate25
            };
            Controls.Add(_mainArea);

            _contentHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Theme.Slate25,
                AutoScroll = true
            };

            _topBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = Theme.TopHeaderHeight,
                BackColor = Color.FromArgb(250, 251, 252)
            };

            _toggle = new IconButton
            {
                Glyph = Glyph.Menu,
                Location = new Point(Theme.PagePadding - 6, 13),
                Size = new Size(Dpi.S(34), Dpi.S(34)),
                BorderTint = Theme.Slate150,
                IdleColor = Theme.Slate600
            };
            _toggle.Click += (s, e) => SetCollapsed(!_collapsed);
            _tips.SetToolTip(_toggle, "Collapse sidebar");
            _topBar.Controls.Add(_toggle);

            _search = new TextInput
            {
                Glyph = Glyph.Search,
                Rounded = true,
                Placeholder = "Search projects, forms, clients, record numbers...",
                Location = new Point(_toggle.Right + 12, 13),
                Width = Dpi.S(420),
                Height = Dpi.S(34)
            };
            _search.Submitted += (s, e) => Navigate(new SearchView(this, _search.Value));
            _topBar.Controls.Add(_search);

            Panel rule = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = Dpi.S(1),
                BackColor = Theme.Slate150
            };
            _topBar.Controls.Add(rule);

            _topBar.Resize += (s, e) =>
            {
                int available = _topBar.Width - _search.Left - Theme.PagePadding;
                _search.Width = Math.Max(160, Math.Min(420, available));
            };

            // Content first, then top bar, so Top reserves its strip above Fill.
            _mainArea.Controls.Add(_contentHost);
            _mainArea.Controls.Add(_topBar);
        }

        #endregion

        #region Sidebar

        private void BuildSidebar()
        {
            _sidebar = new Panel
            {
                BackColor = Theme.Navy950,
                Dock = DockStyle.Left,
                Width = Theme.SidebarWidth
            };
            Controls.Add(_sidebar);

            BuildBrandBlock();
            BuildSidebarFooter();
            BuildNavLinks();
        }

        private void BuildBrandBlock()
        {
            Panel brandBlock = new Panel
            {
                Dock = DockStyle.Top,
                Height = Dpi.S(70),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };

            MzukuluLogo mark = new MzukuluLogo
            {
                Location = new Point(Dpi.S(14), Dpi.S(19)),
                Size = new Size(Dpi.S(32), Dpi.S(32)),
                Name = "brandMark"
            };
            brandBlock.Controls.Add(mark);

            Label brandName = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Location = new Point(Dpi.S(54), Dpi.S(18)),
                Text = "Mzukulu QMS"
            };
            brandBlock.Controls.Add(brandName);
            _expandedOnly.Add(brandName);

            Label brandSub = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 6.75F, FontStyle.Regular),
                ForeColor = Color.FromArgb(120, 255, 255, 255),
                BackColor = Color.Transparent,
                Location = new Point(Dpi.S(56), Dpi.S(38)),
                Text = Theme.Track("QUALITY MANAGEMENT SYSTEM")
            };
            brandBlock.Controls.Add(brandSub);
            _expandedOnly.Add(brandSub);

            EventHandler goHome = (s, e) => Navigate(new DashboardView(this));
            brandBlock.Click += goHome;
            mark.Click += goHome;
            brandName.Click += goHome;

            _sidebar.Controls.Add(brandBlock);

            Panel brandRule = new Panel
            {
                Dock = DockStyle.Top,
                Height = Dpi.S(1),
                BackColor = Color.FromArgb(20, 255, 255, 255)
            };
            _sidebar.Controls.Add(brandRule);
        }

        private void BuildNavLinks()
        {
            Panel navHost = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Transparent,
                Padding = new Padding(Dpi.S(12), Dpi.S(14), Dpi.S(12), Dpi.S(8)),
                Name = "navHost"
            };

            int y = 4;

            y = AddNavGroup(navHost, "Operations", y, new[]
            {
                Tuple.Create("dashboard", "Dashboard", Glyph.Grid),
                Tuple.Create("clients", "Clients", Glyph.Building),
                Tuple.Create("projects", "Projects", Glyph.Folder),
                Tuple.Create("library", "Form Library", Glyph.Layers),
                Tuple.Create("search", "Search", Glyph.Search)
            });

            // Quality is visible to everyone: site users raise and work NCRs.
            y = AddNavGroup(navHost, "Quality", y + Dpi.S(6), new[]
            {
                Tuple.Create("ncr", "Non-Conformance", Glyph.Alert),
                Tuple.Create("reports", "Reports", Glyph.BarChart)
            });

            // Administration group is admin-only, matching the route guards.
            if (Repository.IsAdmin)
            {
                AddNavGroup(navHost, "Administration", y + Dpi.S(6), new[]
                {
                    Tuple.Create("templates", "Checklist Builder", Glyph.Tool),
                    Tuple.Create("audit", "Audit Trail", Glyph.Shield),
                    Tuple.Create("users", "Users", Glyph.Users),
                    Tuple.Create("settings", "Settings", Glyph.Gear)
                });
            }

            _sidebar.Controls.Add(navHost);
        }

        private int AddNavGroup(Panel host, string title, int y,
                                Tuple<string, string, Glyph>[] items)
        {
            Label groupLabel = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 6.75F, FontStyle.Bold),
                ForeColor = Color.FromArgb(90, 255, 255, 255),
                BackColor = Color.Transparent,
                Location = new Point(11, y),
                Text = Theme.Track(title.ToUpperInvariant())
            };
            host.Controls.Add(groupLabel);
            _groupLabels.Add(groupLabel);
            _groupSizes.Add(items.Length);
            y = groupLabel.Bottom + Dpi.S(7);

            foreach (var item in items)
            {
                NavLink link = new NavLink
                {
                    Route = item.Item1,
                    Text = item.Item2,
                    Glyph = item.Item3,
                    Location = new Point(0, y),
                    Width = Theme.SidebarWidth - 28
                };

                string route = item.Item1;
                link.Click += (s, e) => NavigateRoute(route);
                _tips.SetToolTip(link, item.Item2);

                host.Controls.Add(link);
                _navLinks.Add(link);
                y = link.Bottom + 2;
            }

            return y;
        }

        private void BuildSidebarFooter()
        {
            Panel footer = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = Dpi.S(62),
                BackColor = Color.Transparent,
                Name = "sidebarFooter"
            };

            Panel rule = new Panel
            {
                Dock = DockStyle.Top,
                Height = Dpi.S(1),
                BackColor = Color.FromArgb(20, 255, 255, 255)
            };
            footer.Controls.Add(rule);

            Avatar avatar = new Avatar
            {
                Text = Format.Initials(Repository.CurrentUserName),
                Location = new Point(Dpi.S(14), Dpi.S(16)),
                Size = new Size(Dpi.S(32), Dpi.S(32)),
                Name = "userAvatar"
            };
            _tips.SetToolTip(avatar, Repository.CurrentUserName
                + " (" + Format.RoleName(Repository.CurrentRole) + ")");
            footer.Controls.Add(avatar);

            Label name = new Label
            {
                AutoSize = true,
                Font = Theme.SmallBold,
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Location = new Point(Dpi.S(54), Dpi.S(16)),
                Text = Repository.CurrentUserName
            };
            footer.Controls.Add(name);
            _expandedOnly.Add(name);

            Label role = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 6.75F, FontStyle.Regular),
                ForeColor = Color.FromArgb(120, 255, 255, 255),
                BackColor = Color.Transparent,
                Location = new Point(Dpi.S(56), Dpi.S(34)),
                Text = Theme.Track(Format.RoleBadge(Repository.CurrentRole))
            };
            footer.Controls.Add(role);
            _expandedOnly.Add(role);

            IconButton switchUser = new IconButton
            {
                Glyph = Glyph.Logout,
                Location = new Point(Theme.SidebarWidth - 42, 22),
                Size = new Size(Dpi.S(26), Dpi.S(26)),
                IdleColor = Color.FromArgb(110, 255, 255, 255),
                HoverColor = Color.White,
                HoverFill = Color.FromArgb(22, 255, 255, 255),
                Name = "switchUser"
            };
            switchUser.Click += (s, e) => SwitchUser();
            _tips.SetToolTip(switchUser, "Switch user");
            footer.Controls.Add(switchUser);
            _expandedOnly.Add(switchUser);

            _sidebar.Controls.Add(footer);
        }

        /// <summary>Signs out and returns to the login screen.</summary>
        private void SwitchUser()
        {
            Repository.SignOut();
            DialogResult = DialogResult.Retry;
            Close();
        }

        #endregion

        #region Collapse

        /// <summary>Whether the sidebar is currently collapsed to an icon rail.</summary>
        public bool SidebarCollapsed
        {
            get { return _collapsed; }
        }

        /// <summary>
        /// Collapses the sidebar to icons or restores the full-width version. The main
        /// area reflows automatically because it is docked Fill beside the sidebar.
        /// </summary>
        public void SetCollapsed(bool collapsed)
        {
            if (_collapsed == collapsed) return;
            _collapsed = collapsed;

            SuspendLayout();
            _sidebar.SuspendLayout();

            _sidebar.Width = collapsed ? RailWidth : Theme.SidebarWidth;

            // Text labels only make sense at full width.
            foreach (Control c in _expandedOnly)
            {
                c.Visible = !collapsed;
            }

            foreach (Label label in _groupLabels)
            {
                label.Visible = !collapsed;
            }

            int linkWidth = collapsed ? RailWidth - 16 : Theme.SidebarWidth - 28;
            foreach (NavLink link in _navLinks)
            {
                link.Collapsed = collapsed;
                link.Width = linkWidth;
            }

            RelayoutNavHost();
            CentreRailControls(collapsed);

            _sidebar.ResumeLayout(true);
            ResumeLayout(true);

            _toggle.Glyph = Glyph.Menu;
            _tips.SetToolTip(_toggle, collapsed ? "Expand sidebar" : "Collapse sidebar");

            // Views are width-sensitive, so rebuild at the new size.
            RefreshCurrentView();
        }

        /// <summary>
        /// Re-stacks the nav links, closing the gaps left by hidden group headings when
        /// the rail is collapsed.
        /// </summary>
        private void RelayoutNavHost()
        {
            Panel navHost = null;
            foreach (Control c in _sidebar.Controls)
            {
                if (c.Name == "navHost") { navHost = c as Panel; break; }
            }
            if (navHost == null) return;

            int y = Dpi.S(4);
            int linkIndex = 0;
            int groupIndex = 0;

            // Group sizes are recorded as the groups are built, so this stays correct
            // when navigation items are added or the role changes.
            foreach (int count in _groupSizes)
            {
                if (groupIndex < _groupLabels.Count)
                {
                    Label heading = _groupLabels[groupIndex];
                    if (!_collapsed)
                    {
                        heading.Location = new Point(11, y);
                        y = heading.Bottom + 7;
                    }
                    else
                    {
                        // Small breathing space instead of the heading.
                        y += groupIndex == 0 ? 2 : 10;
                    }
                    groupIndex++;
                }

                for (int i = 0; i < count && linkIndex < _navLinks.Count; i++, linkIndex++)
                {
                    NavLink link = _navLinks[linkIndex];
                    link.Location = new Point(_collapsed ? 8 : 0, y);
                    y = link.Bottom + 2;
                }

                y += 6;
            }
        }

        /// <summary>Centres the brand mark and avatar when the rail is narrow.</summary>
        private void CentreRailControls(bool collapsed)
        {
            int width = collapsed ? RailWidth : Theme.SidebarWidth;

            Control mark = FindDeep(_sidebar, "brandMark");
            if (mark != null)
            {
                mark.Left = collapsed ? (width - mark.Width) / 2 : Dpi.S(14);
            }

            Control avatar = FindDeep(_sidebar, "userAvatar");
            if (avatar != null)
            {
                avatar.Left = collapsed ? (width - avatar.Width) / 2 : Dpi.S(14);
            }

            Control switchUser = FindDeep(_sidebar, "switchUser");
            if (switchUser != null && !collapsed)
            {
                switchUser.Left = Theme.SidebarWidth - Dpi.S(42);
            }
        }

        private static Control FindDeep(Control parent, string name)
        {
            foreach (Control child in parent.Controls)
            {
                if (child.Name == name) return child;

                Control found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }

        #endregion

        /// <summary>Usable width for a view, excluding the scrollbar gutter.</summary>
        public int ContentWidth
        {
            get { return Math.Max(600, _contentHost.ClientSize.Width); }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _tips != null) _tips.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>A small square icon-only button.</summary>
    internal sealed class IconButton : Control
    {
        private bool _hovered;
        private Glyph _glyph = Glyph.Pencil;

        public IconButton()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.UserPaint
                     | ControlStyles.ResizeRedraw
                     | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Size = new Size(Dpi.S(29), Dpi.S(29));
            Cursor = Cursors.Hand;
        }

        public Glyph Glyph
        {
            get { return _glyph; }
            set { _glyph = value; Invalidate(); }
        }

        public Color IdleColor { get; set; } = Theme.Slate500;
        public Color HoverColor { get; set; } = Theme.Slate900;
        public Color HoverFill { get; set; } = Theme.Slate50;
        public Color BorderTint { get; set; } = Color.Transparent;

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hovered = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            if (r.Width <= 0 || r.Height <= 0) return;

            using (var path = Card.Build(r, 6, false, false))
            {
                if (_hovered)
                {
                    using (SolidBrush b = new SolidBrush(HoverFill))
                    {
                        g.FillPath(b, path);
                    }
                }

                if (BorderTint != Color.Transparent)
                {
                    using (Pen p = new Pen(BorderTint))
                    {
                        g.DrawPath(p, path);
                    }
                }
            }

            int size = 16;
            Glyphs.Draw(g, _glyph,
                new Rectangle((Width - size) / 2, (Height - size) / 2, size, size),
                _hovered ? HoverColor : IdleColor, 1.7f);
        }
    }
}
