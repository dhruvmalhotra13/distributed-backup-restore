using BackupRestore.Core;
using FluentAssertions;
using Xunit;

namespace BackupRestore.Tests;

public class ProgressCalculatorTests
{
    [Theory]
    [InlineData(0, 100, 0)]
    [InlineData(50, 100, 50)]
    [InlineData(62, 100, 62)]
    [InlineData(100, 100, 100)]
    public void Percent_computes_byte_based_progress(long processed, long total, double expected)
    {
        ProgressCalculator.Percent(processed, total).Should().BeApproximately(expected, 0.0001);
    }

    [Fact]
    public void Percent_is_clamped_and_safe_for_zero_total()
    {
        ProgressCalculator.Percent(0, 0).Should().Be(0);
        ProgressCalculator.Percent(10, 0).Should().Be(100);
        ProgressCalculator.Percent(150, 100).Should().Be(100);
    }

    [Fact]
    public void Monotonic_never_returns_lower_than_committed()
    {
        ProgressCalculator.Monotonic(500, 400).Should().Be(500);
        ProgressCalculator.Monotonic(500, 600).Should().Be(600);
    }
}
