namespace MultiCaret
{
    using System;
    using System.Threading;
    using System.Windows;
    using System.Windows.Input;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Editor;
    using Microsoft.VisualStudio.Text.Formatting;

    internal class MultiCaretMouseProcessor : IMouseProcessor
    {
        private readonly IWpfTextView textView;
        private MultiCaretCommandFilter commandFilter;
        private ICoreSettings settings;
        private ViewScroller scroller;
        private SnapshotPoint? selectionStart;

        public MultiCaretMouseProcessor(IWpfTextView view)
        {
            this.textView = view;
            this.EnsureInitialised();
        }

        public void PreprocessMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            if (!this.EnsureInitialised())
                return;

            if (this.settings.IsMouseAddSelectionKeyDown())
            {
                this.commandFilter.HandleClickAdd(Utils.GetCurrentSelections(this.textView));

                this.textView.Selection.Clear();
                var start = this.GetMouseTextPosition(e).Point;
                this.selectionStart = start;
                this.SetSelection(start, start);
                this.textView.VisualElement.CaptureMouse();

                e.Handled = true;
            }
            else
            {
                this.commandFilter.HandleClickClear();
            }
        }

        public void PostprocessMouseMove(MouseEventArgs e)
        {
            if (!this.selectionStart.HasValue)
                return;

            if (!this.EnsureInitialised())
                return;

            var position = this.GetMouseTextPosition(e);

            // Scroll viewport if the mouse is outside like VS
            if (position.Vertical == VerticalBounds.Above)
                this.scroller.StartScrollUp();
            else if (position.Vertical == VerticalBounds.Below)
                this.scroller.StartScrollDown();
            else
                this.scroller.StopScroll();

            // Update the selection
            this.SetSelection(this.selectionStart.Value, position.Point);
            this.ScrollHorizontallyToPoint(position);
            e.Handled = true;
        }

        public void PostprocessMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if (!this.selectionStart.HasValue)
                return;

            if (!this.EnsureInitialised())
                return;

            this.scroller.StopScroll();
            this.textView.VisualElement.ReleaseMouseCapture();
            this.selectionStart = null;
      
            this.commandFilter.HandleClickComplete(Utils.GetCurrentSelections(this.textView));
            
