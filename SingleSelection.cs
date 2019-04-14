namespace MultiCaret
{
    using System;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Editor;

    public struct SingleSelectionWithLocation
    {
        public SingleSelectionWithLocation(int caret, int start, int end, SingleSelection selection)
        {
            this.Caret = caret;
            this.Start = start;
            this.End = end;
            this.Selection = selection;
            this.IsZeroLength = this.Start == this.End;
        }

        public int Caret { get; }

        public int Start { get; }

        public int End { get; }

        public bool IsZeroLength { get; }

        public SingleSelection Selection { get; set; }

        public bool IsBefore(SingleSelectionWithLocation other)
            => this.IsZeroLength || other.IsZeroLength ? this.End < other.Start : this.End <= other.Start;

        public bool IsAfter(SingleSelectionWithLocation other)
            => this.IsZeroLength || other.IsZeroLength ? this.Start > other.End : this.Start >= other.End;

        public SingleSelection ExpandTo(int start, int end)
            => SingleSelection.Create(Math.Min(this.Start, start), Math.Max(this.End, end), this.Selection);
    }

    public struct SingleSelection
    {
        private readonly ITrackingSpan selection;
        private readonly bool isReversed;

        private SingleSelection(ITrackingSpan selection, bool isReversed)
        {
            this.selection = selection;
            this.isReversed = isReversed;
        }

        public static SingleSelection? Create(SnapshotSpan selection, bool isReversed)
        {
            var span = selection.Snapshot
                .CreateTrackingSpan(selection, SpanTrackingMode.EdgeInclusive);

            return span == null ? (SingleSelection?)null : new SingleSelection(span, isReversed);
        }

        public static SingleSelection? Create(ITextSnapshot snapshot, int position, bool isReversed)
        {
            var span = snapshot
                .CreateTrackingSpan(position, 0, SpanTrackingMode.EdgeInclusive);

            return span == null ? (SingleSelection?)null : new SingleSelection(span, isReversed);
        }

        public static SingleSelection Create(int start, int end, SingleSelection directionProvider)
        {
            var span = directionProvider.selection.TextBuffer.CurrentSnapshot
                .CreateTrackingSpan(start, end - start, SpanTrackingMode.EdgeInclusive);

            return new SingleSelection(span, directionProvider.isReversed);
        }

        public SingleSelection WithDirection(SingleSelection directionProvider)
            => new SingleSelection(this.selection, directionProvider.isReversed);

        public void SetSelection(IWpfTextView textView)
        {
            var span = this.GetSelectionSpan();
            textView.Caret.MoveTo(this.GetCaretPoint(span));
            textView.Selection.Select(span, this.isReversed);
        }

        public SingleSelectionWithLocation GetDetails()
        {
            var span = this.GetSelectionSpan();
            return new SingleSelectionWithLocation(
                this.isReversed ? span.Start.Position : span.End.Position,
                span.Start.Position,
                span.End.Position,
                this);
        }

        public SnapshotSpan GetSelectionSpan()
        {
            return this.selection.GetSpan(this.selection.TextBuffer.CurrentSnapshot);
        }

        public SnapshotPoint GetCaretPoint(SnapshotSpan span)
        {
            return this.isReversed ? span.Start : span.End;
        }
    }
}
