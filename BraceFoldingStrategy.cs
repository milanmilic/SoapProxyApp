using System.Collections.Generic;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Folding;

namespace SoapProxyApp
{
    public class BraceFoldingStrategy
    {
        public void UpdateFoldings(FoldingManager manager, TextDocument document)
        {
            int firstErrorOffset;
            IEnumerable<NewFolding> foldings = CreateNewFoldings(document, out firstErrorOffset);
            manager.UpdateFoldings(foldings, firstErrorOffset);
        }

        public IEnumerable<NewFolding> CreateNewFoldings(TextDocument document, out int firstErrorOffset)
        {
            firstErrorOffset = -1;
            return CreateNewFoldings(document);
        }

        public IEnumerable<NewFolding> CreateNewFoldings(ITextSource document)
        {
            List<NewFolding> newFoldings = new List<NewFolding>();
            Stack<int> startOffsets = new Stack<int>();
            int lastNewLineOffset = 0;
            bool inString = false;
            bool isEscape = false;

            for (int i = 0; i < document.TextLength; i++)
            {
                char c = document.GetCharAt(i);

                if (inString)
                {
                    if (isEscape)
                        isEscape = false;
                    else if (c == '\\')
                        isEscape = true;
                    else if (c == '"')
                        inString = false;
                }
                else
                {
                    if (c == '"')
                    {
                        inString = true;
                    }
                    else if (c == '{' || c == '[')
                    {
                        startOffsets.Push(i);
                    }
                    else if (c == '}' || c == ']')
                    {
                        if (startOffsets.Count > 0)
                        {
                            int startOffset = startOffsets.Pop();
                            // Only fold if the block spans across lines
                            if (startOffset < lastNewLineOffset)
                            {
                                newFoldings.Add(new NewFolding(startOffset, i + 1) { Name = "{...}" });
                            }
                        }
                    }
                    else if (c == '\n' || c == '\r')
                    {
                        lastNewLineOffset = i + 1;
                    }
                }
            }
            newFoldings.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
            return newFoldings;
        }
    }
}
