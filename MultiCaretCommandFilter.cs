namespace MultiCaret
{
    using System;
    using System.Collections.Generic;
    using EnvDTE;
    using EnvDTE80;
    using Microsoft.VisualStudio;
    using Microsoft.VisualStudio.OLE.Interop;
    using Microsoft.VisualStudio.Shell;
    using Microsoft.VisualStudio.Text;
    using Microsoft.VisualStudio.Text.Editor;
    using Microsoft.VisualStudio.Text.Operations;

    internal partial class MultiCaretCommandFilter : IOleCommandTarget
    {
        private readonly SVsServiceProvider serviceProvider;
        private readonly IWpfTextView textView;
        private readonly ITextSearchService textSearch;
        private readonly MultiCaretTextAdornment selectionDisplay;
        private readonly ICoreSettings coreSettings;
        private readonly DTE2 dte;
        private IOleCommandTarget nextTarget;

        private SelectionList selections = new SelectionList();
        private SelectionList spareSelectionsList = new SelectionList();
        
        // This is used to track the expected edit which enables us
        // to suppress auto-format when in multi-edit mode.
        private ITextVersion expectedEditVersion;

        public MultiCaretCommandFilter(
            SVsServiceProvider serviceProvider,
            IWpfTextView textView,
            ITextSearchService textSearch,
            MultiCaretTextAdornment selectionDisplay,
            ICoreSettings coreSettings)
        {
            this.serviceProvider = serviceProvider;
            this.textView = textView;
            this.textSearch = textSearch;
            this.selectionDisplay = selectionDisplay;
            this.coreSettings = coreSettings;
            this.dte = (DTE2)ServiceProvider.GlobalProvider.GetService(typeof(DTE));

            textView.TextBuffer.Changing += this.HandleTextChanging;
        }

        public bool InMultiEditMode { get; private set; }

        public void SetNextTarget(IOleCommandTarget next)
        {
            this.nextTarget = next;
        }

        public void HandleClickAdd(IEnumerable<SingleSelection> newSelections)
        {
            try
            {
                this.selections.Insert(newSelections);
                this.selectionDisplay.SetSelections(this.selections);
            }
            finally
            {
                this.UpdateInMultiEditMode();
            }
        }

        public void HandleClickClear()
        {
            try
            {
                this.selections.Clear();
                this.selectionDisplay.SetSelections(null);
            }
            finally
            {
                this.UpdateInMultiEditMode();
            }
        }

        public void HandleClickComplete(IEnumerable<SingleSelection> newSelections)
        {
            try
            {
                this.selections.Insert(newSelections);

                // Remove the last selection from the list - this will be the Visual Studio
                // managed selection - we want to add then remove incase it gets merged with
                // another selection as a result of the edit - we also need to set this selection
                // as the VS selection
                this.selections.RemoveLast().SetSelection(this.textView);

                // Update selections display
                this.selectionDisplay.SetSelections(this.selections);
            }
            finally
            {
                this.UpdateInMultiEditMode();
            }
        }

        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            // Allow automation to work as expected and do nothing if we have
            // no selections and this is not a non-multi-select custom command
            if (this.NotUsedQueryStatus(pguidCmdGroup, cCmds, prgCmds))
                return this.nextTarget.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
            
            // Check if this is something we support
            IDictionary<uint, ExecCommand> specialCommands = null;
            if (ExecOnAllCommandHandlersByType.TryGetValue(pguidCmdGroup, out var execAllCommands)
                || SpecialCommandsByType.TryGetValue(pguidCmdGroup, out specialCommands))
            {
                // If we support the command type then see if we support the command
                for (int i = 0; i < cCmds; i++)
                {
                    if ((execAllCommands != null && execAllCommands.Contains(prgCmds[i].cmdID))
                        || (specialCommands != null && specialCommands.ContainsKey(prgCmds[i].cmdID)))
                    {
                        prgCmds[i].cmdf = (uint)(OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED);
                        return VSConstants.S_OK;
                    }
                }
            }

            // If we do not know the command type then pass on the enquiry.
            return this.nextTarget.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }

        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            try
            {
                // Allow automation to work as expected and do nothing if we have
                // no selections and this is not a non-multi-select custom command
                // or a visual studio command that falls outside the scope of multi-select
                if (this.NotUsedExec(pguidCmdGroup, nCmdID))
                {
                    // We set the expected edit version to null here so that commands that change the
                    // document can run unaffected
                    this.expectedEditVersion = null;
                    return this.nextTarget.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
                }

                // Most commands use the ExecOnAll method and simply execute the input command
                // on the all the selections. We check if the command is one of those here.
                if (ExecOnAllCommandHandlersByType.TryGetValue(pguidCmdGroup, out var execAllCommands)
                    && execAllCommands.Contains(nCmdID))
                {
                    return this.ExecOnAll(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
                }

                // Some commands have special handlers registered, if this is one of those then use that handler.
                if (SpecialCommandsByType.TryGetValue(pguidCmdGroup, out var specialCommands)
                    && specialCommands.TryGetValue(nCmdID, out var specialCommand))
                {
                    this.expectedEditVersion = this.textView.TextSnapshot.Version;
                    return specialCommand(this, ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
                }

                // If we got here the command is one we don't recognise. In most cases we simply want
                // to pass through once since most commands of this type are control commands.
                // This does mean that any special navigation and edit commands will not behave as
                // expected (e.g. resharper special navigation)
                // We set the expected edit version to null here so that commands that change the
                // document can run unaffected
                this.expectedEditVersion = null;
                return this.nextTarget.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
            }
            finally
            {
                this.UpdateInMultiEditMode();
            }
        }

        private bool NotUsedQueryStatus(Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds)
        {
            if (VsShellUtilities.IsInAutomationFunction(this.serviceProvider))
                return true;

            if (this.InMultiEditMode)
                return false;

            for (int i = 0; i < cCmds; ++i)
            {
                if (AnyTimeCommands.TryGetValue(pguidCmdGroup, out var commands)
                    && commands.Contains(prgCmds[i].cmdID))
                {
                    return false;
                }
            }

            return true;
        }

        private bool NotUsedExec(Guid pguidCmdGroup, uint cmdId)
        {
            if (VsShellUtilities.IsInAutomationFunction(this.serviceProvider))
                return true;

            if (this.InMultiEditMode)
                return false;

            return !(AnyTimeCommands.TryGetValue(pguidCmdGroup, out var commands)
                && commands.Contains(cmdId));
        }

        private int ExecOnAll(
            ref Guid pguidCmdGroup,
            uint nCmdID,
            uint nCmdexecopt,
            IntPtr pvaIn,
            IntPtr pvaOut)
        {
            var result = 0;
            var pguidCmdGroupLocal = pguidCmdGroup;
            this.SelectionSwapExec(
                (tempSelections, targetSelections) =>
                {
                    // Begin the edit.
                    this.dte.UndoContext.Open("Multi-point edit");

                    foreach (var selection in tempSelections)
                    {
                        selection.SetSelection(this.textView);
                        this.expectedEditVersion = this.textView.TextSnapshot.Version;
                        result = this.nextTarget.Exec(ref pguidCmdGroupLocal, nCmdID, nCmdexecopt, pvaIn, pvaOut);

                        // Add the selection into the list again - we do this so that we have
                        // clean selection details in the list and can handle any merging
                        targetSelections.InsertExpectEnd(Utils.GetCurrentSelections(this.textView));
                    }

                    // End the edit
                    this.dte.UndoContext.Close();
                });

            pguidCmdGroup = pguidCmdGroupLocal;
            return result;
        }

        private void SelectionSwapExec(Action<SelectionList, SelectionList> execute)
        {
            // Swap selections around - we rebuild the list each time to
            // capture the changes that the command has caused. We cache
            // the old list for use next time to avoid additional memory
            // allocation and GC loading.
            
            // Clear display so that we do not redraw annotations for every change
            this.selectionDisplay.SetSelections(null);

            var tempSelections = this.selections;
            this.selections = this.spareSelectionsList;
            this.selections.Clear();
            this.spareSelectionsList = tempSelections;

            // Add the current selection into the list so that we apply
            // the operation to the entire set of points.
            tempSelections.InsertExpectEnd(Utils.GetCurrentSelections(this.textView));

            // Do whatever we are doing
            execute(tempSelections, this.selections);

            // Remove the last selection from the list - this will be the Visual Studio
            // managed selection - we want to add then remove incase it gets merged with
            // another selection as a result of the edit - we also need to set this selection
            // as the VS selection
            this.selections.RemoveLast().SetSelection(this.textView);

            // Update selections display
            this.selectionDisplay.SetSelections(this.selections);
        }

        private void HandleTextChanging(object sender, TextContentChangingEventArgs e)
        {
            // If we are in multi-edit mode then look to see if the change is applied
            // to the version we were expecting to edit - if not then we assume this
            // is an auto-format change which we do not want
            if (this.InMultiEditMode
                && this.expectedEditVersion != null
                && e.BeforeVersion != this.expectedEditVersion)
            {
                e.Cancel();
            }
        }

        private void UpdateInMultiEditMode() => this.InMultiEditMode = this.selections.Count != 0;
    }
}
