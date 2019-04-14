namespace MultiCaret
{
    using System.ComponentModel.Composition;
    using Microsoft.VisualStudio.Language.Intellisense;
    using Microsoft.VisualStudio.Utilities;

    [Export(typeof(IIntellisensePresenterProvider))]
    [ContentType("code")]
    [Order(Before = "default")]
    [Name("MultiCaret Intellisense")]
    internal class MultiCaretIntellisenseProvier : IIntellisensePresenterProvider
    {
        public IIntellisensePresenter TryCreateIntellisensePresenter(IIntellisenseSession session)
        {
            // If we are in multi-edit mode then we don't want
            // intellisense popping up messing that up.
            if (session.TextView.Properties.TryGetProperty(
                    typeof(MultiCaretCommandFilter),
                    out MultiCaretCommandFilter commandFilter)
                && commandFilter.InMultiEditMode)
            {
                return new NoneIntellisensePresenter();
            }

            // If we are not in multi-edit mode then let visual
            // studio handle intellisense as normal.
            return null;
        }

        private class NoneIntellisensePresenter : IIntellisensePresenter
        {
            public IIntellisenseSession Session { get; }
        }
    }
}