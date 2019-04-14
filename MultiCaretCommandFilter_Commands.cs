namespace MultiCaret
{
    using System;
    using System.Collections.Generic;
    using Microsoft.VisualStudio;

    internal partial class MultiCaretCommandFilter
    {
        private delegate int ExecCommand(
            MultiCaretCommandFilter commandFilter,
            ref Guid pguidCmdGroup,
            uint nCmdID,
            uint nCmdexecopt,
            IntPtr pvaIn,
            IntPtr pvaOut);

        private static readonly IDictionary<Guid, HashSet<uint>> ExecOnAllCommandHandlersByType =
            new Dictionary<Guid, HashSet<uint>>
            {
                {
                    typeof(VSConstants.VSStd2KCmdID).GUID, new HashSet<uint>
                    {
                        (uint)VSConstants.VSStd2KCmdID.RETURN,
                        (uint)VSConstants.VSStd2KCmdID.ECMD_TAB,
                        (uint)VSConstants.VSStd2KCmdID.TAB,
                        (uint)VSConstants.VSStd2KCmdID.BACKTAB,
                        (uint)VSConstants.VSStd2KCmdID.LEFT,
                        (uint)VSConstants.VSStd2KCmdID.LEFT_EXT,
                        (uint)VSConstants.VSStd2KCmdID.RIGHT,
                        (uint)VSConstants.VSStd2KCmdID.RIGHT_EXT,
                        (uint)VSConstants.VSStd2KCmdID.UP,
                        (uint)VSConstants.VSStd2KCmdID.UP_EXT,
                        (uint)VSConstants.VSStd2KCmdID.DOWN,
                        (uint)VSConstants.VSStd2KCmdID.DOWN_EXT,
                        (uint)VSConstants.VSStd2KCmdID.HOME,
                        (uint)VSConstants.VSStd2KCmdID.HOME_EXT,
                        (uint)VSConstants.VSStd2KCmdID.END,
                        (uint)VSConstants.VSStd2KCmdID.END_EXT,
                        (uint)VSConstants.VSStd2KCmdID.BOL,
                        (uint)VSConstants.VSStd2KCmdID.BOL_EXT,
                        (uint)VSConstants.VSStd2KCmdID.FIRSTCHAR,
                        (uint)VSConstants.VSStd2KCmdID.FIRSTCHAR_EXT,
                        (uint)VSConstants.VSStd2KCmdID.EOL,
                        (uint)VSConstants.VSStd2KCmdID.EOL_EXT,
                        (uint)VSConstants.VSStd2KCmdID.LASTCHAR,
                        (uint)VSConstants.VSStd2KCmdID.LASTCHAR_EXT,
                        (uint)VSConstants.VSStd2KCmdID.PAGEUP,
                        (uint)VSConstants.VSStd2KCmdID.PAGEUP_EXT,
                        (uint)VSConstants.VSStd2KCmdID.PAGEDN,
                        (uint)VSConstants.VSStd2KCmdID.PAGEDN_EXT,
                        (uint)VSConstants.VSStd2KCmdID.TOPLINE,
                        (uint)VSConstants.VSStd2KCmdID.TOPLINE_EXT,
                        (uint)VSConstants.VSStd2KCmdID.BOTTOMLINE,
                        (uint)VSConstants.VSStd2KCmdID.BOTTOMLINE_EXT,
                        (uint)VSConstants.VSStd2KCmdID.SELECTALL,
                        (uint)VSConstants.VSStd2KCmdID.SELTABIFY,
                        (uint)VSConstants.VSStd2KCmdID.SELUNTABIFY,
                        (uint)VSConstants.VSStd2KCmdID.SELLOWCASE,
                        (uint)VSConstants.VSStd2KCmdID.SELUPCASE,
                        (uint)VSConstants.VSStd2KCmdID.SELTOGGLECASE,
                        (uint)VSConstants.VSStd2KCmdID.SELTITLECASE,
                        (uint)VSConstants.VSStd2KCmdID.SELSWAPANCHOR,
                        (uint)VSConstants.VSStd2KCmdID.GOTOBRACE,
                        (uint)VSConstants.VSStd2KCmdID.GOTOBRACE_EXT,
                        (uint)VSConstants.VSStd2KCmdID.GOBACK,
                        (uint)VSConstants.VSStd2KCmdID.SELECTMODE,
                        (uint)VSConstants.VSStd2KCmdID.TOGGLE_OVERTYPE_MODE,
                        (uint)VSConstants.VSStd2KCmdID.DELETELINE,
                        (uint)VSConstants.VSStd2KCmdID.DELETETOEOL,
                        (uint)VSConstants.VSStd2KCmdID.DELETETOBOL,
                        (uint)VSConstants.VSStd2KCmdID.OPENLINEABOVE,
                        (uint)VSConstants.VSStd2KCmdID.OPENLINEBELOW,
                        (uint)VSConstants.VSStd2KCmdID.INDENT,
                        (uint)VSConstants.VSStd2KCmdID.UNINDENT,
                        (uint)VSConstants.VSStd2KCmdID.TRANSPOSECHAR,
                        (uint)VSConstants.VSStd2KCmdID.TRANSPOSEWORD,
                        (uint)VSConstants.VSStd2KCmdID.TRANSPOSELINE,
                        (uint)VSConstants.VSStd2KCmdID.DELETEWORDRIGHT,
                        (uint)VSConstants.VSStd2KCmdID.DELETEWORDLEFT,
                        (uint)VSConstants.VSStd2KCmdID.WORDPREV,
                        (uint)VSConstants.VSStd2KCmdID.WORDPREV_EXT,
                        (uint)VSConstants.VSStd2KCmdID.WORDNEXT,
                        (uint)VSConstants.VSStd2KCmdID.WORDNEXT_EXT,
                        (uint)VSConstants.VSStd2KCmdID.COMMENTBLOCK,
                        (uint)VSConstants.VSStd2KCmdID.UNCOMMENTBLOCK,
                        (uint)VSConstants.VSStd2KCmdID.FIRSTNONWHITEPREV,
                        (uint)VSConstants.VSStd2KCmdID.FIRSTNONWHITENEXT,
                        (uint)VSConstants.VSStd2KCmdID.FORMATSELECTION,
                        (uint)VSConstants.VSStd2KCmdID.LEFT_EXT_COL,
                        (uint)VSConstants.VSStd2KCmdID.RIGHT_EXT_COL,
                        (uint)VSConstants.VSStd2KCmdID.UP_EXT_COL,
                        (uint)VSConstants.VSStd2KCmdID.DOWN_EXT_COL,
                        (uint)VSConstants.VSStd2KCmdID.BOL_EXT_COL,
                        (uint)VSConstants.VSStd2KCmdID.EOL_EXT_COL,
                        (uint)VSConstants.VSStd2KCmdID.WORDPREV_EXT_COL,
                        (uint)VSConstants.VSStd2KCmdID.WORDNEXT_EXT_COL,
                        (uint)VSConstants.VSStd2KCmdID.COMMENT_BLOCK,
                        (uint)VSConstants.VSStd2KCmdID.UNCOMMENT_BLOCK,
                        (uint)VSConstants.VSStd2KCmdID.LAYOUTINDENT,
                        (uint)VSConstants.VSStd2KCmdID.LAYOUTUNINDENT
                    }
                },
                {
                    typeof(VSConstants.VSStd97CmdID).GUID, new HashSet<uint>
                    {
                        (uint)VSConstants.VSStd97CmdID.BeginLine,
                        (uint)VSConstants.VSStd97CmdID.EndLine,
                        (uint)VSConstants.VSStd97CmdID.BeginWord,
                        (uint)VSConstants.VSStd97CmdID.EndWord
                    }
                }
            };

        private static readonly IDictionary<Guid, IDictionary<uint, ExecCommand>> SpecialCommandsByType =
            new Dictionary<Guid, IDictionary<uint, ExecCommand>>
            {
                {
                    typeof(VSConstants.VSStd2KCmdID).GUID, new Dictionary<uint, ExecCommand>
                    {
                        { (uint)VSConstants.VSStd2KCmdID.CANCEL, HandleCancel },
                        { (uint)VSConstants.VSStd2KCmdID.TYPECHAR, HandleTypeChar },
                        { (uint)VSConstants.VSStd2KCmdID.DELETE, HandleDelete },
                        { (uint)VSConstants.VSStd2KCmdID.BACKSPACE, HandleBackspace },
                        { (uint)VSConstants.VSStd2KCmdID.DELETEKEY, HandleDelete }
                    }
                },
                {
                    typeof(VSConstants.VSStd97CmdID).GUID, new Dictionary<uint, ExecCommand>
                    {
                        { (uint)VSConstants.VSStd97CmdID.Escape, HandleCancel },
                        { (uint)VSConstants.VSStd97CmdID.Cancel, HandleCancel },

                        { (uint)VSConstants.VSStd97CmdID.Redo, HandleUndoRedo },
                        { (uint)VSConstants.VSStd97CmdID.MultiLevelRedo, HandleUndoRedo },
                        { (uint)VSConstants.VSStd97CmdID.Undo, HandleUndoRedo },
                        { (uint)VSConstants.VSStd97CmdID.MultiLevelUndo, HandleUndoRedo },

                        { (uint)VSConstants.VSStd97CmdID.Cut, HandleCut },
                        { (uint)VSConstants.VSStd97CmdID.Copy, HandleCopy },
                        { (uint)VSConstants.VSStd97CmdID.Paste, HandlePaste },
                        { (uint)VSConstants.VSStd97CmdID.Delete, HandleDelete }
                    }
                },
                {
                    MultiCaretCommands.CommandSet, new Dictionary<uint, ExecCommand>
                    {
                        { (uint)MultiCaretCommandId.SplitSelectionIntoLines, SplitSelectionIntoLines },
                        { (uint)MultiCaretCommandId.SelectAllInDocument, new SelectAllInDocument().Select },
                        { (uint)MultiCaretCommandId.SelectNextInDocument, new SelectNextInDocument().Select },
                        { (uint)MultiCaretCommandId.SelectPreviousInDocument, new SelectPreviousInDocument().Select },
                        { (uint)MultiCaretCommandId.NewTempDocument, TempEditor.ShowNew },
                    }
                }
            };

        /// <summary>
        /// Commands that we can run when we are not in multi-select mode.
        /// </summary>
        private static readonly IDictionary<Guid, HashSet<uint>> AnyTimeCommands =
            new Dictionary<Guid, HashSet<uint>>
            {
                {
                    MultiCaretCommands.CommandSet, new HashSet<uint>
                    {
                        (uint)MultiCaretCommandId.SplitSelectionIntoLines,
                        (uint)MultiCaretCommandId.SelectNextInDocument,
                        (uint)MultiCaretCommandId.SelectPreviousInDocument,
                        (uint)MultiCaretCommandId.SelectAllInDocument,
                        (uint)MultiCaretCommandId.NewTempDocument,
                    }
                }
            };
    }
}
