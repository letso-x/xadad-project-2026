using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace MzuApplication.Controls
{
    /// <summary>
    /// Puts a <see cref="Graphics"/> into design space for the duration of a paint.
    ///
    /// Owner-drawn controls are authored against a 96 DPI grid, but their outer size is
    /// scaled to the display. Applying the scale as a transform lets the paint code keep
    /// using its original literal offsets while still rendering at full resolution, which
    /// is what keeps text and icons sharp rather than stretched.
    ///
    /// Inside the scope use <see cref="W"/> and <see cref="H"/> instead of the control's
    /// Width and Height, because those are in device pixels.
    /// </summary>
    internal struct PaintScope : IDisposable
    {
        private readonly Graphics _graphics;
        private readonly GraphicsState _state;

        public PaintScope(Graphics graphics, int deviceWidth, int deviceHeight)
        {
            _graphics = graphics;

            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            _state = graphics.Save();

            float scale = Dpi.Scale;
            graphics.ScaleTransform(scale, scale);

            // Round down so drawing never spills past the control's real edge.
            W = (int)Math.Floor(deviceWidth / scale);
            H = (int)Math.Floor(deviceHeight / scale);
        }

        /// <summary>Control width in design units.</summary>
        public int W { get; private set; }

        /// <summary>Control height in design units.</summary>
        public int H { get; private set; }

        public void Dispose()
        {
            if (_graphics != null && _state != null)
            {
                _graphics.Restore(_state);
            }
        }
    }
}
