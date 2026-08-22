using System.Text.RegularExpressions;
using Xunit;

namespace EventSourcingBankAccountWeb.Tests;

public sealed class EcommerceEventPublishingArchitectureTests
{
    private static readonly string RepoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    private static readonly string EcommerceRoot = Path.Combine(RepoRoot, "src", "EcommerceCheckoutFlow");
    private static readonly string ApplicationRoot = Path.Combine(EcommerceRoot, "Application");

    [Fact]
    public void application_layer_must_not_depend_on_secondary_adapters_or_entity_framework()
    {
        var csFiles = Directory.GetFiles(ApplicationRoot, "*.cs", SearchOption.AllDirectories);
        var offenders = new List<string>();

        foreach (var file in csFiles)
        {
            var content = File.ReadAllText(file);
            if (content.Contains("EcommerceCheckoutFlow.Adapters", StringComparison.Ordinal)
                || content.Contains("Microsoft.EntityFrameworkCore", StringComparison.Ordinal))
            {
                offenders.Add(Path.GetRelativePath(RepoRoot, file));
            }
        }

        Assert.True(
            offenders.Count == 0,
            $"Application code must depend on application ports, not adapter technologies. Offenders: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void publish_calls_in_application_layer_must_use_standardized_event_bus_abstraction()
    {
        var csFiles = Directory.GetFiles(ApplicationRoot, "*.cs", SearchOption.AllDirectories);

        foreach (var file in csFiles)
        {
            var content = File.ReadAllText(file);

            Assert.DoesNotContain("capPublisher.PublishAsync(", content, StringComparison.Ordinal);
            Assert.DoesNotContain("ICapPublisher", content, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void only_cap_event_bus_adapter_can_call_raw_publisher_publish_async()
    {
        var allCsFiles = Directory.GetFiles(EcommerceRoot, "*.cs", SearchOption.AllDirectories);
        var offenders = new List<string>();

        foreach (var file in allCsFiles)
        {
            var content = File.ReadAllText(file);
            if (!content.Contains("capPublisher.PublishAsync(", StringComparison.Ordinal))
            {
                continue;
            }

            if (!file.EndsWith(Path.Combine("Adapters", "Secondary", "CapEventBus.cs"), StringComparison.Ordinal))
            {
                offenders.Add(Path.GetRelativePath(RepoRoot, file));
            }
        }

        Assert.True(offenders.Count == 0, $"Raw CAP publish call is only allowed in CapEventBus adapter. Offenders: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void application_publish_path_must_provide_order_id_partition_key_or_envelope_based_routing()
    {
        var csFiles = Directory.GetFiles(ApplicationRoot, "*.cs", SearchOption.AllDirectories);

        foreach (var file in csFiles)
        {
            var lines = File.ReadAllLines(file);

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (!line.Contains("eventBus.PublishAsync(", StringComparison.Ordinal))
                {
                    continue;
                }

                var invocation = CollectInvocation(lines, i);
                var hasOrderIdPartition = invocation.Contains("GetPartitionKey()", StringComparison.Ordinal)
                    || Regex.IsMatch(invocation, @"\bpartitionKey\b", RegexOptions.CultureInvariant)
                    || Regex.IsMatch(invocation, @"\.OrderId\b", RegexOptions.CultureInvariant);

                Assert.True(
                    hasOrderIdPartition,
                    $"Publish call in {Path.GetRelativePath(RepoRoot, file)} line {i + 1} must carry OrderId partition affinity (partitionKey/OrderId/GetPartitionKey). Invocation: {invocation}");
            }
        }
    }

    private static string CollectInvocation(IReadOnlyList<string> lines, int startLine)
    {
        var buffer = new List<string>();
        var balance = 0;
        var started = false;

        for (var i = startLine; i < lines.Count; i++)
        {
            var line = lines[i];
            buffer.Add(line.Trim());

            foreach (var ch in line)
            {
                if (ch == '(')
                {
                    balance++;
                    started = true;
                }
                else if (ch == ')')
                {
                    balance--;
                }
            }

            if (started && balance <= 0)
            {
                break;
            }
        }

        return string.Join(" ", buffer);
    }
}
