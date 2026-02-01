using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace AuraUnlock;

/// <summary>
/// A control that represents a custom track bar with a dark-themed appearance.
/// Allows the user to select a value from a specified range using a draggable thumb.
/// </summary>
public sealed class DarkTrackBar : Control
{
    [DefaultValue(0), Category("Behavior")]
    public int Minimum { get; set; } = 0;

    [DefaultValue(100), Category("Behavior")]
    public int Maximum { get; set; } = 100;

    private int _value = 80;

    [DefaultValue(80), Category("Behavior")]
    public int Value
    {
        get => _value;
        set
        {
            var old = _value;
            _value = Math.Max(Minimum, Math.Min(Maximum, value));
            if (old != _value)
            {
                Invalidate();
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    [Category("Action")] public event EventHandler? ValueChanged;

    [DefaultValue(typeof(Color), "Red"), Category("Appearance")]
    public Color ThumbColor { get; set; } = Color.Red;

    [DefaultValue(typeof(Color), "Gray"), Category("Appearance")]
    public Color TrackColor { get; set; } = Color.Gray;

    public DarkTrackBar()
    {
        DoubleBuffered = true;
        Cursor = Cursors.Hand;
    }

    /// <summary>
    /// Handles the painting of the custom track bar, including the track and the thumb.
    /// This method ensures the control is rendered with a smooth, anti-aliased appearance.
    /// </summary>
    /// <param name="e">Provides data for the Paint event, including access to the graphics surface where the control is drawn.</param>
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;


        // 1. Рисуем линию (Track)
        const int trackHeight = 4;
        var trackY = (Height - trackHeight) / 2;
        using (var brush = new SolidBrush(TrackColor))
        {
            // Закругленная линия
            e.Graphics.FillRectangle(brush, 0, trackY, Width, trackHeight);
        }

        // 2. Рисуем ползунок (Thumb)
        var percent = (float)(Value - Minimum) / (Maximum - Minimum);
        const int thumbSize = 16;
        var thumbX = (int)(percent * (Width - thumbSize));
        var thumbY = (Height - thumbSize) / 2;

        using (var brush = new SolidBrush(ThumbColor))
        {
            e.Graphics.FillEllipse(brush, thumbX, thumbY, thumbSize, thumbSize);
        }
    }

    /// <summary>
    /// Handles the mouse button press event for the control.
    /// Updates the track bar value based on the position of the mouse pointer
    /// when the left mouse button is pressed.
    /// </summary>
    /// <param name="e">Provides data for mouse events, such as the button pressed and the location of the mouse pointer.</param>
    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left) UpdateValue(e.X);
    }

    /// <summary>
    /// Handles the mouse movement over the control, specifically allowing the user to drag the thumb
    /// to adjust the value. Updates the track bar value based on the mouse position when
    /// the left mouse button is held down.
    /// </summary>
    /// <param name="e">Provides data for the MouseMove event, including the position of the mouse cursor
    /// and the state of mouse buttons.</param>
    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left) UpdateValue(e.X);
    }

    /// <summary>
    /// Updates the value of the track bar based on the specified horizontal position.
    /// This method calculates the new value by mapping the position to the track bar's range
    /// while ensuring the value remains within the defined limits.
    /// </summary>
    /// <param name="x">The horizontal position, in pixels, relative to the left edge of the track bar.</param>
    private void UpdateValue(int x)
    {
        var thumbSize = 16;
        var percent = (float)(x - thumbSize / 2) / (Width - thumbSize);
        if (percent < 0) percent = 0;
        if (percent > 1) percent = 1;

        var newVal = Minimum + (int)(percent * (Maximum - Minimum));
        Value = newVal;
    }
}