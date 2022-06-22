using System.Text;
using JetBrains.Annotations;
using OpenScadGraphEditor.Library;
using OpenScadGraphEditor.Library.IO;
using OpenScadGraphEditor.Utils;

namespace OpenScadGraphEditor.Nodes.ForLoop
{
    [UsedImplicitly]
    public class ForLoopEnd : ScadNode, ICanHaveModifier, IAmBoundToOtherNode
    {
        public override string NodeTitle => "End Loop";
        public override string NodeDescription => "Collects the generated geometry from the loop.";

        public string OtherNodeId { get; set; }

        public ForLoopEnd()
        {
            InputPorts
                .Geometry();

            OutputPorts
                .Geometry();
        }
  

        public override string GetPortDocumentation(PortId portId)
        {
            if (portId.IsInput)
            {
                return "Geometry generated by the loop.";
            }

            if (portId.IsOutput)
            {
                return "Output geometry.";
            }

            return "";
        }


        public override void SaveInto(SavedNode node)
        {
            node.SetData("loopStartId", OtherNodeId);
            base.SaveInto(node);
        }

        public override void RestorePortDefinitions(SavedNode node, IReferenceResolver referenceResolver)
        {
            OtherNodeId = node.GetDataString("loopStartId");
            base.RestorePortDefinitions(node, referenceResolver);
        }

        public override string Render(ScadGraph context, int portIndex)
        {
            var loopStart = (ForLoopStart) context.ById(OtherNodeId);
            
            var builder = new StringBuilder(loopStart.IntersectMode ? "intersection_for(" : "for(");
            for (var i = 0; i < loopStart.CurrentInputSize; i++)
            {
                var variableName = loopStart.Render(context, i);
                var array = loopStart.RenderInput(context, i).OrUndef();
                builder.Append(variableName)
                    .Append(" = ")
                    .Append(array);
                if (i + 1 < loopStart.CurrentInputSize)
                {
                    builder.Append(", ");
                }
            }

            builder.Append(")");

            var children =RenderInput(context, 0);

            return $"{builder}{children.AsBlock()}";
        }

    }
}