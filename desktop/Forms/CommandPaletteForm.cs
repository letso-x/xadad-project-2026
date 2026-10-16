using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MzuApplication.Controls;
using MzuApplication.Data;

namespace MzuApplication.Forms
{
    /// <summary>
    /// A quick-navigation command palette (Ctrl+K). The user types to filter a list of
    /// destinations and presses Enter to jump straight there, so power users can move
    /// around the app without reaching for the sidebar. Styled to match the app chrome.
    /// </summary>
    public class CommandPaletteForm : Form
    {
        /// <summary>A navigable destination: label, keywords, and the route to open.</summary>
        public sealed class Command
        {
            public string Label;
            public string Keywords;
            public string Route;
        }

        private readonly List<Command> _all;
        private List<Command> _filtered;

        private Card _card;
        private TextInput _query;
        private Panel _list;

        /// <summary>The route the user chose, or null if cancelled.</summary>
        public string ChosenRoute { get; private set; }

        public CommandPaletteForm(IEnumerable<Command> commands)
        {
            _all = commands.ToList();
            _filtered = _all;

            Text = "Mzukulu QMS — Go to";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.Navy950;
            ClientSize = new Size(Dpi.S(520), Dpi.S(440));
            DoubleBuffered = true;
            KeyPreview = true;
            ShowInTaskbar = false;

            BuildCard();

            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape) { DialogResult = DialogResult.Cancel; Close(); }
            };

            // Dismiss when the palette loses focus, like a real command bar.
            Deactivate += (s, e) => { if (Visible) { DialogResult = DialogResult.Cancel; Close(); } };
        }

        private void BuildCard()
        {
            int cardWidth = ClientSize.Width;

            _card = new Card
            {
                Width = cardWidth,
                Location = new Point(0, 0),
                Fill = Color.White,
                BorderWidth = 0,
                Radius = Dpi.S(14)
            };

            _query = new TextInput
            {
                Location = new Point(Dpi.S(14), Dpi.S(14)),
                Width = cardWidth - Dpi.S(28),
                Height = Dpi.S(40),
                Glyph = Glyph.Search,
                Placeholder = "Go to… (type a screen name)"
            };
            _query.ValueChanged += (s, e) => ApplyFilter();
            _query.Submitted += (s, e) => ChooseFirst();
            _card.Controls.Add(_query);

            _list = new Panel
            {
                Location = new Point(Dpi.S(8), _query.Bottom + Dpi.S(8)),
                Width = cardWidth - Dpi.S(16),
                Height = ClientSize.Height - _query.Bottom - Dpi.S(16),
                BackColor = Color.White,
                AutoScroll = true
            };
            _card.Controls.Add(_list);

            _card.Height = ClientSize.Height;
            Controls.Add(_card);

            RenderList();

            // Arrow-key navigation from the query box.
            _query.PreviewKeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Up) e.IsInputKey = true;
            };

            ActiveControl = _query;
        }

        private void ApplyFilter()
        {
            string q = (_query.Value ?? string.Empty).Trim().ToLowerInvariant();

            _filtered = string.IsNullOrEmpty(q)
                ? _all
                : _all.Where(c =>
                    (c.Label ?? string.Empty).ToLowerInvariant().Contains(q)
                    || (c.Keywords ?? string.Empty).ToLowerInvariant().Contains(q)).ToList();

            RenderList();
        }

        private void RenderList()
        {
            _list.Controls.Clear();

            int y = 0;
            foreach (Command cmd in _filtered)
            {
                Command captured = cmd;

                FlatButton row = new FlatButton
                {
                    Text = cmd.Label,
                    Variant = ButtonVariant.Ghost,
                    AllowGrow = false,
                    Width = _list.Width - Dpi.S(16),
                    Height = Dpi.S(38),
                    Location = new Point(Dpi.S(4), y),
                    LeftAlign = true
                };
                row.Click += (s, e) => Choose(captured.Route);
                _list.Controls.Add(row);
                y = row.Bottom + Dpi.S(2);
            }

            if (_filtered.Count == 0)
            {
                Label none = Ui.Wrapped("No matching screens.", Theme.Small, Theme.Slate400,
                    _list.Width - Dpi.S(16));
                none.Location = new Point(Dpi.S(10), Dpi.S(8));
                _list.Controls.Add(none);
            }
        }

        private void ChooseFirst()
        {
            if (_filtered.Count > 0) Choose(_filtered[0].Route);
        }

        private void Choose(string route)
        {
            ChosenRoute = route;
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen pen = new Pen(Theme.Slate300))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
        }
    }
}
