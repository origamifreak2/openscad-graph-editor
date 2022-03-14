using OpenScadGraphEditor.Library;

namespace OpenScadGraphEditor.Nodes
{
    public sealed class StringConstant : ScadExpressionNode
    {
        public override string NodeTitle => "String constant";
        public override string NodeDescription => "A string constant";


        public StringConstant()
        {
            OutputPorts
                .String();
        }

        public override string Render(IScadGraph context)
        {
            return RenderOutput(context, 0);
        }
    }
}