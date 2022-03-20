using System.Globalization;

namespace OpenScadGraphEditor.Nodes
{
    public class NumberLiteral : IScadLiteral
    {
        public NumberLiteral(double value)
        {
            Value = value;
        }

        public double Value { get; set; }

        public string LiteralValue => Value.ToString(CultureInfo.InvariantCulture);

        public string SerializedValue
        {
            get => Value.ToString(CultureInfo.InvariantCulture);
            set => Value = double.Parse(value);
        }
    }
}