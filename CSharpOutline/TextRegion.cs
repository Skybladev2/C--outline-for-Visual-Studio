using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Tagging;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Microsoft.VisualStudio.Text.Classification;

namespace CSharpOutline
{
    internal enum TextRegionType
    {
        None,
        Block // {}
    }

    class TextRegion
    {
        #region Props
        public SnapshotPoint StartPoint { get; set; }
        public SnapshotPoint EndPoint { get; set; }

        /// <summary>
        /// whether region has endpoint
        /// </summary>
        public bool Complete
        {
            get { return EndPoint.Snapshot != null; }
        }
        public ITextSnapshotLine StartLine { get { return StartPoint.GetContainingLine(); } }
        public ITextSnapshotLine EndLine { get { return EndPoint.GetContainingLine(); } }
        public TextRegionType RegionType { get; private set; }
        public string Name { get; set; }

        public TextRegion Parent { get; set; }
        public List<TextRegion> Children { get; set; }

        public string InnerText
        {
            get { return StartPoint.Snapshot.GetText(StartPoint.Position, EndPoint.Position - StartPoint.Position + 1); }
        }
        #endregion

        #region Constructors
        /// <summary>
        /// text from first line start to region start
        /// </summary>
        public string TextBefore
        {
            get { return StartLine.GetText().Substring(0, StartPoint - StartLine.Start); }
        }

        public TextRegion()
        {
            Children = new List<TextRegion>();
        }

        public TextRegion(SnapshotPoint startPoint, TextRegionType type)
            : this()
        {
            StartPoint = startPoint;
            RegionType = type;
        }
        #endregion

        public TagSpan<IOutliningRegionTag> AsOutliningRegionTag()
        {
            SnapshotSpan span = this.AsSnapshotSpan();
            string hoverText = span.GetText();
            //hoverText = hoverText.Substring(hoverText.IndexOf('{'));
            // TODO: add tabs and space removing for well-formed formatting
            return new TagSpan<IOutliningRegionTag>(span, new OutliningRegionTag(false, false, GetCollapsedText(), hoverText));
        }

        public SnapshotSpan AsSnapshotSpan()
        {
            return new SnapshotSpan(this.StartPoint, this.EndPoint);
        }

        private string GetCollapsedText()
        {
            return "...";
        }

        /// <summary>
        /// parses input buffer, searches for region start
        /// </summary>
        /// <param name="parser"></param>
        /// <returns>created region or null</returns>
        public static TextRegion TryCreateRegion(SnapshotParser parser)
        {
            SnapshotPoint point = parser.CurrentPoint;
            ClassificationSpan span = parser.CurrentSpan;
            if (span == null)
            {
                char c = point.GetChar();
                switch (c)
                {
                    case '{':
                        return new TextRegion(point, TextRegionType.Block);
                }
            }
            return null;
        }

        /// <summary>
        /// tries to close region
        /// </summary>
        /// <param name="parser">parser</param>
        /// <returns>whether region was closed</returns>
        public bool TryComplete(SnapshotParser parser)
        {
            SnapshotPoint point = parser.CurrentPoint;
            ClassificationSpan span = parser.CurrentSpan;

            if (span == null)
            {
                char c = point.GetChar();
                if (RegionType == TextRegionType.Block && c == '}')
                {
                    EndPoint = point + 1;
                }
            }

            return Complete;
        }

        /// <summary>
        /// parses buffer
        /// </summary>
        /// <param name="parser"></param>
        /// <param name="parent">parent region or null</param>
        /// <returns>a region with its children or null</returns>
        public static TextRegion ParseBuffer(SnapshotParser parser, TextRegion parent)
        {
            for (; !parser.AtEnd(); parser.MoveNext())
            {
                TextRegion r = TextRegion.TryCreateRegion(parser);

                if (r != null)
                {
                    parser.MoveNext();
                    //found the start of the region
                    if (!r.Complete)
                    {
                        //searching for child regions						
                        while (TextRegion.ParseBuffer(parser, r) != null) ;
                        //found everything						
                        r.ExtendStartPoint();
                    }
                    //adding to children or merging with last child
                    r.Parent = parent;
                    parent.Children.Add(r);
                    return r;
                }
                //found parent's end - terminating parsing
                if (parent.TryComplete(parser))
                {
                    parser.MoveNext();
                    return null;
                }
            }
            return null;
        }

        /// <summary>
        /// Tries to move region start point up to get C#-like outlining
        /// 
        /// for (var k in obj)
        /// { -- from here
        /// 
        /// for (var k in obj) -- to here
        /// {
        /// </summary>
        private void ExtendStartPoint()
        {
            //some are not extended
            if (!Complete
                || StartLine.LineNumber == EndLine.LineNumber
                || !string.IsNullOrWhiteSpace(TextBefore)) return;

            //how much can we move region start
            int upperLimit = 0;
            if (this.Parent != null)
            {
                int childPosition = Parent.Children.IndexOf(this);
                if (childPosition == 0)
                {
                    //this region is first child of its parent
                    //we can go until the parent's start
                    upperLimit = Parent.RegionType != TextRegionType.None ? Parent.StartLine.LineNumber + 1 : 0;
                }
                else
                {
                    //there is previous child
                    //we can go until its end
                    TextRegion prevRegion = Parent.Children[childPosition - 1];
                    upperLimit = prevRegion.EndLine.LineNumber + (prevRegion.EndLine.LineNumber == prevRegion.StartLine.LineNumber ? 0 : 1);
                }
            }

            //now looking up to calculated upper limit for non-empty line
            for (int i = StartLine.LineNumber - 1; i >= upperLimit; i--)
            {
                ITextSnapshotLine line = StartPoint.Snapshot.GetLineFromLineNumber(i);
                if (!string.IsNullOrWhiteSpace(line.GetText()))
                {
                    //found such line, placing region start at its end
                    StartPoint = line.End;
                    return;
                }
            }
        }
    }
}