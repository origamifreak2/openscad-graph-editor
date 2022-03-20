using System.Linq;
using GodotExt;
using OpenScadGraphEditor.Library;
using OpenScadGraphEditor.Utils;

namespace OpenScadGraphEditor.Nodes
{
    public class ModuleEntryPoint : EntryPoint, IMultiExpressionOutputNode, IReferToAnInvokable
    {
        private ModuleDescription _description;

        public override string NodeTitle => _description.NodeNameOrFallback;
        public override string NodeDescription => _description.Description;


        public override void SaveInto(SavedNode node)
        {
            node.SetData("module_description_id", _description.Id);
            base.SaveInto(node);
        }

        public override void LoadFrom(SavedNode node, IReferenceResolver referenceResolver)
        {
            var functionDescriptionId = node.GetData("module_description_id");
            Setup(referenceResolver.ResolveModuleReference(functionDescriptionId));
            base.LoadFrom(node, referenceResolver);
        }

        public void Setup(InvokableDescription description)
        {
            GdAssert.That(description is ModuleDescription, "needs a module description");

            _description = (ModuleDescription) description;
            OutputPorts
                .Flow();

            foreach (var parameter in description.Parameters)
            {
                OutputPorts
                    .OfType(parameter.TypeHint, parameter.Name);
            }
        }

        public override string Render(IScadGraph context)
        {
            var arguments = _description.Parameters.Indices()
                .Select(it => _description.Parameters[it].Name + RenderOutput(context, it + 1).PrefixUnlessEmpty(" = "));
            var content = RenderOutput(context, 0);

            return $"module {_description.Name}({string.Join(", ", arguments)}){content.AsBlock()}";
        }

        public string RenderExpressionOutput(IScadGraph context, int port)
        {
            // return simply the parameter name.
            return _description.Parameters[port - 1].Name;
        }

        public bool IsExpressionPort(int port)
        {
            return port > 0 && port <= _description.Parameters.Count;
        }
    }
}