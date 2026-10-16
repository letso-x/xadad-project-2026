using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using MzuApplication.Controls;

namespace MzuApplication.Forms
{
    /// <summary>
    /// A modal panel with a header, a stacked field body and a footer button row.
    /// Fields are added in order and the dialog sizes itself to fit.
    /// </summary>
    internal sealed class Dialog : Form
    {
        private readonly Panel _body;
        private readonly Panel _footer;
        private readonly List<FlatButton> _actions = new List<FlatButton>();
        private int _cursorY;
        private readonly int _fieldWidth;

        public Dialog(string title, int width)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.White;
            Font = Theme.Body;
            Width = width;
            DoubleBuffered = true;
            KeyPreview = true;

            _fieldWidth = width - 48;

            // ---- Header ------------------------------------------------------
            Panel header = new Panel
            {
                Dock = DockStyle.Top,
                Height = Dpi.S(56),
                BackColor = Color.White
            };

            Label heading = new Label
            {
                AutoSize = false,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Theme.Navy950,
                Location = new Point(Dpi.S(24), Dpi.S(19)),
                Width = width - 80,
                Height = Dpi.S(22),
                Text = title
            };
            header.Controls.Add(heading);

            IconButton close = new IconButton
            {
                Glyph = Glyph.Cross,
                Location = new Point(width - 44, 15),
                Size = new Size(Dpi.S(26), Dpi.S(26))
            };
            close.Click += (s, e) => { DialogResult = DialogResult.Cancel; };
            header.Controls.Add(close);

            Panel headerRule = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = Dpi.S(1),
                BackColor = Theme.Slate150
            };
            header.Controls.Add(headerRule);

            Controls.Add(header);

            // ---- Body --------------------------------------------------------
            _body = new Panel
            {
                Location = new Point(0, header.Height),
                Width = width,
                BackColor = Color.White,
                AutoScroll = true
            };
            Controls.Add(_body);

            // ---- Footer ------------------------------------------------------
            _footer = new Panel
            {
                Height = Dpi.S(62),
                Width = width,
                BackColor = Color.White
            };

            Panel footerRule = new Panel
            {
                Dock = DockStyle.Top,
                Height = Dpi.S(1),
                BackColor = Theme.Slate150
            };
            _footer.Controls.Add(footerRule);

