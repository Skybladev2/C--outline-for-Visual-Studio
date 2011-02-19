using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.Utilities;
using Microsoft.VisualStudio.Text.Tagging;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Projection;


namespace CSharpOutline
{
	[Export(typeof(ITaggerProvider))]
	[TagType(typeof(IOutliningRegionTag))]
	[ContentType("CSharp")]
	internal sealed class JSOutliningTaggerProvider : ITaggerProvider
	{
		[Import]
		IClassifierAggregatorService classifierAggregator = null;

		public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
		{
			//no outlining for projection buffers
			if (buffer is IProjectionBuffer) return null;

			IClassifier classifier = classifierAggregator.GetClassifier(buffer);
			//var spans = c.GetClassificationSpans(new SnapshotSpan(buffer.CurrentSnapshot, 0, buffer.CurrentSnapshot.Length));
			//create a single tagger for each buffer.
			return buffer.Properties.GetOrCreateSingletonProperty<ITagger<T>>(() => new CSharpOutliningTagger(buffer, classifier) as ITagger<T>);
		} 
	}
}
