namespace MultiCaret
{
    using System.Collections.Generic;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Editor;

    internal static class Utils
    {
        public static IEnumerable<SingleSelection> GetCurrentSelections(IWpfTextView textView)
        {
            return textView.Selection.SelectedSpans.ToSingleSelections(textView.Selection.IsReversed);
        }

        public static IEnumerable<SingleSelection> ToSingleSelections(
            this IEnumerable<SnapshotSpan> spans,
            bool areReversed)
        {
            foreach (var span in spans)
            {
                var selection = SingleSelection.Create(span, areReversed);
                if (selection.HasValue)
                    yield return selection.Value;
            }
        }
    }
}
