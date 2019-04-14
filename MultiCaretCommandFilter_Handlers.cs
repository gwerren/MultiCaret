namespace MultiCaret
{
    using System;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Windows;
    using Microsoft.VisualStudio;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Operations;

    internal partial class MultiCaretCommandFilter
    {
        private static int SplitSelectionIntoLines(
            MultiCaretCommandFilter commandFilter,
            ref Guid pguidCmdGroup,
            uint nCmdID,
            uint nCmdexecopt,
            IntPtr pvaIn,
            IntPtr pvaOut)
        {
            commandFilter.SelectionSwapExec(
                (currentSelections, targetSelections) =>
                {
                    foreach (var selection in currentSelections)
                    {
                        var span = selection.GetSelectionSpan();
                        var searchingStart = true;
                        SingleSelection? previous = null;
                        foreach (var line in commandFilter.textView.TextBuffer.CurrentSnapshot.Lines)
                        {
                            var intersection = line.Extent.Intersection(span);
                            if (intersection.HasValue)
                            {
                                if (previous.HasValue)
                                {
                                    targetSelections.InsertExpectEnd(previous.Value);
                                    previous = null;
                                }

                                searchingStart = false;
                                var newSelection = SingleSelection.Create(intersection.Value, false);
                                if (newSelection.HasValue)
                                {
                                    if (intersection.Value.Length != 0)
                                        targetSelections.InsertExpectEnd(newSelection.Value);
                                    else
                                        previous = newSelection;
                                }
                            }
                            else if (!searchingStart)
                            {
                                break;
                            }
                        }
                    }
                });

            return VSConstants.S_OK;
        }

        private static int HandleTypeChar(
            MultiCaretCommandFilter commandFilter,
            ref Guid pguidCmdGroup,
            uint nCmdID,
            uint nCmdexecopt,
            IntPtr pvaIn,
            IntPtr pvaOut)
        {
            var typed = ((char)Marshal.GetObjectForNativeVariant<ushort>(pvaIn)).ToString();
            commandFilter.SelectionSwapExec(
                (tempSelections, targetSelections) =>
                {
                    using (var edit = commandFilter.CreateEdit())
                    {
                        foreach (var selection in tempSelections)
                            edit.Replace(selection.GetSelectionSpan().Span, typed);

                        edit.Apply();
                    }

                    // Whatever we typed the tracking spans should now end at the
                    // end of our typing which is where our carets should be
                    foreach (var selection in tempSelections)
                    {
                        var newSelection = SingleSelection.Create(
                            commandFilter.textView.TextSnapshot,
                            selection.GetSelectionSpan().Span.End,
                            false);

                        if (newSelection.HasValue)
                            targetSelections.InsertExpectEnd(newSelection.Value);
                    }
                });

            return VSConstants.S_OK;
        }

        private static int HandleDelete(
            MultiCaretCommandFilter commandFilter,
            ref Guid pguidCmdGroup,
            uint nCmdID,
            uint nCmdexecopt,
            IntPtr pvaIn,
            IntPtr pvaOut)
        {
            commandFilter.SelectionSwapExec(
                (tempSelections, targetSelections) =>
                {
                    using (var edit = commandFilter.CreateEdit())
                    {
                        foreach (var selection in tempSelections)
                        {
                            var span = selection.GetSelectionSpan();
                            if (span.Length != 0)
                            {
                                edit.Delete(span.Span);
                            }
                            else if (span.Start.Position < span.Snapshot.Length)
                            {
                                // If this span is zero length then we should delete
                                // the previous char - if it is an end of line char
                                // then we should make sure we delete the full set
                                // e.g. \r\n for windows line endings
                                var position = span.Start.Position;
                                var length = 1;
                                if (IsEndOfLineChar(span.Snapshot, position))
                                {
                                    while (position + length < span.Snapshot.Length
                                        && IsEndOfLineChar(span.Snapshot, position + length))
                                    {
                                        ++length;
                                    }
                                }

                                edit.Delete(position, length);
                            }
                        }

                        edit.Apply();
                    }

                    // The tracking spans should now be zero length
                    // We add them back in here to ensure merging of overlaps
                    foreach (var selection in tempSelections)
                        targetSelections.InsertExpectEnd(selection);
                });

            return VSConstants.S_OK;
        }

        private static int HandleBackspace(
            MultiCaretCommandFilter commandFilter,
            ref Guid pguidCmdGroup,
            uint nCmdID,
            uint nCmdexecopt,
            IntPtr pvaIn,
            IntPtr pvaOut)
        {
            commandFilter.SelectionSwapExec(
                (tempSelections, targetSelections) =>
                {
                    using (var edit = commandFilter.CreateEdit())
                    {
                        foreach (var selection in tempSelections)
                        {
                            var span = selection.GetSelectionSpan();
                            if (span.Length != 0)
                            {
                                edit.Delete(span.Span);
                            }
                            else if (span.Start.Position > 0)
                            {
                                // If this span is zero length then we should delete
                                // the previous char - if it is an end of line char
                                // then we should make sure we delete the full set
                                // e.g. \r\n for windows line endings
                                var position = span.Start.Position - 1;
                                var length = 1;
                                if (IsEndOfLineChar(span.Snapshot, position))
                                {
                                    while (position > 0 && IsEndOfLineChar(span.Snapshot, position - 1))
                                    {
                                        --position;
                                        ++length;
                                    }
                                }

                                if (position >= 0)
                                    edit.Delete(position, length);
                            }
                        }

                        edit.Apply();
                    }

                    // The tracking spans should now be zero length
                    // We add them back in here to ensure merging of overlaps
                    foreach (var selection in tempSelections)
                        targetSelections.InsertExpectEnd(selection);
                });

            return VSConstants.S_OK;
        }

        private static bool IsEndOfLineChar(ITextSnapshot snapshot, int location)
        {
            var value = snapshot.GetText(location, 1);
            return value == "\r" || value == "\n";
        }

        private static int HandleCancel(
            MultiCaretCommandFilter commandFilter,
            ref Guid pguidCmdGroup,
            uint nCmdID,
            uint nCmdexecopt,
            IntPtr pvaIn,
            IntPtr pvaOut)
        {
            // Clear the selections
            commandFilter.selections.Clear();
            commandFilter.selectionDisplay.SetSelections(commandFilter.selections);

            return VSConstants.S_OK;
        }

        private static int HandleUndoRedo(
            MultiCaretCommandFilter commandFilter,
            ref Guid pguidCmdGroup,
            uint nCmdID,
            uint nCmdexecopt,
            IntPtr pvaIn,
            IntPtr pvaOut)
        {
            /* We only want to call undo once however if we simply use PassThrough
               then we end up loosing the selection that VS is handling rather than us */

            var result = 0;
            var pguidCmdGroupLocal = pguidCmdGroup;
            commandFilter.SelectionSwapExec(
                (tempSelections, targetSelections) =>
                {
                    // Execute undo
                    result = commandFilter.nextTarget.Exec(
                        ref pguidCmdGroupLocal,
                        nCmdID,
                        nCmdexecopt,
                        pvaIn,
                        pvaOut);

                    // Add the selection into the list again - we do this so that we have
                    // clean selection details in the list and can handle any merging
                    targetSelections.InsertExpectEnd(tempSelections);
                });

            pguidCmdGroup = pguidCmdGroupLocal;
            return result;
        }

        private static int HandleCut(
            MultiCaretCommandFilter commandFilter,
            ref Guid pguidCmdGroup,
            uint nCmdID,
            uint nCmdexecopt,
            IntPtr pvaIn,
            IntPtr pvaOut)
        {
            commandFilter.SelectionSwapExec(
                (tempSelections, targetSelections) =>
                {
                    using (var edit = commandFilter.CreateEdit())
                    {
                        var copyText = new StringBuilder();
                        foreach (var selection in tempSelections)
                        {
                            var span = selection.GetSelectionSpan();
                            if (span.Length != 0)
                            {
                                copyText.AppendLine(span.GetText());
                                edit.Delete(span.Span);
                            }
                        }

                        Clipboard.SetText(copyText.ToString());
                        edit.Apply();
                    }

                    // The tracking spans should now be zero length
                    // We add them back in here to ensure merging of overlaps
                    foreach (var selection in tempSelections)
                        targetSelections.InsertExpectEnd(selection);
                });

            return VSConstants.S_OK;
        }

        private static int HandleCopy(
            MultiCaretCommandFilter commandFilter,
            ref Guid pguidCmdGroup,
            uint nCmdID,
            uint nCmdexecopt,
            IntPtr pvaIn,
            IntPtr pvaOut)
        {
            var copyText = new StringBuilder();
            foreach (var selection in commandFilter.selections
                .Concat(Utils.GetCurrentSelections(commandFilter.textView)))
            {
                var span = selection.GetSelectionSpan();
                if (span.Length != 0)
                    copyText.AppendLine(span.GetText());
            }

            Clipboard.SetText(copyText.ToString());
            return VSConstants.S_OK;
        }

        private static int HandlePaste(
            MultiCaretCommandFilter commandFilter,
            ref Guid pguidCmdGroup,
            uint nCmdID,
            uint nCmdexecopt,
            IntPtr pvaIn,
            IntPtr pvaOut)
        {
            var text = Clipboard.GetText();
            if (string.IsNullOrEmpty(text))
                return VSConstants.S_OK;

            var pguidCmdGroupLocal = pguidCmdGroup;
            commandFilter.SelectionSwapExec(
                (tempSelections, targetSelections) =>
                {
                    var textParts = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    using (var edit = commandFilter.CreateEdit())
                    {
                        if (textParts.Length != tempSelections.Count)
                        {
                            foreach (var selection in tempSelections)
                                edit.Replace(selection.GetSelectionSpan().Span, text);
                        }
                        else
                        {
                            for (int i = 0; i < textParts.Length; ++i)
                                edit.Replace(tempSelections[i].GetSelectionSpan().Span, textParts[i]);
                        }

                        edit.Apply();
                    }

                    foreach (var selection in tempSelections)
                    {
                        // Add the selection into the list again - we do this so that we have
                        // clean selection details in the list and can handle any merging
                        targetSelections.InsertExpectEnd(selection);
                    }
                });

            pguidCmdGroup = pguidCmdGroupLocal;
            return VSConstants.S_OK;
        }

        private ITextEdit CreateEdit() => this.textView.TextBuffer.CreateEdit();

        private static class TempEditor
        {
            private static readonly object NextNumberLock = new object();
            private static int nextNumber = 1;

            public static int ShowNew(
                MultiCaretCommandFilter commandFilter,
                ref Guid pguidCmdGroup,
                uint nCmdID,
                uint nCmdexecopt,
                IntPtr pvaIn,
                IntPtr pvaOut)
            {
                string fileName;
                lock (NextNumberLock)
                {
                    fileName = $"Temp{nextNumber:0000}.{commandFilter.coreSettings.TempFileLanguage}";
                    ++nextNumber;
                }

                commandFilter.dte.ItemOperations.NewFile(
                    Name: fileName,
                    ViewKind: "{7651A703-06E5-11D1-8EBD-00A0C90F26EA}");

                return VSConstants.S_OK;
            }
        }

        private abstract class AddFromSearch
        {
            public int Select(
                MultiCaretCommandFilter commandFilter,
                ref Guid pguidCmdGroup,
                uint nCmdID,
                uint nCmdexecopt,
                IntPtr pvaIn,
                IntPtr pvaOut)
            {
                commandFilter.SelectionSwapExec(
                    (currentSelections, targetSelections) =>
                    {
                        targetSelections.InsertExpectEnd(currentSelections);

                        var currentText = currentSelections.Last.Value.GetSelectionSpan();
                        if (currentText.Length == 0)
                            return;

                        var findData = new FindData(
                            currentText.GetText(),
                            currentText.Snapshot,
                            this.FindOptions,
                            null);

                        this.Search(
                            commandFilter,
                            targetSelections,
                            currentSelections.First.Value,
                            currentText,
                            findData);
                    });

                return VSConstants.S_OK;
            }

            protected virtual FindOptions FindOptions => FindOptions.MatchCase;

            protected static void SearchOne(
                MultiCaretCommandFilter commandFilter,
                int startIndex,
                Func<SnapshotSpan, int> getSearchStart,
                Func<SingleSelection, bool> insert,
                FindData findData)
            {
                // Look for the next selection - if the exact same selection already exists then
                // we need to keep looking until we get back to the beginning.
                var searchStart = startIndex;
                while (true)
                {
                    var next = commandFilter.textSearch.FindNext(searchStart, true, findData);
                    if (!next.HasValue)
                        return;

                    // If we have looped round then break out
                    searchStart = getSearchStart(next.Value);
                    if (searchStart == startIndex)
                        return;

                    var selection = SingleSelection.Create(next.Value, false);
                    if (!selection.HasValue)
                        return;

                    // If we manage to insert then we are done
                    if (insert(selection.Value))
                        return;
                }
            }

            protected abstract void Search(
                MultiCaretCommandFilter commandFilter,
                SelectionList targetSelections,
                SingleSelection firstText,
                SnapshotSpan lastText,
                FindData findData);
        }

        private class SelectAllInDocument : AddFromSearch
        {
            protected override void Search(
                MultiCaretCommandFilter commandFilter,
                SelectionList targetSelections,
                SingleSelection firstText,
                SnapshotSpan lastText,
                FindData findData)
            {
                targetSelections.InsertExpectEnd(
                    commandFilter.textSearch.FindAll(findData).ToSingleSelections(false));
            }
        }

        private class SelectNextInDocument : AddFromSearch
        {
            protected override void Search(
                MultiCaretCommandFilter commandFilter,
                SelectionList targetSelections,
                SingleSelection firstText,
                SnapshotSpan lastText,
                FindData findData)
            {
                SearchOne(
                    commandFilter,
                    lastText.End.Position,
                    o => o.End.Position,
                    targetSelections.InsertExpectEnd,
                    findData);
            }
        }

        private class SelectPreviousInDocument : AddFromSearch
        {
            protected override FindOptions FindOptions => base.FindOptions | FindOptions.SearchReverse;

            protected override void Search(
                MultiCaretCommandFilter commandFilter,
                SelectionList targetSelections,
                SingleSelection firstText,
                SnapshotSpan lastText,
                FindData findData)
            {
                SearchOne(
                    commandFilter,
                    firstText.GetSelectionSpan().Start.Position,
                    o => o.Start.Position,
                    targetSelections.InsertExpectStart,
                    findData);
            }
        }
    }
}
