using System.Collections.Generic;
using JetBrains.Annotations;
using OpenScadGraphEditor.Actions;
using OpenScadGraphEditor.Library;
using OpenScadGraphEditor.Refactorings;
using OpenScadGraphEditor.Utils;
using OpenScadGraphEditor.Widgets.AddDialog;

namespace OpenScadGraphEditor.Nodes.ForLoop
{
    
    [UsedImplicitly]
    public class ForLoopAddDialogEntryFactory : IAddDialogEntryFactory
    {
        public IEnumerable<IAddDialogEntry> BuildEntries(IEditorContext editorContext)
        {
            // add an entry for the for-comprehension loop
            yield return new BoundPairBasedEntry(Resources.ScadBuiltinIcon, "For loop [Frlp]",
                "for-loop, iterate, iteration",
                NodeFactory.Build<ForLoopStart>,
                NodeFactory.Build<ForLoopEnd>,
                editorContext);
        }
    }
}