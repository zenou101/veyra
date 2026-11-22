using System;
using System.Threading;
using Veyra.Display.Recovery;
using Xunit;

namespace Veyra.Display.Tests;

public sealed class MutexTests
{
    [Fact]
    public void Second_process_cannot_enter_while_the_first_holds_the_lock()
    {
        Assert.True(DisplayChangeCoordinator.TryEnter(TimeSpan.FromMilliseconds(200), out var held, out _));
        try
        {
            Exception? failure = null;
            var other = new Thread(() =>
            {
                try
                {
                    Assert.False(DisplayChangeCoordinator.TryEnter(TimeSpan.FromMilliseconds(50), out var second, out var error));
                    Assert.Null(second);
                    Assert.NotEmpty(error);
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });

            other.Start();
            other.Join();
            if (failure is not null) throw failure;
        }
        finally
        {
            DisplayChangeCoordinator.Exit(held);
        }
    }
}