            FlatButton cancel = new FlatButton
            {
                Text = "Close",
                Width = Dpi.S(84),
                Height = Dpi.S(34)
            };
            cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; };
            _footer.Controls.Add(cancel);
            _actions.Add(cancel);

            Controls.Add(_footer);

            _cursorY = 20;

            // Escape always dismisses.
            KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape) DialogResult = DialogResult.Cancel;
            };
        }

        /// <summary>Adds a labelled single-line text field and returns it.</summary>
        public TextInput AddTextField(string label, string value, string placeholder = null)
        {
            AddLabel(label);

            TextInput input = new TextInput
            {
                Location = new Point(24, _cursorY),
                Width = _fieldWidth,
                Height = Dpi.S(34),
                Value = value ?? string.Empty
            };
            if (!string.IsNullOrEmpty(placeholder)) input.Placeholder = placeholder;

            _body.Controls.Add(input);
            _cursorY = input.Bottom + 14;
            return input;
        }

        /// <summary>Adds a labelled multi-line text area and returns it.</summary>
        public TextInput AddTextArea(string label, string value, int height = 78)
        {
            AddLabel(label);

            TextInput input = new TextInput
            {
                Location = new Point(24, _cursorY),
                Width = _fieldWidth,
                Height = height,
                Multiline = true,
                Value = value ?? string.Empty
            };

            _body.Controls.Add(input);
            _cursorY = input.Bottom + 14;
            return input;
        }

        /// <summary>Adds a labelled dropdown and returns it.</summary>
        public ComboBox AddDropdown(string label, IEnumerable<string> options, string selected)
        {
            AddLabel(label);

            ComboBox combo = Ui.Dropdown(_fieldWidth);
            combo.Location = new Point(24, _cursorY);
            foreach (string option in options) combo.Items.Add(option);
            if (selected != null && combo.Items.Contains(selected)) combo.SelectedItem = selected;
            else if (combo.Items.Count > 0) combo.SelectedIndex = 0;

            _body.Controls.Add(combo);
            _cursorY = combo.Bottom + 14;
            return combo;
        }

        /// <summary>Adds a labelled date field (ISO text) and returns it.</summary>
        public DateTimePicker AddDateField(string label, DateTime? value)
        {
            AddLabel(label);

            DateTimePicker picker = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = "yyyy-MM-dd",
                ShowCheckBox = true,
                Font = Theme.Body,
                Location = new Point(24, _cursorY),
                Width = _fieldWidth
            };

            if (value.HasValue)
            {
                picker.Value = value.Value;
                picker.Checked = true;
            }
            else
            {
                picker.Checked = false;
            }

            _body.Controls.Add(picker);
            _cursorY = picker.Bottom + 14;
            return picker;
        }

        public CheckBox AddCheckField(string label, bool checkedState)
        {
            CheckBox box = Ui.Check(label);
            box.Checked = checkedState;
            box.Location = new Point(24, _cursorY + 2);

            _body.Controls.Add(box);
            _cursorY = box.Bottom + 14;
            return box;
        }

        /// <summary>Adds a read-only label / value pair.</summary>
        public void AddReadOnlyField(string label, string value)
        {
            AddLabel(label);

            Label content = Ui.Wrapped(value ?? "—", Theme.Body, Theme.Slate900, _fieldWidth);
            content.Location = new Point(24, _cursorY);
            _body.Controls.Add(content);
            _cursorY = content.Bottom + 14;
        }

        /// <summary>Adds an explanatory note in muted small text.</summary>
        public void AddNote(string text)
        {
            Label note = Ui.Wrapped(text, Theme.Tiny, Theme.Slate500, _fieldWidth);
            note.Location = new Point(24, _cursorY);
            _body.Controls.Add(note);
            _cursorY = note.Bottom + 12;
        }

        private void AddLabel(string text)
        {
            Label label = Ui.FieldLabel(text);
            label.Location = new Point(24, _cursorY);
            _body.Controls.Add(label);
            _cursorY = label.Bottom + 6;
        }

        /// <summary>Adds a primary footer action. The callback runs on click.</summary>
        public void AddPrimaryAction(string text, Action onClick)
        {
            AddAction(text, ButtonVariant.Primary, onClick);
        }

        /// <summary>Adds a destructive (red) footer action.</summary>
        public void AddDangerAction(string text, Action onClick)
        {
            AddAction(text, ButtonVariant.Danger, onClick);
        }

        private void AddAction(string text, ButtonVariant variant, Action onClick)
        {
            FlatButton button = new FlatButton
            {
                Text = text,
                Variant = variant,
                Width = Math.Max(Dpi.S(90), Dpi.S(text.Length * 8 + 28)),
                Height = Dpi.S(34)
            };
            button.Click += (s, e) => onClick();

            _footer.Controls.Add(button);
            _actions.Add(button);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            // Size to content, then lay the footer buttons out right to left.
            int bodyHeight = Math.Min(_cursorY + 8, 520);
            _body.Height = bodyHeight;
            _footer.Top = _body.Bottom;
            ClientSize = new Size(Width, _footer.Bottom);

            int x = Width - 24;
            for (int i = _actions.Count - 1; i >= 0; i--)
            {
                FlatButton button = _actions[i];
                x -= button.Width;
                button.Location = new Point(x, 16);
                x -= 8;
            }

            CenterToParent();
        }

        /// <summary>Draws a border and soft shadow so the borderless dialog reads as a card.</summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (Pen pen = new Pen(Theme.Slate200))
            {
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            }
        }
    }
}
