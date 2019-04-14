namespace MultiCaret
{
    using System;
    using System.Collections.Generic;
    using System.Windows.Controls;
    using System.Windows.Media;
    using System.Windows.Threading;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Editor;

    internal sealed class MultiCaretTextAdornment
    {
        private readonly IWpfTextView view;
        private readonly IAdornmentLayer layer;
        private readonly CaretBlinkHelper caretBlinkHelper;
        private readonly ICoreSettings settings;

        private SelectionList currentSelections;

        public MultiCaretTextAdornment(IWpfTextView view, ICoreSettings settings)
        {
            this.view = view ?? throw new ArgumentNullException(nameof(view));
            this.settings = settings;
            this.caretBlinkHelper = new CaretBlinkHelper();
            this.layer = view.GetAdornmentLayer(Constants.AdornmentLayerName);

            this.view.LayoutChanged += this.UpdateLayout;
            this.settings.SettingsChanged += this.UpdateLayout;
        }

        public void SetSelections(SelectionList selections)
        {
            this.currentSelections = selections;
            if (this.currentSelections == null || this.currentSelections.Count == 0)
                this.ClearLayout();
            else
                this.UpdateLayout();
        }

        private void UpdateLayout(object sender, EventArgs e)
        {
            if (this.currentSelections != null && this.currentSelections.Count > 0)
                this.UpdateLayout();
        }

        private void ClearLayout()
        {
            this.layer.RemoveAllAdornments();
            this.caretBlinkHelper.SetCarets(null);
        }

        private void UpdateLayout()
        {
            this.layer.RemoveAllAdornments();
            var carets = new List<Image>(this.currentSelections.Count);
            foreach (var selection in this.currentSelections)
            {
                var span = selection.GetSelectionSpan();
                this.DrawSelection(span);
                var caret = this.DrawCaret(selection.GetCaretPoint(span));
                if (caret != null)
                    carets.Add(caret);
            }

            this.caretBlinkHelper.SetCarets(carets);
        }

        private void DrawSelection(SnapshotSpan span)
        {
            var geometry = this.view.TextViewLines?.GetTextMarkerGeometry(
                span,
                false,
                new System.Windows.Thickness(0, 0.5, 0, 1.5));

            if (geometry != null)
                this.AddAdornment(geometry, this.settings.SelectionBrush, 0.4, span);
        }

        private Image DrawCaret(SnapshotPoint caret)
        {
            // The GetCharacterBounds call will throw if it is outside the viewport
            // - we do not need to draw the caret in that case.
            if (caret.Position < this.view.TextViewLines.FirstVisibleLine.Start.Position
                || caret.Position > this.view.TextViewLines.LastVisibleLine.End.Position)
            {
                return null;
            }

            // Create a geometry to draw the caret
            var bounds = this.view.TextViewLines.GetCharacterBounds(caret);
            var geometry = new RectangleGeometry(
                new System.Windows.Rect(
                    bounds.Left,
                    bounds.TextTop,
                    this.view.Caret.Width,
                    bounds.TextHeight));

            return this.AddAdornment(
                geometry,
                this.settings.CaretBrush,
                1,
                new SnapshotSpan(caret, 0));
        }

        private Image AddAdornment(
            Geometry geometry,
            Brush brush,
            double initialOpacity,
            SnapshotSpan span)
        {
            var drawing = new GeometryDrawing(brush, null, geometry);
            drawing.Freeze();

            var drawingImage = new DrawingImage(drawing);
            drawingImage.Freeze();

            var image = new Image
            {
                Opacity = initialOpacity,
                Source = drawingImage,
            };

            // Align the image with the top of the bounds of the text geometry
            Canvas.SetLeft(image, geometry.Bounds.Left);
            Canvas.SetTop(image, geometry.Bounds.Top);

            this.layer.AddAdornment(
                AdornmentPositioningBehavior.TextRelative,
                span,
                null,
                image,
                null);

            return image;
        }

        private class CaretBlinkHelper
        {
            private readonly DispatcherTimer blinkTimer;
            private IList<Image> images;

            public CaretBlinkHelper()
            {
                this.blinkTimer = new DispatcherTimer(
                    TimeSpan.FromMilliseconds(GetCaretBlinkTime()),
                    DispatcherPriority.Normal,
                    this.Blink,
                    Dispatcher.CurrentDispatcher);

                this.blinkTimer.Stop();
            }

            public void SetCarets(IList<Image> carets)
            {
                this.images = carets;
                if (this.blinkTimer.IsEnabled)
                {
                    if (carets == null || carets.Count == 0)
                        this.blinkTimer.Stop();
                }
                else if (carets?.Count > 0)
                {
                    this.blinkTimer.Start();
                }
            }

            private void Blink(object sender, EventArgs e)
            {
                var imagesLocal = this.images;
                if (imagesLocal == null)
                    return;

                foreach (var image in imagesLocal)
                    image.Opacity = image.Opacity == 0 ? 1 : 0;
            }

            [System.Runtime.InteropServices.DllImport("user32.dll")]
            private static extern uint GetCaretBlinkTime();
        }
    }
}
