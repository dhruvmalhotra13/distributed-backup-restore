using BackupRestore.Core;
using FluentAssertions;
using Xunit;

namespace BackupRestore.Tests;

public class BackupIdFactoryTests
{
    [Fact]
    public void NewId_has_expected_prefix_and_is_unique()
    {
        var a = BackupIdFactory.NewId();
        var b = BackupIdFactory.NewId();

        a.Should().StartWith("backup-");
        a.Should().NotBe(b);
        a.Length.Should().Be("backup-".Length + 8);
    }
}
