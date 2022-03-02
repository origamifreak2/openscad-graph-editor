using JetBrains.Annotations;

namespace OpenScadGraphEditor.Nodes
{
    [UsedImplicitly]
    public class ModuloOperator : BinaryOperator
    {
        public override string NodeTitle => "%";
        public override string NodeDescription => "Calculates the modulus the given inputs.";
        protected override string OperatorSign => "%";

        public ModuloOperator()
        {
            InputPorts
                .Number()
                .Number();

            OutputPorts
                .Number();
        }
    }
}