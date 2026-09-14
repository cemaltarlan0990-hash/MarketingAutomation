namespace CrmRegistrationGateway.Validation;

public sealed class ValidationResult
{
    private readonly Dictionary<string, List<string>> _errors =
        new(StringComparer.OrdinalIgnoreCase);

    public bool IsValid => _errors.Count == 0;

    public IReadOnlyDictionary<string, string[]> Errors =>
        _errors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());

    public void Add(string field, string message)
    {
        if (!_errors.TryGetValue(field, out var messages))
        {
            messages = [];
            _errors[field] = messages;
        }

        messages.Add(message);
    }
}
