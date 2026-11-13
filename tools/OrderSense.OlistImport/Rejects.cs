namespace OrderSense.OlistImport;

public sealed class Rejects
{
    private readonly Dictionary<string, int> _counts = [];

    public int Total => _counts.Values.Sum();

    public IEnumerable<KeyValuePair<string, int>> Items => _counts;

    public void Add(string reason) => _counts[reason] = _counts.GetValueOrDefault(reason) + 1;
}