            e.Handled = true;
        }

        private bool EnsureInitialised()
        {
            if (this.commandFilter != null)
                return true;

            if (!this.textView.Properties
                .TryGetProperty(typeof(MultiCaretCommandFilter), out this.commandFilter))
            {
                return false;
            }

            this.settings = this.textView.Properties.GetProperty<ICoreSettings>(typeof(ICoreSettings));
            this.scroller = new ViewScroller(this.textView, this.ProcessViewportScrolled);

            return true;
        }

        private void ProcessViewportScrolled()
        {
            if (!this.selectionStart.HasValue)
                return;

            var position = this.GetMouseTextPosition(null);
            this.SetSelection(this.selectionStart.Value, position.Point);
            this.ScrollHorizontallyToPoint(position);
        }

        private void ScrollHorizontallyToPoint(MouseTextPosition position)
        {
            if (position.Horizontal == HorizontalBounds.Inside)
                return;

            // If outside to the left then reset the viewport all the way to the start first
            if (position.Horizontal == HorizontalBounds.Left)
                this.textView.ViewportLeft = 0;

            // Ensure the position is visible, either if we were off the right or if
            // we were off to the left and went too far by resetting the viewport left to 0
            this.textView.ViewScroller.EnsureSpanVisible(
                this.textView.GetTextElementSpan(position.Point));
        }

        private MouseTextPosition GetMouseTextPosition(MouseEventArgs args)
        {
            var mousePoint = args?.GetPosition(this.textView.VisualElement) ??
                Mouse.GetPosition(this.textView.VisualElement);

            var result = new MouseTextPosition {Horizontal = HorizontalBounds.Inside};

            // Find the line we are interested in based on mouse position
            ITextViewLine line;
            if (mousePoint.Y <= 0)
            {
                line = this.textView.TextViewLines.FirstVisibleLine;
                result.Vertical = VerticalBounds.Above;
            }
            else if (mousePoint.Y >= this.textView.ViewportBottom - this.textView.ViewportTop)
            {
                line = this.textView.TextViewLines.LastVisibleLine;
                result.Vertical = VerticalBounds.Below;
            }
            else
            {
                // If GetTextViewLineContainingYCoordinate returns null in this situation
                // then since we are within the viewport we know we must have scrolled all
                // the way to the end of the document and the mosue must be over the viewport
                // space showing after the end of the document hence in that case we select the
                // last line.
                line = this.textView.TextViewLines.GetTextViewLineContainingYCoordinate(
                        mousePoint.Y + this.textView.ViewportTop)
                    ?? this.textView.TextViewLines.LastVisibleLine;

                result.Vertical = VerticalBounds.Inside;
            }

            var x = mousePoint.X + this.textView.ViewportLeft;

            // If the mouse is outside the viewport to right or left then
            // select the first or last point on the line
            if (x < this.textView.ViewportLeft)
            {
                result.Point = line.Start;
                if (line.TextLeft < this.textView.ViewportLeft)
                    result.Horizontal = HorizontalBounds.Left;
                else if (line.TextLeft > this.textView.ViewportRight)
                    result.Horizontal = HorizontalBounds.Right;

                return result;
            }

            if (x > this.textView.ViewportRight)
            {
                result.Point = line.End;
                if (line.TextRight > this.textView.ViewportRight)
                    result.Horizontal = HorizontalBounds.Right;
                else if (line.TextRight < this.textView.ViewportLeft)
                    result.Horizontal = HorizontalBounds.Left;

                return result;
            }

            // Find and adjust the desired point based on x position
            var point = line.GetBufferPositionFromXCoordinate(x);
            if (point.HasValue)
            {
                // The point will always be for the charecter that the mouse is over
                // however it feels far more natural (and VS selection does this) to
                // select a charecter based on the closest start point to the mouse.
                // Here we select the next charecter if its start position is closer.
                var bounds = line.GetCharacterBounds(point.Value);
                if (x > (bounds.Left + (bounds.Width / 2)))
                {
                    result.Point = line.GetBufferPositionFromXCoordinate(x + bounds.Width) ?? point.Value;
                    return result;
                }

                result.Point = point.Value;
                return result;
            }

            // If we get here the line does not go all the way to the viewport right
            // side and we are past the end of the line but inside the viewport
            result.Point = line.End;
            if (line.TextRight < this.textView.ViewportLeft)
                result.Horizontal = HorizontalBounds.Left;

            return result;
        }

        private void SetSelection(SnapshotPoint start, SnapshotPoint end)
        {
            this.textView.Caret.MoveTo(end);
            this.textView.Selection.Select(
                new VirtualSnapshotPoint(start),
                new VirtualSnapshotPoint(end));
        }

        private enum VerticalBounds
        {
            Above,
            Inside,
            Below
        }

        private enum HorizontalBounds
        {
            Left,
            Inside,
            Right
        }

        private struct MouseTextPosition
        {
            public SnapshotPoint Point { get; set; }

            public VerticalBounds Vertical { get; set; }

            public HorizontalBounds Horizontal { get; set; }
        }

        private class ViewScroller
        {
            private enum VerticalScroll
            {
                Up,
                None,
                Down
            }

            private static readonly TimeSpan VerticalStepTime = TimeSpan.FromSeconds(0.25);
            private readonly IWpfTextView textView;
            private readonly Action processViewportScrolled;
            private readonly Timer verticalTimer;
            private VerticalScroll verticalScroll = VerticalScroll.None;

            public ViewScroller(IWpfTextView view, Action processViewportScrolled)
            {
                this.textView = view;
                this.processViewportScrolled = processViewportScrolled;
                this.verticalTimer = new Timer(this.InvokeScrollVertically);
            }

            public void StartScrollUp()
            {
                if (this.verticalScroll == VerticalScroll.Up || this.AtTop())
                    return;

                this.verticalScroll = VerticalScroll.Up;
                this.verticalTimer.Change(VerticalStepTime, Timeout.InfiniteTimeSpan);
            }

            public void StartScrollDown()
            {
                if (this.verticalScroll == VerticalScroll.Down || this.AtBottom())
                    return;

                this.verticalScroll = VerticalScroll.Down;
                this.verticalTimer.Change(VerticalStepTime, Timeout.InfiniteTimeSpan);
            }

            public void StopScroll()
            {
                if (this.verticalScroll == VerticalScroll.None)
                    return;

                this.verticalScroll = VerticalScroll.None;
                this.verticalTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            }

            private void InvokeScrollVertically(object state)
            {
                if (this.verticalScroll == VerticalScroll.None)
                    return;

                this.textView.VisualElement.Dispatcher.BeginInvoke(new Action(this.ScrollVertically));
            }

            private void ScrollVertically()
            {
                switch (this.verticalScroll)
                {
                    case VerticalScroll.None:
                        return;
                    case VerticalScroll.Up:
                        this.textView.ViewScroller.ScrollViewportVerticallyByLine(ScrollDirection.Up);
                        this.processViewportScrolled();
                        if (this.AtTop())
                        {
                            this.verticalScroll = VerticalScroll.None;
                            return;
                        }

                        break;
                    case VerticalScroll.Down:
                        this.textView.ViewScroller.ScrollViewportVerticallyByLine(ScrollDirection.Down);
                        this.processViewportScrolled();
                        if (this.AtBottom())
                        {
                            this.verticalScroll = VerticalScroll.None;
                            return;
                        }

                        break;
                }

                this.verticalTimer.Change(VerticalStepTime, Timeout.InfiniteTimeSpan);
            }

            private bool AtTop() { return this.textView.ViewportTop <= 0; }

            private bool AtBottom()
            {
                return this.textView.TextViewLines[this.textView.TextViewLines.Count - 1]
                    .VisibilityState == VisibilityState.FullyVisible;
            }
        }

        #region Unused

        public void PostprocessDragEnter(DragEventArgs e) { }

        public void PostprocessDragLeave(DragEventArgs e) { }

        public void PostprocessDragOver(DragEventArgs e) { }

        public void PostprocessDrop(DragEventArgs e) { }

        public void PostprocessGiveFeedback(GiveFeedbackEventArgs e) { }

        public void PostprocessMouseDown(MouseButtonEventArgs e) { }

        public void PostprocessMouseEnter(MouseEventArgs e) { }

        public void PostprocessMouseLeave(MouseEventArgs e) { }

        public void PostprocessMouseLeftButtonDown(MouseButtonEventArgs e) { }

        public void PostprocessMouseRightButtonDown(MouseButtonEventArgs e) { }

        public void PostprocessMouseRightButtonUp(MouseButtonEventArgs e) { }

        public void PostprocessMouseUp(MouseButtonEventArgs e) { }

        public void PostprocessMouseWheel(MouseWheelEventArgs e) { }

        public void PostprocessQueryContinueDrag(QueryContinueDragEventArgs e) { }

        public void PreprocessDragEnter(DragEventArgs e) { }

        public void PreprocessDragLeave(DragEventArgs e) { }

        public void PreprocessDragOver(DragEventArgs e) { }

        public void PreprocessDrop(DragEventArgs e) { }

        public void PreprocessGiveFeedback(GiveFeedbackEventArgs e) { }

        public void PreprocessMouseDown(MouseButtonEventArgs e) { }

        public void PreprocessMouseEnter(MouseEventArgs e) { }

        public void PreprocessMouseLeave(MouseEventArgs e) { }

        public void PreprocessMouseLeftButtonUp(MouseButtonEventArgs e) { }

        public void PreprocessMouseMove(MouseEventArgs e) { }

        public void PreprocessMouseRightButtonDown(MouseButtonEventArgs e) { }

        public void PreprocessMouseRightButtonUp(MouseButtonEventArgs e) { }

        public void PreprocessMouseUp(MouseButtonEventArgs e) { }

        public void PreprocessMouseWheel(MouseWheelEventArgs e) { }

        public void PreprocessQueryContinueDrag(QueryContinueDragEventArgs e) { }

        #endregion
    }
}
