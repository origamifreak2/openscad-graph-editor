using System.Linq;
using OpenScadGraphEditor.Library;
using OpenScadGraphEditor.Nodes;
using OpenScadGraphEditor.Utils;

namespace OpenScadGraphEditor.Refactorings
{
    public class DeleteInvokableRefactoring : Refactoring
    {
        private readonly InvokableDescription _description;

        public DeleteInvokableRefactoring(InvokableDescription description)
        {
            _description = description;
        }

        public override void PerformRefactoring(RefactoringContext context)
        {
            // find all graphs that refer to this invokable and make them refactorable
            var graphs = context.Project.FindContainingReferencesTo(_description)
                .Select(context.MakeRefactorable)
                .ToList();
            
            // now walk all graphs and remove all nodes that refer to this invokable
            foreach (var graph in graphs)
            {
                var nodesToKill = graph.GetAllNodes()
                    .Where(it =>
                        it is IReferToAnInvokable iReferToAnInvokable &&
                        iReferToAnInvokable.InvokableDescription == _description)
                    .Where(it =>
                        !(it is ICannotBeDeleted)) // keep nodes that cannot be deleted, we will kill them with the graph later
                    .ToList();

                // first kill all connections involving any of these nodes from the graph
                graph.GetAllConnections().Where(it => nodesToKill.Any(it.InvolvesNode))
                    .ToList()
                    .ForAll(it => graph.RemoveConnection(it));
                
                // then kill the nodes themselves
                nodesToKill.ForAll(it => graph.RemoveNode(it));
            }
            
            // finally delete the graph defining the invokable itself.
            var definingGraph = context.Project.FindDefiningGraph(_description);
            context.Project.RemoveInvokable(_description);
            context.MarkDeleted(definingGraph);
        }
    }
}