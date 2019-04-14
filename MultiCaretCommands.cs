namespace MultiCaret
{
    using System;

    public enum MultiCaretCommandId
    {
        SplitSelectionIntoLines = 1,
        SelectAllInDocument = 2,
        SelectNextInDocument = 3,
        SelectPreviousInDocument = 4,
        NewTempDocument = 5,
    }

    /// <summary>
    /// Useful reference:
    /// https://docs.microsoft.com/en-gb/visualstudio/extensibility/walkthrough-creating-a-view-adornment-commands-and-settings-column-guides
    /// </summary>
    internal static class MultiCaretCommands
    {
        public static readonly Guid CommandSet = new Guid("a11dc8f8-5559-4dad-bbc9-ed929fe5424a");
    }
}
