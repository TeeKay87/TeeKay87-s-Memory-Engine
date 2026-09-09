using System.Collections.Generic;
using TeeKay87.MemoryEngine.PluginSdk.Models;

namespace TeeKay87.MemoryEngine.PluginSdk.Contracts;

public interface IMemoryScanOption
{
    string Id { get; }

    string DisplayName { get; }

    string Description { get; }

    IReadOnlyList<MemoryScanOptionChoice> Choices { get; }

    string DefaultChoiceId { get; }

    bool LockAfterFirstScan { get; }

    bool SupportsValueType(IMemoryValueType valueType);
}
