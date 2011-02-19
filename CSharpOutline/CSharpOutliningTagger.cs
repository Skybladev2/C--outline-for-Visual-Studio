using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Threading;
using Microsoft.VisualStudio.Text.Classification;

namespace CSharpOutline
{
	class CSharpOutliningTagger:ITagger<IOutliningRegionTag>
	{
		//Add some fields to track the text buffer and snapshot and to accumulate the sets of lines that should be tagged as outlining regions. 
		//This code includes a list of Region objects (to be defined later) that represent the outlining regions.		
		private ITextBuffer Buffer;
		private ITextSnapshot Snapshot;
		private List<TextRegion> Regions = new List<TextRegion>();
		private DateTime LastOutlined = new DateTime();
		private IClassifier Classifier;

		public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

		public CSharpOutliningTagger(ITextBuffer buffer, IClassifier classifier)
		{
			this.Buffer = buffer;
			this.Snapshot = buffer.CurrentSnapshot;
			this.Classifier = classifier;
			this.Outline(Snapshot);
			this.Buffer.Changed += BufferChanged;
			//this.Classifier.ClassificationChanged += BufferChanged;			
		}


		//Implement the GetTags method, which instantiates the tag spans. 
		//This example assumes that the spans in the NormalizedSpanCollection passed in to the method are contiguous, although this may not always be the case. 
		//This method instantiates a new tag span for each of the outlining regions.
		public IEnumerable<ITagSpan<IOutliningRegionTag>> GetTags(NormalizedSnapshotSpanCollection spans)
		{
			if (spans.Count == 0)
				yield break;
			List<TextRegion> currentRegions = this.Regions;
			ITextSnapshot currentSnapshot = this.Snapshot;
			SnapshotSpan entire = new SnapshotSpan(spans[0].Start, spans[spans.Count - 1].End).TranslateTo(currentSnapshot, SpanTrackingMode.EdgeExclusive);
			int startLineNumber = entire.Start.GetContainingLine().LineNumber;
			int endLineNumber = entire.End.GetContainingLine().LineNumber;
			foreach (TextRegion region in currentRegions)
			{
				if (region.StartLine.LineNumber <= endLineNumber && region.EndLine.LineNumber >= startLineNumber)
				{					
					yield return region.AsOutliningRegionTag();					
				}
			}
		}

		//Add a BufferChanged event handler that responds to Changed events by parsing the text buffer.
		private void BufferChanged(object sender, TextContentChangedEventArgs e)
		{
			// If this isn't the most up-to-date version of the buffer, then ignore it for now (we'll eventually get another change event).
			if (e.After != Buffer.CurrentSnapshot) return;
			ITextSnapshot snapshot = Buffer.CurrentSnapshot;

			// Bender doing some magic here.
			// I suppose this need to prevent hangs when outlining large regions 
			// or while typing fast
			if ((DateTime.Now - LastOutlined).TotalMilliseconds > snapshot.Length / 10)
			{
				//Stopwatch w = new Stopwatch();
				//w.Start();				
				this.Outline(snapshot);

				/*IList<ClassificationSpan> spans = Classifier.GetClassificationSpans(new SnapshotSpan(Snapshot, 0, Snapshot.Length));
				Debug.Print("-----------------------------------------------------------------");
				foreach (ClassificationSpan s in spans)
					Debug.Print(s.ClassificationType.ToString().PadRight(24) + s.Span.GetText().PadRight(24) + new SnapshotSpan(s.Span.Start, s.Span.End).GetText()); */
				//w.Stop();
				//Debug.Print(w.ElapsedMilliseconds.ToString() + " ms");
				LastOutlined = DateTime.Now;
			}
		}

		//Add a method that parses the buffer. The example given here is for illustration only. 
		//It synchronously parses the buffer into nested outlining regions.
		private void Outline(ITextSnapshot snapshot)
		{
			TextRegion regionTree = new TextRegion();			
			SnapshotParser parser = new SnapshotParser(snapshot, Classifier);

			//parsing snapshot
			while(TextRegion.ParseBuffer(parser, regionTree) != null);
						
			List<TextRegion> newRegions = GetRegionList(regionTree);

			List<Span> oldSpans = Regions.ConvertAll(r => r.AsSnapshotSpan().TranslateTo(snapshot, SpanTrackingMode.EdgeExclusive).Span);
			List<Span> newSpans = newRegions.ConvertAll(r => r.AsSnapshotSpan().Span);

			NormalizedSpanCollection oldSpanCollection = new NormalizedSpanCollection(oldSpans);
			NormalizedSpanCollection newSpanCollection = new NormalizedSpanCollection(newSpans);

			//the changed regions are regions that appear in one set or the other, but not both.
			NormalizedSpanCollection removed = NormalizedSpanCollection.Difference(oldSpanCollection, newSpanCollection);

			int changeStart = int.MaxValue;
			int changeEnd = -1;

			if (removed.Count > 0)
			{
				changeStart = removed[0].Start;
				changeEnd = removed[removed.Count - 1].End;
			}

			if (newSpans.Count > 0)
			{
				changeStart = Math.Min(changeStart, newSpans[0].Start);
				changeEnd = Math.Max(changeEnd, newSpans[newSpans.Count - 1].End);
			}

			this.Snapshot = snapshot;
			this.Regions = newRegions;

			if (changeStart <= changeEnd && this.TagsChanged != null)
			{					
				this.TagsChanged(this, new SnapshotSpanEventArgs(
						new SnapshotSpan(this.Snapshot, Span.FromBounds(changeStart, changeEnd))));
			}
		}

		private List<TextRegion> GetRegionList(TextRegion tree)
		{			
			List<TextRegion> res = new List<TextRegion>(tree.Children.Count);
			foreach (TextRegion r in tree.Children)
			{
				if (r.Complete && r.StartLine.LineNumber != r.EndLine.LineNumber)
					res.Add(r);
				if (r.Children.Count != 0)
					res.AddRange(GetRegionList(r));
			}
			return res;
		}	
	}
}
