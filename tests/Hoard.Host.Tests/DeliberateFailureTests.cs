namespace Hoard.Host.Tests;

// Temporary: proves the PR gate fails and names the failing test (T043). Reverted next commit.
public sealed class DeliberateFailureTests
{
    [Fact]
    public void Gate_must_fail_on_this_test() => Assert.Fail("deliberate failure to prove the PR gate");
}
