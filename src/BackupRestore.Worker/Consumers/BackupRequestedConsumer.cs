using BackupRestore.Core.Contracts;
using BackupRestore.Worker.Services;
using MassTransit;

namespace BackupRestore.Worker.Consumers;

public class BackupRequestedConsumer : IConsumer<BackupRequested>
{
    private readonly BackupProcessor _processor;

    public BackupRequestedConsumer(BackupProcessor processor)
    {
        _processor = processor;
    }

    public Task Consume(ConsumeContext<BackupRequested> context)
        => _processor.ProcessAsync(context.Message, context.CancellationToken);
}
