namespace SwitchAnalyzer
{
    internal class SwitchArgumentTypeItem<T>
    {
        public SwitchArgumentTypeItem(string name, T value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; }

        public T Value { get; set; }
    }
}
