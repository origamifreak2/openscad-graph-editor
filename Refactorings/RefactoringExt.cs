using System.Collections.Generic;
using System.Linq;
using OpenScadGraphEditor.Library;
using OpenScadGraphEditor.Nodes;
using static System.Text.RegularExpressions.Regex;

namespace OpenScadGraphEditor.Refactorings
{
    public static class RefactoringExt
    {
        /// <summary>
        /// Finds all nodes in the project that refer to the given <see cref="InvokableDescription"/>
        /// </summary>
        public static IEnumerable<ReferencingNode<IReferToAnInvokable, ScadGraph>> FindAllReferencingNodes(
            this ScadProject project, InvokableDescription description)
        {
            var graphs = project.AllDeclaredInvokables;
            foreach (var graph in graphs)
            {
                foreach (var node in graph.GetAllNodes().OfType<IReferToAnInvokable>()
                             .Where(it => it.InvokableDescription == description))
                {
                    yield return new ReferencingNode<IReferToAnInvokable, ScadGraph>(graph, (ScadNode) node, node);
                }
            }
        }

        /// <summary>
        /// Finds all nodes in the project that refer to the given <see cref="VariableDescription"/>
        /// </summary>
        public static IEnumerable<ReferencingNode<IReferToAVariable, ScadGraph>> FindAllReferencingNodes(
            this ScadProject project,
            VariableDescription description)
        {
            var graphs = project.AllDeclaredInvokables;
            foreach (var graph in graphs)
            {
                foreach (var node in graph.GetAllNodes().OfType<IReferToAVariable>()
                             .Where(it => it.VariableDescription == description))
                {
                    yield return new ReferencingNode<IReferToAVariable, ScadGraph>(graph, (ScadNode) node, node);
                }
            }
        }

        /// <summary>
        /// Ensures that the given name is a safe identifier for OpenSCAD. Valid identifiers only contain letters
        /// numbers and underscores and start with a letter or a $ sign. If the name is not a valid identifier, it is
        /// converted to a valid identifier by replacing all invalid characters with an underscore. If the identifier
        /// starts with a number an underscore is prepended. A null or empty string is converted to a single underscore.
        /// </summary>
        public static string AsSafeIdentifier(this string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                return "_";
            }
            
            var result = identifier;
            if (!char.IsLetter(result[0]) && result[0] != '$')
            {
                result = "_" + result;
            }
            
            // at this point the first character is guaranteed to be a letter or a $.
            // for the rest of the string regex replace any character that is not a letter, number or underscore with an underscore
            result = result.Substring(0, 1) + Replace(result.Substring(1), "[^a-zA-Z0-9_]", "_");
            return result;
            
        }
        
        /// <summary>
        /// Generates a safe name for any member of the project. If the name is already used, a number is appended to the name.
        /// </summary>
        public static string SafeName(this ScadProject project, string name)
        {
            var safeIdentifier = name.AsSafeIdentifier();
            
            if (!project.IsNameUsed(safeIdentifier))
            {
                return safeIdentifier;
            }
            
            var result = safeIdentifier;
            var i = 2;
            while (project.IsNameUsed(result))
            {
                result = $"{safeIdentifier}{i}";
                i++;
            }

            return result;
        }


        /// <summary>
        /// Generates a safe name for a parameter of an InvokableDescription. If the name is already used, a number is appended to the name.
        /// </summary>
        public static string SafeParameterName(this InvokableDescription invokableDescription, string name)
        {
            var safeIdentifier = name.AsSafeIdentifier();
            
            if (invokableDescription.Parameters.All(it => it.Name != safeIdentifier))
            {
                return safeIdentifier;
            }
            
            var result = safeIdentifier;
            var i = 2;
            while (invokableDescription.Parameters.Any(it => it.Name == result))
            {
                result = $"{safeIdentifier}{i}";
                i++;
            }
            
            return result;
        }
        
        /// <summary>
        /// Creates a new ScadGraph by cloning the selected nodes and the connections between them. Will not clone
        /// entry and return nodes (e.g. all nodes that cannot be deleted). Returns a dictionary which maps the original
        /// node ids to the new node ids (useful for post processing).
        /// </summary>
        public static ScadGraph CloneSelection(this ScadGraph source,IReferenceResolver referenceResolver, IEnumerable<ScadNode> selection,
            out Dictionary<string, string> mappedIds)
        {
            var result = new ScadGraph();
            
            // when we make a copy we need to take special care about bound nodes
            // each bound node needs to have its partner within the copy even if it was not originally selected.
            // so we look up all bound nodes in the selection and if its partner is not in the
            // selection we silently add it.

            var correctedSelection = new HashSet<ScadNode>(selection);

            var boundNodes = correctedSelection.Where(it => it is IAmBoundToOtherNode).ToList();
            foreach (var boundNode in boundNodes)
            {
                var boundTo = (IAmBoundToOtherNode) boundNode;
                var partner = source.ById(boundTo.OtherNodeId);

                if (!correctedSelection.Contains(partner))
                {
                    correctedSelection.Add(partner);
                }
            }
            
            // remove all nodes which cannot be deleted (which are usually nodes that are built in so they cannot be copied either).
            var sanitizedSet = correctedSelection
                .Where(it => !(it is ICannotBeDeleted))
                .ToHashSet();

            var idMapping = new Dictionary<string, string>();
            // make copies of all the nodes and put them into the copy buffer
            foreach (var node in sanitizedSet)
            {
                var copy = NodeFactory.Duplicate(node, referenceResolver);
                // make note of the id mapping, because we need this to resolve connections
                // and bound nodes later.
                idMapping[node.Id] = copy.Id;
                result.AddNode(copy);
            }

            // correct the id mappings of any bound nodes
            foreach (var node in result.GetAllNodes().Where(it => it is IAmBoundToOtherNode))
            {
                var partnerId = ((IAmBoundToOtherNode) node).OtherNodeId;
                // find out which id the partner has in the copy
                var partnerCopyId = idMapping[partnerId];
                // and set the partner id to the copy id
                ((IAmBoundToOtherNode) node).OtherNodeId = partnerCopyId;
            }

            //  copy all connections which are between the selected nodes
            foreach (var connection in source.GetAllConnections())
            {
                if (sanitizedSet.Contains(connection.From) && sanitizedSet.Contains(connection.To))
                {
                    result.AddConnection(idMapping[connection.From.Id], connection.FromPort,
                        idMapping[connection.To.Id], connection.ToPort);
                }
            }

            mappedIds = idMapping;
            return result;
        }

        public readonly struct ReferencingNode<T, TGraphType> where TGraphType : ScadGraph
        {
            public TGraphType Graph { get; }
            public ScadNode Node { get; }
            public T NodeAsReference { get; }
            
            public ReferencingNode(TGraphType graph, ScadNode node, T nodeAsReference)
            {
                Node = node;
                NodeAsReference = nodeAsReference;
                Graph = graph;
            }
        }
    }
}